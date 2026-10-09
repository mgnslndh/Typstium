using Typstium.Testing.Fakes;
using Typstium.Testing.Fixtures;
using Typstium.Testing.Typst;

namespace Typstium.IntegrationTests;

public class PathsTests
{
    [Test]
    public async Task Temp_root_with_spaces_and_non_ascii_characters_works_and_maps_diagnostics()
    {
        using var temp = new TemporaryDirectory("it-paths");
        var tempRoot = temp.Combine("Åsa Ström", "typstium");
        var compiler = await TypstAvailability.CreateCompilerAsync(CliTypstCompilerSettings.Default with { TempRoot = tempRoot });
        var templates = new InMemoryTemplateProvider()
            .Add("ok", "ok.typ", ("ok.typ", "= Fine"))
            .Add("broken", "broken.typ", ("broken.typ", "#nope"));

        var ok = await compiler.CompileTemplateAsync(templates, "ok");
        var broken = await compiler.CompileTemplateAsync(templates, "broken");

        await Assert.That(ok.IsSuccess).IsTrue();
        var error = broken.Errors.Single();
        await Assert.That(error.Location!.File).IsEqualTo((TypstSourceFile)new TypstSourceFile.Project(TypstPath.Parse("broken.typ"), null));
        await Assert.That(error.ToString()).DoesNotContain("Åsa Ström");
    }

    // A temp root on a network share, through this machine's admin share (\\localhost\C$\...). Typst prints such
    // paths as \\?\UNC\localhost\C$\...; diagnostics must still map to template files and never show the sandbox.
    [Test]
    public async Task Temp_root_on_a_unc_share_maps_diagnostics_and_hides_the_sandbox()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test("UNC paths are Windows paths.");
        }

        using var temp = new TemporaryDirectory("it-unc");
        var local = temp.Combine("typstium");
        var tempRoot = @"\\localhost\" + local.Substring(0, 1) + "$" + local.Substring(2);
        try
        {
            Directory.CreateDirectory(tempRoot);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Skip.Test($"The admin share isn't reachable here: {exception.Message}");
        }

        var compiler = await TypstAvailability.CreateCompilerAsync(CliTypstCompilerSettings.Default with { TempRoot = tempRoot });
        var templates = new InMemoryTemplateProvider().Add("broken", "broken.typ", ("broken.typ", "#image(\"missing.png\")"));

        var broken = await compiler.CompileTemplateAsync(templates, "broken");

        var error = broken.Errors.Single();
        await Assert.That(error.Location!.File).IsEqualTo((TypstSourceFile)new TypstSourceFile.Project(TypstPath.Parse("broken.typ"), null));
        await Assert.That(error.Message).Contains("/missing.png");
        foreach (var text in new[] { error.ToString(), error.Message }.Concat(error.Hints))
        {
            await Assert.That(text).DoesNotContain("localhost");
            await Assert.That(text).DoesNotContain("ws-");
        }
    }
}
