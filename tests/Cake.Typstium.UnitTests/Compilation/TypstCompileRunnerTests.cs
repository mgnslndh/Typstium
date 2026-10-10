using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Testing;
using Cake.Typstium.Compilation;
using Cake.Typstium.UnitTests.Testing;
using Typstium;
using Typstium.Testing.Fixtures;

namespace Cake.Typstium.UnitTests.Compilation;

public class TypstCompileRunnerTests
{
    private sealed class Fixture : IDisposable
    {
        public TemporaryDirectory Work { get; } = new("cake-runner");
        public FakeLog Log { get; } = new() { Verbosity = Verbosity.Diagnostic };
        public FilePath? ToolPathSeen { get; private set; }
        public bool CompilerCreated { get; private set; }

        public TypstCompileRunner Runner(ITypstCompiler compiler) =>
            new(CakeFakes.Environment(Work.Path), Log, toolPath => { CompilerCreated = true; ToolPathSeen = toolPath; return compiler; });

        public void Dispose() => Work.Dispose();
    }

    [Test]
    public async Task Writes_the_pdf_beside_the_input_by_default_and_logs_one_line()
    {
        using var fixture = new Fixture();
        var compiler = FixedResultCompiler.Pdf();

        var result = await fixture.Runner(compiler).RunAsync(new FilePath("docs/spec.typ"), output: null, new TypstCompileSettings(), CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(File.ReadAllText(fixture.Work.Combine("docs", "spec.pdf"))).IsEqualTo("%PDF-1.7");
        await Assert.That(compiler.LastRequest!.Source).IsEqualTo(TypstCompileRequest.ForFile(fixture.Work.Combine("docs", "spec.typ")).Build().Source);
        var information = fixture.Log.Entries.Single(entry => entry.Level == LogLevel.Information);
        await Assert.That(information.Message).Contains("spec.pdf");
    }

    [Test]
    public async Task Creates_a_missing_output_folder()
    {
        using var fixture = new Fixture();

        await fixture.Runner(FixedResultCompiler.Pdf()).RunAsync(new FilePath("spec.typ"), new FilePath("artifacts/docs/spec.pdf"), new TypstCompileSettings(), CancellationToken.None);

        await Assert.That(File.Exists(fixture.Work.Combine("artifacts", "docs", "spec.pdf"))).IsTrue();
    }

    [Test]
    public async Task Saves_every_page_through_the_pattern()
    {
        using var fixture = new Fixture();

        await fixture.Runner(FixedResultCompiler.Pages(TypstOutputFormat.Png, 3))
            .RunAsync(new FilePath("spec.typ"), new FilePath("out/page-{0p}.png"), new TypstCompileSettings(), CancellationToken.None);

        await Assert.That(File.Exists(fixture.Work.Combine("out", "page-1.png"))).IsTrue();
        await Assert.That(File.Exists(fixture.Work.Combine("out", "page-3.png"))).IsTrue();
    }

    [Test]
    public async Task Several_pages_without_a_pattern_are_refused_before_anything_is_written()
    {
        using var fixture = new Fixture();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            fixture.Runner(FixedResultCompiler.Pages(TypstOutputFormat.Png, 3))
                .RunAsync(new FilePath("spec.typ"), new FilePath("out/page.png"), new TypstCompileSettings(), CancellationToken.None));

        await Assert.That(exception!.Message).Contains("3 pages");
        await Assert.That(Directory.Exists(fixture.Work.Combine("out"))).IsFalse();
    }

    [Test]
    [Arguments(TypstCompileStatus.Failed)]
    [Arguments(TypstCompileStatus.TimedOut)]
    [Arguments(TypstCompileStatus.OutputLimitExceeded)]
    public async Task An_unsuccessful_compile_logs_its_diagnostics_then_throws_a_cake_exception(TypstCompileStatus status)
    {
        using var fixture = new Fixture();
        var error = Diagnostics.Error("unknown variable: x");

        var exception = await Assert.ThrowsAsync<CakeException>(() =>
            fixture.Runner(FixedResultCompiler.Unsuccessful(status, error))
                .RunAsync(new FilePath("spec.typ"), output: null, new TypstCompileSettings(), CancellationToken.None));

        await Assert.That(exception!.Message).Contains("spec.typ");
        await Assert.That(exception.Message).Contains(status.ToString());
        await Assert.That(exception.Message).Contains("1 error");
        await Assert.That(fixture.Log.Entries.Any(entry => entry.Level == LogLevel.Error && entry.Message == error.ToString())).IsTrue();
        await Assert.That(File.Exists(fixture.Work.Combine("spec.pdf"))).IsFalse();
    }

    [Test]
    public async Task Warnings_are_logged_and_pass_by_default()
    {
        using var fixture = new Fixture();
        var warning = Diagnostics.Warning("unknown font family: foo");

        var result = await fixture.Runner(FixedResultCompiler.Pdf(warning)).RunAsync(new FilePath("spec.typ"), output: null, new TypstCompileSettings(), CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(fixture.Log.Entries.Any(entry => entry.Level == LogLevel.Warning && entry.Message == warning.ToString())).IsTrue();
    }

    [Test]
    public async Task Warnings_fail_the_task_when_treated_as_errors()
    {
        using var fixture = new Fixture();

        var exception = await Assert.ThrowsAsync<CakeException>(() =>
            fixture.Runner(FixedResultCompiler.Pdf(Diagnostics.Warning("unknown font family: foo")))
                .RunAsync(new FilePath("spec.typ"), output: null, new TypstCompileSettings().WithWarningsAsErrors(), CancellationToken.None));

        await Assert.That(exception!.Message).Contains("1 warning");
        await Assert.That(File.Exists(fixture.Work.Combine("spec.pdf"))).IsFalse();
    }

    [Test]
    public async Task An_unknown_extension_fails_before_the_compiler_is_created()
    {
        using var fixture = new Fixture();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            fixture.Runner(FixedResultCompiler.Pdf()).RunAsync(new FilePath("spec.typ"), new FilePath("spec.docx"), new TypstCompileSettings(), CancellationToken.None));

        await Assert.That(fixture.CompilerCreated).IsFalse();
    }

    [Test]
    public async Task The_tool_path_setting_reaches_the_compiler_factory()
    {
        using var fixture = new Fixture();

        await fixture.Runner(FixedResultCompiler.Pdf())
            .RunAsync(new FilePath("spec.typ"), output: null, new TypstCompileSettings().WithToolPath(new FilePath("bin/typst")), CancellationToken.None);

        await Assert.That(fixture.ToolPathSeen!.FullPath).IsEqualTo("bin/typst");
    }

    [Test]
    public async Task Core_exceptions_pass_through_unwrapped()
    {
        using var fixture = new Fixture();
        var notFound = new TypstExecutableNotFoundException([]);
        var runner = new TypstCompileRunner(CakeFakes.Environment(fixture.Work.Path), fixture.Log, _ => new ThrowingCompiler(notFound));

        var exception = await Assert.ThrowsAsync<TypstExecutableNotFoundException>(() =>
            runner.RunAsync(new FilePath("spec.typ"), output: null, new TypstCompileSettings(), CancellationToken.None));

        await Assert.That(exception).IsSameReferenceAs(notFound);
    }

    private sealed class ThrowingCompiler(Exception exception) : ITypstCompiler
    {
        public Task<TypstCompileResult> CompileAsync(TypstCompileRequest request, CancellationToken cancellationToken = default) =>
            Task.FromException<TypstCompileResult>(exception);
    }
}
