using Typstium.Testing;
using Typstium.Testing.Fakes;
using Typstium.Testing.Fixtures;
using Typstium.Testing.Typst;

namespace Typstium.IntegrationTests;

public class DiagnosticsTests
{
    private static string FixtureDirectory => Path.Combine(RepositoryPaths.Fixtures, "typst-0.15", "diagnostics");

    [Test]
    [Arguments("01-syntax", "TYPST1007")]
    [Arguments("02-unknown-variable", "TYPST1001")]
    [Arguments("03-hint", "TYPST1001")]
    [Arguments("04-outside-root", "TYPST1003")]
    [Arguments("06-error-in-import", "TYPST0001")]
    [Arguments("07-infinite-loop", "TYPST1008")]
    [Arguments("08-recursion", "TYPST1008")]
    [Arguments("09-missing-package", "TYPST1005")]
    [Arguments("10-multiple-errors", "TYPST0001")]
    [Arguments("11-bad-image", "TYPST1006")]
    public async Task In_place_failures_carry_ids_and_full_paths(string name, string expectedId)
    {
        using var temp = new TemporaryDirectory("it-diagnostics");
        var compiler = await TypstAvailability.CreateCompilerAsync(CliTypstCompilerSettings.Default with { TempRoot = temp.Path });

        var result = await compiler.CompileAsync(TypstSource.File(Path.Combine(FixtureDirectory, name + ".typ"), FixtureDirectory));

        await Assert.That(result.Status).IsEqualTo(TypstCompileStatus.Failed);
        var first = result.Errors.First();
        await Assert.That(first.Id.Value).IsEqualTo(expectedId);
        var file = first.Location!.File as TypstSourceFile.Project;
        await Assert.That(file).IsNotNull();
        await Assert.That(File.Exists(file!.FullPath)).IsTrue();
    }

    [Test]
    public async Task Warnings_do_not_fail_the_compile_and_get_the_system_fonts_hint()
    {
        using var temp = new TemporaryDirectory("it-diagnostics");
        var compiler = await TypstAvailability.CreateCompilerAsync(CliTypstCompilerSettings.Default with { TempRoot = temp.Path });

        var result = await compiler.CompileAsync(TypstSource.File(Path.Combine(FixtureDirectory, "05-warning-font.typ")));

        await Assert.That(result.Status).IsEqualTo(TypstCompileStatus.Succeeded);
        var warning = result.Warnings.Single();
        await Assert.That(warning.Id.Value).IsEqualTo("TYPST2001");
        await Assert.That(warning.Hints.Any(hint => hint.Contains("system fonts are disabled"))).IsTrue();
    }

    [Test]
    public async Task Missing_packages_with_a_blocked_network_get_the_network_hint()
    {
        using var temp = new TemporaryDirectory("it-diagnostics");
        var compiler = await TypstAvailability.CreateCompilerAsync(CliTypstCompilerSettings.Default with { TempRoot = temp.Path });
        var options = TypstCompileOptions.Default with
        {
            Packages = TypstPackageOptions.Default with { PackagePath = Path.Combine(temp.Path, "empty-path"), PackageCachePath = Path.Combine(temp.Path, "empty-cache") },
        };

        var result = await compiler.CompileMarkupAsync("#import \"@preview/definitely-not-a-real-package:9.9.9\": *", TypstOutput.Pdf(), options);

        var error = result.Errors.Single();
        await Assert.That(error.Id.Value).IsEqualTo("TYPST1005");
        await Assert.That(error.Hints.Any(hint => hint.Contains("network access is blocked"))).IsTrue();
        await Assert.That(error.Location!.File).IsEqualTo((TypstSourceFile)TypstSourceFile.Markup.Instance);
    }

    [Test]
    public async Task Sandboxed_template_diagnostics_never_mention_the_sandbox()
    {
        using var temp = new TemporaryDirectory("it-diagnostics");
        var compiler = await TypstAvailability.CreateCompilerAsync(CliTypstCompilerSettings.Default with { TempRoot = temp.Path });
        var templates = new InMemoryTemplateProvider().Add(
            "imports",
            "main.typ",
            ("main.typ", "#import \"sub/helper.typ\": greet\n#greet(42)\n"),
            ("sub/helper.typ", "#let greet(name) = {\n  \"Hello \" + name\n}\n"));

        var result = await compiler.CompileTemplateAsync(templates, "imports");

        var error = result.Errors.Single();
        await Assert.That(error.Location!.File).IsEqualTo((TypstSourceFile)new TypstSourceFile.Project(TypstPath.Parse("sub/helper.typ"), null));
        await Assert.That(error.Trace.Single().Location!.File).IsEqualTo((TypstSourceFile)new TypstSourceFile.Project(TypstPath.Parse("main.typ"), null));
        await Assert.That(error.ToString()).DoesNotContain(temp.Path);
        await Assert.That(error.ToString()).DoesNotContain("ws-");
    }
}
