using System.Text.Json;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Cake.Extensions.GitHub;

// The SHA-256 GitHub records for each release asset (the release API's "digest", "sha256:<hex>").
public static class GitHubReleaseDigests
{
    public static IReadOnlyDictionary<string, string> Parse(string releaseJson)
    {
        using var document = JsonDocument.Parse(releaseJson);
        var digests = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var asset in document.RootElement.GetProperty("assets").EnumerateArray())
        {
            if (asset.TryGetProperty("digest", out var digest) && digest.ValueKind == JsonValueKind.String
                && digest.GetString() is { } value && value.StartsWith("sha256:", StringComparison.Ordinal))
            {
                digests[asset.GetProperty("name").GetString()!] = value["sha256:".Length..].ToLowerInvariant();
            }
        }

        return digests;
    }

    public static IReadOnlyDictionary<string, string> Read(ICakeContext context, string repository, string tag) =>
        Parse(new GitHubCli(context).Gh(new ProcessArgumentBuilder().Append("api").Append($"repos/{repository}/releases/tags/{tag}")));
}
