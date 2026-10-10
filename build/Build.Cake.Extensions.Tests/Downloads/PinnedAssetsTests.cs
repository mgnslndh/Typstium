using Build.Cake.Extensions.Downloads;

namespace Build.Cake.Extensions.Tests.Downloads;

public class PinnedAssetsTests
{
    private const string Hash = "a6d077d0a95eed5a2eba715b2dae06be954f624ccbf85758a03f389ded33118c";

    [Test]
    public async Task Round_trips_with_keys_in_ordinal_order()
    {
        var assets = new PinnedAssets("0.15.1", new Dictionary<string, PinnedAsset>
        {
            ["win-x64"] = new("typst-x86_64-pc-windows-msvc.zip", Hash),
            ["linux-x64"] = new("typst-x86_64-unknown-linux-musl.tar.xz", Hash),
        });

        var json = assets.ToJson();

        await Assert.That(json).IsEqualTo($$"""
            {
              "version": "0.15.1",
              "assets": {
                "linux-x64": {
                  "name": "typst-x86_64-unknown-linux-musl.tar.xz",
                  "sha256": "{{Hash}}"
                },
                "win-x64": {
                  "name": "typst-x86_64-pc-windows-msvc.zip",
                  "sha256": "{{Hash}}"
                }
              }
            }

            """);
        var parsed = PinnedAssets.Parse(json);
        await Assert.That(parsed.Version).IsEqualTo("0.15.1");
        await Assert.That(parsed.Assets["win-x64"]).IsEqualTo(new PinnedAsset("typst-x86_64-pc-windows-msvc.zip", Hash));
    }

    [Test]
    [Arguments("""{ "assets": {} }""", "\"version\"")]
    [Arguments("""{ "version": "1", "assets": { "a": { "name": "x.zip", "sha256": "ABC" } } }""", "64 lowercase hex")]
    [Arguments("""{ "version": "1", "assets": { "a": { "name": "dir/x.zip", "sha256": "0000000000000000000000000000000000000000000000000000000000000000" } } }""", "file name")]
    [Arguments("""{ "version": "1" }""", "\"assets\"")]
    [Arguments("""{ "version": "1", "assets": { "a": { "name": "x.zip" } } }""", "\"sha256\"")]
    [Arguments("""{ "version": "1", "assets": { "a": { "name": "x.zip", "sha256": "0000" } """, "valid JSON")]
    [Arguments("""{ "version": "1", "assets": { "a": { "name": "x.zip", "sha256": "0000000000000000000000000000000000000000000000000000000000000000" }, "a": { "name": "y.zip", "sha256": "0000000000000000000000000000000000000000000000000000000000000000" } } }""", "more than once")]
    [Arguments("[1, 2]", "JSON object")]
    public async Task Rejects_lock_files_it_cant_trust(string json, string mention)
    {
        var exception = Assert.Throws<InvalidDataException>(() => PinnedAssets.Parse(json));

        await Assert.That(exception.Message).Contains(mention);
    }
}
