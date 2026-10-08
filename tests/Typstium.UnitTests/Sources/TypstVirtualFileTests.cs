using System.Text;
using System.Text.Json.Serialization;
using TUnit.Assertions.Enums;

namespace Typstium.UnitTests.Sources;

public class TypstVirtualFileTests
{
    public sealed record License(string LicenseId, int Seats);

    [Test]
    public async Task FromText_writes_utf8_without_bom()
    {
        var file = TypstVirtualFile.FromText(TypstPath.Parse("notes.md"), "åäö");
        await Assert.That(file.Content.ToArray()).IsEquivalentTo(Encoding.UTF8.GetBytes("åäö"), CollectionOrdering.Matching);
    }

    [Test]
    public async Task FromJson_uses_camel_case_web_defaults()
    {
        var file = TypstVirtualFile.FromJson(TypstPath.Parse("data.json"), new License("FJL-1", 25));
        var json = Encoding.UTF8.GetString(file.Content.ToArray());
        await Assert.That(json).IsEqualTo("{\"licenseId\":\"FJL-1\",\"seats\":25}");
    }

    [Test]
    public async Task FromJson_with_type_info_is_aot_friendly()
    {
        var file = TypstVirtualFile.FromJson(TypstPath.Parse("data.json"), new License("FJL-2", 1), TestJsonContext.Default.License);
        var json = Encoding.UTF8.GetString(file.Content.ToArray());
        await Assert.That(json).Contains("FJL-2");
    }

    [Test]
    public async Task FromStreamAsync_reads_the_stream_once_up_front()
    {
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var file = await TypstVirtualFile.FromStreamAsync(TypstPath.Parse("logo.png"), stream, CancellationToken.None);
        await Assert.That(file.Content.Length).IsEqualTo(3);
        await Assert.That(stream.Position).IsEqualTo(3L);
    }
}

[JsonSerializable(typeof(TypstVirtualFileTests.License))]
internal sealed partial class TestJsonContext : JsonSerializerContext;
