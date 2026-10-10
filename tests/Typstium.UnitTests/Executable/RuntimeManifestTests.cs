using Typstium.Executable;
using Typstium.UnitTests.Testing;

namespace Typstium.UnitTests.Executable;

public class RuntimeManifestTests
{
    private static readonly string Directory = Path.Combine(FakeLocatorEnvironment.Root, "app");

    private static readonly string ManifestPath = Path.Combine(Directory, RuntimeManifest.FileName);

    private static RuntimeManifestResult Parse(string json, bool isWindows = false) => RuntimeManifest.Parse(ManifestPath, json, isWindows);

    private static string Runtimes(string rid, string path) => $$"""{ "version": 1, "runtimes": { "{{rid}}": "{{path}}" } }""";

    [Test]
    [Property("Scenario", "RS31")]
    public async Task Paths_resolve_against_the_manifests_folder()
    {
        var result = Parse("""{ "version": 1, "runtimes": { "linux-x64": "tools/typst/typst", "win-x64": "typst/win-x64/typst.exe" } }""");

        var loaded = (RuntimeManifestResult.Loaded)result;
        await Assert.That(loaded.Path).IsEqualTo(ManifestPath);
        await Assert.That(loaded.Manifest.Executables["linux-x64"]).IsEqualTo(Path.Combine(Directory, "tools", "typst", "typst"));
        await Assert.That(loaded.Manifest.Executables["win-x64"]).IsEqualTo(Path.Combine(Directory, "typst", "win-x64", "typst.exe"));
    }

    [Test]
    public async Task A_byte_order_mark_and_crlf_line_endings_are_accepted()
    {
        var result = Parse("\uFEFF{\r\n  \"version\": 1,\r\n  \"runtimes\": { \"linux-x64\": \"typst/linux-x64/typst\" }\r\n}\r\n");

        await Assert.That(result is RuntimeManifestResult.Loaded).IsTrue();
    }

    [Test]
    [Arguments("not json", "not valid JSON: ")]
    [Arguments("[]", "the top level must be an object")]
    [Arguments("""{ "runtimes": { "linux-x64": "a" } }""", "\"version\" is missing")]
    [Arguments("""{ "version": "1", "runtimes": { "linux-x64": "a" } }""", "\"version\" must be a number")]
    [Arguments("""{ "version": 2, "runtimes": { "linux-x64": "a" } }""", "unknown version 2")]
    [Arguments("""{ "version": 1 }""", "\"runtimes\" must be a non-empty object")]
    [Arguments("""{ "version": 1, "runtimes": [] }""", "\"runtimes\" must be a non-empty object")]
    [Arguments("""{ "version": 1, "runtimes": {} }""", "\"runtimes\" must be a non-empty object")]
    [Arguments("""{ "version": 1, "runtimes": { "": "a" } }""", "a runtime identifier is empty")]
    [Arguments("""{ "version": 1, "runtimes": { "linux-x64": "a", "linux-x64": "b" } }""", "linux-x64 is listed twice")]
    [Arguments("""{ "version": 1, "runtimes": { "linux-x64": 1 } }""", "the path for linux-x64 must be a string")]
    [Arguments("""{ "version": 1, "runtimes": { "linux-x64": "" } }""", "the path for linux-x64 is empty")]
    public async Task Malformed_manifests_are_invalid_with_a_reason(string json, string reason)
    {
        var invalid = (RuntimeManifestResult.Invalid)Parse(json);

        await Assert.That(invalid.Path).IsEqualTo(ManifestPath);
        await Assert.That(invalid.Reason).StartsWith(reason);
    }

    [Test]
    [Arguments("/usr/bin/typst", false)]
    [Arguments(@"C:\\typst\\typst.exe", true)]
    [Arguments("C:typst.exe", true)]
    [Arguments(@"\\typst.exe", true)]
    [Arguments("/typst.exe", true)]
    [Arguments(@"\\\\server\\share\\typst.exe", true)]
    public async Task Paths_that_are_not_relative_are_rejected(string path, bool isWindows)
    {
        var invalid = (RuntimeManifestResult.Invalid)Parse(Runtimes("linux-x64", path), isWindows);
        var unescaped = path.Replace(@"\\", @"\");

        await Assert.That(invalid.Reason).IsEqualTo($"path '{unescaped}' for linux-x64 is not relative");
    }

    [Test]
    [Arguments("../typst", false)]
    [Arguments("tools/../../typst", false)]
    [Arguments(@"tools\\..\\typst", true)]
    [Arguments(@"tools\\..\\typst", false)]
    public async Task Parent_segments_are_rejected(string path, bool isWindows)
    {
        var invalid = (RuntimeManifestResult.Invalid)Parse(Runtimes("linux-x64", path), isWindows);

        await Assert.That(invalid.Reason).IsEqualTo($"path '{path.Replace(@"\\", @"\")}' for linux-x64 contains '..'");
    }

    [Test]
    public async Task An_entry_with_an_embedded_NUL_is_invalid_and_does_not_throw()
    {
        // JSON escape \u0000 decodes to a NUL character, which the file system rejects with ArgumentException.
        var invalid = (RuntimeManifestResult.Invalid)Parse(Runtimes("linux-x64", @"a\u0000b"));

        await Assert.That(invalid.Reason).EndsWith("is not a valid path");
    }

    [Test]
    public async Task A_colon_inside_a_windows_entry_does_not_throw()
    {
        // .NET Framework's GetFullPath throws NotSupportedException here, which Parse reports as invalid. Newer runtimes may
        // accept the colon during path normalization and return Loaded. Either outcome is fine; the point is that Parse does not throw.
        var result = Parse(Runtimes("linux-x64", "tools/a:b"), isWindows: true);

        await Assert.That(result is RuntimeManifestResult.Invalid or RuntimeManifestResult.Loaded).IsTrue();
    }
}
