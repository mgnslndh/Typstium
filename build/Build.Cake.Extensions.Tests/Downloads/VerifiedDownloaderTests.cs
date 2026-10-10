using System.Net;
using System.Security.Cryptography;
using System.Text;
using Build.Cake.Extensions.Downloads;

namespace Build.Cake.Extensions.Tests.Downloads;

public class VerifiedDownloaderTests
{
    private static readonly byte[] Content = Encoding.UTF8.GetBytes("typst release asset");

    private static readonly string Hash = Convert.ToHexStringLower(SHA256.HashData(Content));

    private static readonly Uri Url = new("https://github.com/o/r/releases/download/v1/asset.zip");

    // Answers each request with the next response; records the requests.
    private sealed class StubHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] responses) : HttpMessageHandler
    {
        private int _next;

        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(responses[Math.Min(_next++, responses.Length - 1)](request));
        }
    }

    // The first request never answers until its token is cancelled (a stall); the second one succeeds.
    private sealed class StallOnceHandler : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (++Requests == 1)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            return Ok();
        }
    }

    private static HttpResponseMessage Ok() => new(HttpStatusCode.OK) { Content = new ByteArrayContent(Content) };

    private static VerifiedDownloader Downloader(StubHandler handler) => new(handler, _ => Task.CompletedTask);

    [Test]
    public async Task Downloads_and_verifies_the_file()
    {
        using var temp = new TempFolder();
        var handler = new StubHandler(_ => Ok());

        await Downloader(handler).DownloadAsync(Url, temp.Combine("asset.zip"), Hash);

        await Assert.That(File.ReadAllBytes(temp.Combine("asset.zip"))).IsEquivalentTo(Content);
        await Assert.That(File.Exists(temp.Combine("asset.zip.partial"))).IsFalse();
    }

    [Test]
    public async Task A_cached_file_with_the_pinned_hash_needs_no_request()
    {
        using var temp = new TempFolder();
        File.WriteAllBytes(temp.Combine("asset.zip"), Content);
        var handler = new StubHandler(_ => throw new InvalidOperationException("no request expected"));

        await Downloader(handler).DownloadAsync(Url, temp.Combine("asset.zip"), Hash);

        await Assert.That(handler.Requests).IsEmpty();
    }

    [Test]
    public async Task A_hash_mismatch_fails_names_both_hashes_and_leaves_nothing_behind()
    {
        using var temp = new TempFolder();
        var pinned = new string('0', 64);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => Downloader(new StubHandler(_ => Ok())).DownloadAsync(Url, temp.Combine("asset.zip"), pinned));

        await Assert.That(exception!.Message).Contains(Hash);
        await Assert.That(exception.Message).Contains(pinned);
        await Assert.That(Directory.GetFiles(temp.Path)).IsEmpty();
    }

    [Test]
    [Arguments(HttpStatusCode.BadGateway)]
    [Arguments(HttpStatusCode.TooManyRequests)]
    public async Task Server_errors_are_retried(HttpStatusCode status)
    {
        using var temp = new TempFolder();
        var handler = new StubHandler(_ => new HttpResponseMessage(status), _ => Ok());

        await Downloader(handler).DownloadAsync(Url, temp.Combine("asset.zip"), Hash);

        await Assert.That(handler.Requests.Count).IsEqualTo(2);
    }

    [Test]
    public async Task A_stalled_request_is_retried()
    {
        using var temp = new TempFolder();
        var handler = new StallOnceHandler();
        using var downloader = new VerifiedDownloader(handler, _ => Task.CompletedTask, TimeSpan.FromMilliseconds(200));

        await downloader.DownloadAsync(Url, temp.Combine("asset.zip"), Hash);

        await Assert.That(handler.Requests).IsEqualTo(2);
        await Assert.That(File.ReadAllBytes(temp.Combine("asset.zip"))).IsEquivalentTo(Content);
    }

    [Test]
    public async Task Plain_http_urls_are_refused_before_any_request()
    {
        using var temp = new TempFolder();
        var handler = new StubHandler(_ => Ok());

        await Assert.ThrowsAsync<InvalidOperationException>(() => Downloader(handler).DownloadAsync(new Uri("http://example.com/asset.zip"), temp.Combine("asset.zip"), Hash));

        await Assert.That(handler.Requests).IsEmpty();
    }

    [Test]
    public async Task A_cached_file_matches_an_upper_case_pinned_hash()
    {
        using var temp = new TempFolder();
        File.WriteAllBytes(temp.Combine("asset.zip"), Content);
        var handler = new StubHandler(_ => throw new InvalidOperationException("no request expected"));

        await Downloader(handler).DownloadAsync(Url, temp.Combine("asset.zip"), Hash.ToUpperInvariant());

        await Assert.That(handler.Requests).IsEmpty();
    }

    [Test]
    public async Task Https_redirects_are_followed()
    {
        using var temp = new TempFolder();
        var target = new Uri("https://objects.githubusercontent.com/asset");
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = target } }, _ => Ok());

        await Downloader(handler).DownloadAsync(Url, temp.Combine("asset.zip"), Hash);

        await Assert.That(handler.Requests).IsEquivalentTo([Url, target], TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Redirects_to_plain_http_are_refused()
    {
        using var temp = new TempFolder();
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = new Uri("http://example.com/asset") } });

        await Assert.ThrowsAsync<InvalidOperationException>(() => Downloader(handler).DownloadAsync(Url, temp.Combine("asset.zip"), Hash));
    }
}
