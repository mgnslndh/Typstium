namespace Typstium.UnitTests.Sources;

public class TypstPathTests
{
    [Test]
    [Arguments("logo.png", "logo.png")]
    [Arguments("/logo.png", "logo.png")]
    [Arguments("assets/logo.png", "assets/logo.png")]
    [Arguments("assets\\logo.png", "assets/logo.png")]
    [Arguments("./assets/./logo.png", "assets/logo.png")]
    [Arguments("data/åäö file.json", "data/åäö file.json")]
    public async Task Parse_normalizes(string input, string expected)
    {
        var path = TypstPath.Parse(input);
        await Assert.That(path.Value).IsEqualTo(expected);
        await Assert.That(path.RootRelative).IsEqualTo("/" + expected);
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    [Arguments("/")]
    [Arguments("../secret.txt")]
    [Arguments("assets/../../secret.txt")]
    [Arguments("assets/..")]
    [Arguments("C:/Windows/win.ini")]
    [Arguments("C:\\Windows\\win.ini")]
    [Arguments("\\\\server\\share\\file.txt")]
    [Arguments("assets//logo.png")]
    [Arguments("assets/lo\0go.png")]
    [Arguments("assets/lo:go.png")]
    [Arguments("assets/lo*go.png")]
    public async Task Parse_rejects_invalid_paths(string input)
    {
        var parsed = TypstPath.TryParse(input, out var path);
        await Assert.That(parsed).IsFalse();
        await Assert.That(path).IsNull();
        var exception = Assert.Throws<ArgumentException>(() => TypstPath.Parse(input));
        await Assert.That(exception!.ParamName).IsEqualTo("value");
    }

    [Test]
    public async Task Paths_compare_ordinally()
    {
        await Assert.That(TypstPath.Parse("/a/b.typ")).IsEqualTo(TypstPath.Parse("a\\b.typ"));
        await Assert.That(TypstPath.Parse("a/B.typ").Equals(TypstPath.Parse("a/b.typ"))).IsFalse();
    }

    [Test]
    public async Task Segments_and_file_name()
    {
        var path = TypstPath.Parse("assets/img/logo.png");
        await Assert.That(path.Segments).IsEquivalentTo(new[] { "assets", "img", "logo.png" });
        await Assert.That(path.FileName).IsEqualTo("logo.png");
        await Assert.That(path.ToString()).IsEqualTo("/assets/img/logo.png");
    }
}
