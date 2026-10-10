using System.Security.Cryptography;

namespace Build.Cake.Extensions.Downloads;

// Downloads a file whose SHA-256 is pinned, over https only, with retries and a stall timeout. A file already at the
// destination with the pinned hash is reused. Ideas from Cake.Download.Module's HttpDownloader (same author, MIT).
public sealed class VerifiedDownloader : IDisposable
{
    private const int MaxAttempts = 3;
    private const int MaxRedirects = 5;
    private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(30);

    private readonly HttpClient _client;
    private readonly Func<TimeSpan, Task> _delay;

    // The handler must not follow redirects: they are followed here, so that each hop can be checked for https.
    public VerifiedDownloader(HttpMessageHandler handler, Func<TimeSpan, Task>? delay = null)
    {
        _client = new HttpClient(handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
        _delay = delay ?? (wait => Task.Delay(wait));
    }

    public static VerifiedDownloader CreateDefault() => new(new SocketsHttpHandler { AllowAutoRedirect = false });

    public async Task DownloadAsync(Uri url, string destination, string expectedSha256, CancellationToken cancellationToken = default)
    {
        if (File.Exists(destination) && await HashFileAsync(destination, cancellationToken) == expectedSha256)
        {
            return;
        }

        var partial = destination + ".partial";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
        try
        {
            var actual = await DownloadWithRetriesAsync(url, partial, cancellationToken);
            if (actual != expectedSha256)
            {
                throw new InvalidDataException($"{url} has SHA-256 {actual}, but {expectedSha256} is pinned. Find out why it changed before re-pinning.");
            }

            File.Move(partial, destination, overwrite: true);
        }
        finally
        {
            File.Delete(partial);
        }
    }

    public void Dispose() => _client.Dispose();

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
    }

    private async Task<string> DownloadWithRetriesAsync(Uri url, string path, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await DownloadOnceAsync(url, path, cancellationToken);
            }
            catch (Exception exception) when (attempt < MaxAttempts && exception is HttpRequestException or IOException or TimeoutException)
            {
                await _delay(TimeSpan.FromSeconds(attempt));
            }
        }
    }

    private async Task<string> DownloadOnceAsync(Uri url, string path, CancellationToken cancellationToken)
    {
        using var stall = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var response = await SendFollowingRedirectsAsync(url, stall);
        var status = (int)response.StatusCode;
        if (status is 408 or 429 or >= 500)
        {
            throw new HttpRequestException($"{url}: HTTP {status} ({response.ReasonPhrase}).");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Downloading {url} failed: HTTP {status} ({response.ReasonPhrase}).");
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        await using var source = await response.Content.ReadAsStreamAsync(stall.Token);
        await using var target = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        while (true)
        {
            stall.CancelAfter(StallTimeout);
            int read;
            try
            {
                read = await source.ReadAsync(buffer, stall.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"{url}: no data for {StallTimeout.TotalSeconds} s.");
            }

            if (read == 0)
            {
                return Convert.ToHexStringLower(hash.GetHashAndReset());
            }

            hash.AppendData(buffer, 0, read);
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    private async Task<HttpResponseMessage> SendFollowingRedirectsAsync(Uri url, CancellationTokenSource stall)
    {
        var current = url;
        for (var redirects = 0; ; redirects++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            request.Headers.UserAgent.ParseAdd("Typstium-build");
            stall.CancelAfter(StallTimeout);
            var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, stall.Token);
            if ((int)response.StatusCode is not (301 or 302 or 303 or 307 or 308))
            {
                return response;
            }

            var location = response.Headers.Location;
            response.Dispose();
            var next = location is null ? throw new InvalidOperationException($"{current} redirected without a Location header.")
                : location.IsAbsoluteUri ? location : new Uri(current, location);
            if (next.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidOperationException($"{current} redirected to {next}, which isn't https.");
            }

            if (redirects + 1 > MaxRedirects)
            {
                throw new InvalidOperationException($"{url} redirected more than {MaxRedirects} times.");
            }

            current = next;
        }
    }
}
