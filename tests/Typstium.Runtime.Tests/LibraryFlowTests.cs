using Typstium.Runtime.Testing;

namespace Typstium.Runtime.Tests;

/// <summary>
/// An app that gets Typstium through a library builds the library without a RID, so the library selects every runtime it
/// references. The app's own selection and switches decide what it ships, not the library's (§6).
/// </summary>
public class LibraryFlowTests
{
    private static readonly string Output = Path.Combine("bin", "Release", "net10.0");

    private static async Task<(SampleProject Library, SampleProject App)> AppWithLibraryAsync(Action<SampleProject>? configureApp = null)
    {
        var library = await SampleProject.CreateAsync(name: "Lib", folder: "Lib", runtimes: ["linux-x64", "win-x64"]);
        library.OutputType = "Library";
        library.Write();
        var app = await SampleProject.CreateAsync();
        app.ReferenceTypstium = false;
        app.ExtraItems.Add($"""<ProjectReference Include="{Path.Combine(library.Directory, "Lib.csproj")}" />""");
        configureApp?.Invoke(app);
        app.Write();
        return (library, app);
    }

    [Test]
    [Property(Scenario.Key, "RS25")]
    [Property(Scenario.Key, "RS3")]
    public async Task A_rid_specific_publish_ships_only_the_apps_runtime_not_every_runtime_the_library_selected()
    {
        var (library, app) = await AppWithLibraryAsync();
        using var _ = library;
        using var __ = app;

        var publish = await app.RunAsync("publish", "-c", "Release", "-r", "linux-x64", "-o", "pub");

        await Assert.That(publish.ExitCode).IsEqualTo(0).Because(publish.Output);
        await Assert.That(app.TypstFiles("pub")).IsEquivalentTo(new[] { "typst/linux-x64/typst", "typstium.runtime.json" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        // The library's other runtime brings no license files either.
        await Assert.That(Directory.Exists(Path.Combine(app.Directory, "pub", "typst", "win-x64"))).IsFalse();
    }

    [Test]
    [Property(Scenario.Key, "RS25")]
    [Property(Scenario.Key, "RS13")]
    public async Task Turning_the_copy_off_in_the_app_ships_no_runtime_from_the_library()
    {
        var (library, app) = await AppWithLibraryAsync(app => app.Properties["TypstiumCopyRuntime"] = "false");
        using var _ = library;
        using var __ = app;

        // No -o: it would send the library's own build output to the same folder.
        var build = await app.RunAsync("build", "-c", "Release");
        var publish = await app.RunAsync("publish", "-c", "Release", "-o", "pub");

        await Assert.That(build.ExitCode).IsEqualTo(0).Because(build.Output);
        await Assert.That(publish.ExitCode).IsEqualTo(0).Because(publish.Output);
        await Assert.That(app.TypstFiles(Output)).IsEmpty();
        await Assert.That(app.TypstFiles("pub")).IsEmpty();
        await Assert.That(Directory.Exists(Path.Combine(app.Directory, Output, "typst"))).IsFalse();
        await Assert.That(Directory.Exists(Path.Combine(app.Directory, "pub", "typst"))).IsFalse();
    }

    [Test]
    [Property(Scenario.Key, "RS25")]
    [Property(Scenario.Key, "RS31")]
    public async Task The_apps_runtime_directory_wins_over_the_librarys()
    {
        var (library, app) = await AppWithLibraryAsync(app => app.Properties["TypstiumRuntimeDirectory"] = "tools/{rid}");
        using var _ = library;
        using var __ = app;

        // No -o: it would send the library's own build output to the same folder.
        var build = await app.RunAsync("build", "-c", "Release");
        var publish = await app.RunAsync("publish", "-c", "Release", "-o", "pub");

        await Assert.That(build.ExitCode).IsEqualTo(0).Because(build.Output);
        await Assert.That(publish.ExitCode).IsEqualTo(0).Because(publish.Output);
        var expected = new[] { "tools/linux-x64/typst", "tools/win-x64/typst.exe", "typstium.runtime.json" };
        await Assert.That(app.TypstFiles(Output)).IsEquivalentTo(expected, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(app.TypstFiles("pub")).IsEquivalentTo(expected, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        // Nothing lands in the library's default typst/<rid>/ folders, license files included.
        await Assert.That(Directory.Exists(Path.Combine(app.Directory, Output, "typst"))).IsFalse();
        await Assert.That(Directory.Exists(Path.Combine(app.Directory, "pub", "typst"))).IsFalse();
    }
}
