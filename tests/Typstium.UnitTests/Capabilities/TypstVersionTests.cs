namespace Typstium.UnitTests.Capabilities;

public class TypstVersionTests
{
    [Test]
    [Arguments("0.15.1", 0, 15, 1)]
    [Arguments("1.0.0", 1, 0, 0)]
    [Arguments("0.16.0-rc1", 0, 16, 0)]
    public async Task Parse_reads_major_minor_patch(string input, int major, int minor, int patch)
    {
        var version = TypstVersion.Parse(input);
        await Assert.That(version).IsEqualTo(new TypstVersion(major, minor, patch));
    }

    [Test]
    public async Task Versions_compare_numerically()
    {
        var older = TypstVersion.Parse("0.9.0");
        var newer = TypstVersion.Parse("0.15.0");
        await Assert.That(older < newer).IsTrue();
        await Assert.That(newer.CompareTo(older)).IsGreaterThan(0);
        await Assert.That(newer.ToString()).IsEqualTo("0.15.0");
    }

    [Test]
    [Arguments("")]
    [Arguments("0.15")]
    [Arguments("v0.15.1")]
    [Arguments("a.b.c")]
    public async Task TryParse_rejects_malformed_versions(string input)
    {
        var parsed = TypstVersion.TryParse(input, out _);
        await Assert.That(parsed).IsFalse();
    }
}
