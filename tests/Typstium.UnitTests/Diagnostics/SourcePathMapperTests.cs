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

    [Test]
    public async Task Locations_keep_line_and_columns()
    {
        var location = Sandbox(UnixRoot).Map(new RawLocation("/tmp/typstium/ws-1/root/a.typ", 3, 2, 16));
        await Assert.That(location!.Start).IsEqualTo(new TypstPosition(3, 2));
        await Assert.That(location.End).IsEqualTo(new TypstPosition(3, 16));
    }
}
