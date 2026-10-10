using Build.Cake.Extensions.GitHub;

namespace Build.Cake.Extensions.Tests.GitHub;

public class GitHubReleaseDigestsTests
{
    [Test]
    public async Task Reads_sha256_digests_by_asset_name_and_skips_assets_without_one()
    {
        const string release = """
            { "tag_name": "v0.15.1", "assets": [
              { "name": "typst-x86_64-pc-windows-msvc.zip", "digest": "sha256:19CE3551153C2FE7EE9FA2F95208310C8F4D3209FEDB699E0333FAF8913F6736" },
              { "name": "old.zip", "digest": null },
              { "name": "md5.zip", "digest": "md5:abc" }
            ] }
            """;

        var digests = GitHubReleaseDigests.Parse(release);

        await Assert.That(digests.Count).IsEqualTo(1);
        await Assert.That(digests["typst-x86_64-pc-windows-msvc.zip"]).IsEqualTo("19ce3551153c2fe7ee9fa2f95208310c8f4d3209fedb699e0333faf8913f6736");
    }
}
