using System.Collections.Immutable;
using Typstium.Diagnostics;

namespace Typstium.UnitTests.Diagnostics;

public class SourcePathMapperTests
{
    private const string WindowsRoot = @"C:\Temp\typstium\ws-1\root";
    private const string UnixRoot = "/tmp/typstium/ws-1/root";

    private static SourcePathMapper Sandbox(string root, params string[] virtualFiles) =>
        new(new SourceMappingContext(root, IsSandbox: true, virtualFiles.Select(TypstPath.Parse).ToImmutableHashSet()));

    [Test]
    public async Task Sandbox_template_files_map_to_project_files_without_full_path()
    {
        var file = Sandbox(WindowsRoot).MapFile(@"\\?\C:\Temp\typstium\ws-1\root\sub\helper.typ");
        await Assert.That(file).IsEqualTo((TypstSourceFile)new TypstSourceFile.Project(TypstPath.Parse("sub/helper.typ"), null));
    }

    [Test]
    public async Task Virtual_files_map_to_virtual_and_windows_comparison_ignores_case()
    {
        var file = Sandbox(WindowsRoot, "data.json").MapFile(@"\\?\c:\temp\TYPSTIUM\ws-1\root\data.json");
        await Assert.That(file).IsEqualTo((TypstSourceFile)new TypstSourceFile.Virtual(TypstPath.Parse("data.json")));
    }

    [Test]
    public async Task Unix_roots_compare_ordinally()
    {
        var mapper = Sandbox(UnixRoot);
        await Assert.That(mapper.MapFile("/tmp/typstium/ws-1/root/a.typ")).IsNotNull();
        await Assert.That(mapper.MapFile("/TMP/typstium/ws-1/root/a.typ")).IsNull();
    }

    [Test]
    public async Task Packages_and_markup_are_recognized()
    {
        var mapper = Sandbox(WindowsRoot);
        await Assert.That(mapper.MapFile("@preview/cmarker:0.1.10/lib.typ"))
            .IsEqualTo((TypstSourceFile)new TypstSourceFile.Package("@preview/cmarker:0.1.10", TypstPath.Parse("lib.typ")));
        await Assert.That(mapper.MapFile(@"\\?\C:\Temp\typstium\ws-1\root\<stdin>")).IsEqualTo((TypstSourceFile)TypstSourceFile.Markup.Instance);
    }

    [Test]
    public async Task In_place_files_keep_their_full_path()
    {
        var mapper = new SourcePathMapper(new SourceMappingContext(@"C:\repo\docs", IsSandbox: false, ImmutableHashSet<TypstPath>.Empty));
        var file = mapper.MapFile(@"\\?\C:\repo\docs\spec.typ");
        await Assert.That(file).IsEqualTo((TypstSourceFile)new TypstSourceFile.Project(TypstPath.Parse("spec.typ"), @"C:\repo\docs\spec.typ"));
    }

    [Test]
    public async Task Paths_outside_the_root_are_not_mapped()
    {
        await Assert.That(Sandbox(WindowsRoot).MapFile(@"\\?\C:\Windows\win.ini")).IsNull();
    }

    // On Linux and macOS Typst prints paths under its working directory (the project root) relative to it,
    // e.g. "sub/helper.typ:2:2" (verified with Typst 0.15.1 on Ubuntu).
    [Test]
    public async Task Relative_printed_paths_are_root_relative_in_a_sandbox()
    {
        var mapper = Sandbox(UnixRoot, "data.json");
        await Assert.That(mapper.MapFile("sub/helper.typ")).IsEqualTo((TypstSourceFile)new TypstSourceFile.Project(TypstPath.Parse("sub/helper.typ"), null));
        await Assert.That(mapper.MapFile("data.json")).IsEqualTo((TypstSourceFile)new TypstSourceFile.Virtual(TypstPath.Parse("data.json")));
        await Assert.That(mapper.MapFile("<stdin>")).IsEqualTo((TypstSourceFile)TypstSourceFile.Markup.Instance);
    }

    [Test]
    public async Task Relative_printed_paths_get_a_full_path_for_in_place_sources()
    {
        var root = Path.Combine(Path.GetTempPath(), "repo", "docs");
        var mapper = new SourcePathMapper(new SourceMappingContext(root, IsSandbox: false, ImmutableHashSet<TypstPath>.Empty));
        var file = mapper.MapFile("chapters/intro.typ");
        var expectedFullPath = Path.Combine(root, "chapters", "intro.typ");
        await Assert.That(file).IsEqualTo((TypstSourceFile)new TypstSourceFile.Project(TypstPath.Parse("chapters/intro.typ"), expectedFullPath));
    }

    [Test]
    public async Task Relative_printed_paths_that_leave_the_root_are_not_mapped()
    {
        await Assert.That(Sandbox(UnixRoot).MapFile("../outside.typ")).IsNull();
    }

    [Test]
    public async Task Locations_keep_line_and_columns()
    {
        var location = Sandbox(UnixRoot).Map(new RawLocation("/tmp/typstium/ws-1/root/a.typ", 3, 2, 16));
        await Assert.That(location!.Start).IsEqualTo(new TypstPosition(3, 2));
        await Assert.That(location.End).IsEqualTo(new TypstPosition(3, 16));
    }

    [Test]
    public async Task Scrub_replaces_a_unix_root_with_a_slash()
    {
        await Assert.That(Sandbox(UnixRoot).Scrub("/tmp/typstium/ws-1/root/a/b.png")).IsEqualTo("/a/b.png");
    }

    [Test]
    public async Task Scrub_matches_windows_roots_case_insensitively_with_either_separator()
    {
        var mapper = Sandbox(WindowsRoot);
        await Assert.That(mapper.Scrub(@"searched at \\?\c:\temp\TYPSTIUM\ws-1\root\img.png")).IsEqualTo("searched at /img.png");
        await Assert.That(mapper.Scrub("searched at C:/Temp/typstium/ws-1/root/img.png")).IsEqualTo("searched at /img.png");
    }

    [Test]
    public async Task Scrub_scrubs_hint_text_too()
    {
        await Assert.That(Sandbox(WindowsRoot).Scrub(@"see \\?\C:\Temp\typstium\ws-1\root\x.typ")).IsEqualTo("see /x.typ");
    }

    [Test]
    public async Task Scrub_replaces_a_bare_root_and_leaves_longer_names_alone()
    {
        var mapper = Sandbox(UnixRoot);
        await Assert.That(mapper.Scrub("at /tmp/typstium/ws-1/root")).IsEqualTo("at /");
        await Assert.That(mapper.Scrub("/tmp/typstium/ws-1/rootless.typ")).IsEqualTo("/tmp/typstium/ws-1/rootless.typ");
    }

    [Test]
    public async Task Scrub_is_a_no_op_for_in_place_sources()
    {
        var mapper = new SourcePathMapper(new SourceMappingContext(@"C:\repo\docs", IsSandbox: false, ImmutableHashSet<TypstPath>.Empty));
        await Assert.That(mapper.Scrub(@"\\?\C:\repo\docs\spec.typ")).IsEqualTo(@"\\?\C:\repo\docs\spec.typ");
    }
}
