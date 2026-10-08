using Typstium.Capabilities;

namespace Typstium.UnitTests.Capabilities;

public class TypstInfoParserTests
{
    private const string Typst0151Info =
        "{\"version\":\"0.15.1\",\"build\":{\"commit\":\"9dfd3a08500b7896045f907433cf7b4b02434fad\",\"platform\":{\"os\":\"windows\",\"arch\":\"x86_64\"},\"settings\":{\"self-update\":true,\"http-server\":true}},\"features\":{\"html\":false,\"bundle\":false,\"a11y-extras\":false},\"fonts\":{\"paths\":[],\"system\":true,\"embedded\":true},\"packages\":{\"package-path\":\"C:\\\\Users\\\\x\\\\AppData\\\\Roaming\\\\typst/packages\",\"package-cache-path\":\"C:\\\\Users\\\\x\\\\AppData\\\\Local\\\\typst/packages\"}}";

    [Test]
    public async Task Parses_version_and_available_features()
    {
        var info = TypstInfoParser.Parse(Typst0151Info);
        await Assert.That(info.Version).IsEqualTo(new TypstVersion(0, 15, 1));
        await Assert.That(info.Features.Contains("html")).IsTrue();
        await Assert.That(info.Features.Contains("bundle")).IsTrue();
    }

    [Test]
    public async Task Missing_features_object_means_no_features()
    {
        var info = TypstInfoParser.Parse("{\"version\":\"0.16.0\"}");
        await Assert.That(info.Features.Count).IsEqualTo(0);
    }

    [Test]
    [Arguments("not json")]
    [Arguments("{}")]
    [Arguments("{\"version\":\"banana\"}")]
    public async Task Unexpected_json_is_a_process_output_error(string json)
    {
        var exception = Assert.Throws<TypstProcessOutputException>(() => TypstInfoParser.Parse(json));
        await Assert.That(exception).IsNotNull();
    }

    [Test]
    [Arguments("typst 0.14.2 (abcdef12)", 0, 14, 2)]
    [Arguments("typst 0.15.1 (9dfd3a08)\n", 0, 15, 1)]
    public async Task Parses_version_output(string text, int major, int minor, int patch)
    {
        await Assert.That(TypstInfoParser.ParseVersionOutput(text)).IsEqualTo(new TypstVersion(major, minor, patch));
    }
}
