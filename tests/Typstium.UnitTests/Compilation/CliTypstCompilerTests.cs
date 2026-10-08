using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Typstium.CommandLine;
using Typstium.Processes;
using Typstium.Testing.Fakes;
using Typstium.Testing.Fixtures;
using Typstium.UnitTests.Testing;
using Typstium.Workspace;
using TUnit.Assertions.Enums;

namespace Typstium.UnitTests.Compilation;

public class CliTypstCompilerTests
{
    private static readonly AsyncLocal<bool> ActivityTestScope = new();

    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.7 fake");

    private sealed class Harness : IDisposable
    {
        public Harness(Func<ProcessSpec, ProcessOutcome> compile, bool html = true, TimeSpan delay = default, int maxConcurrency = 4, TypstProcessPriority processPriority = TypstProcessPriority.Normal)
        {
            Temp = new TemporaryDirectory("compiler");
            Runner = new FakeProcessRunner(spec => FakeProcessRunner.IsInfo(spec)
                ? FakeProcessRunner.Exited(0, FakeProcessRunner.InfoJson("0.15.1", html))
                : compile(spec)) { Delay = delay };
            TempRoot = Temp.Combine("tmp");
            var settings = CliTypstCompilerSettings.Default with { TempRoot = TempRoot, MaxConcurrency = maxConcurrency, ProcessPriority = processPriority };
            Compiler = new CliTypstCompiler(new FixedExecutableResolver(Temp.Combine("typst.exe")), settings, Runner);
        }

        public TemporaryDirectory Temp { get; }

        public FakeProcessRunner Runner { get; }

        public string TempRoot { get; }

        public CliTypstCompiler Compiler { get; }

        public IEnumerable<ProcessSpec> CompileSpecs => Runner.Specs.Where(spec => !FakeProcessRunner.IsInfo(spec));

        public bool NoWorkspacesLeft => !Directory.Exists(TempRoot) || Directory.GetDirectories(TempRoot, ScratchDirectory.Prefix + "*").Length == 0;

        public void Dispose() => Temp.Dispose();
    }

    /// <summary>The root as Typst prints it: \\?\-prefixed on Windows.</summary>
    private static string PrintedRoot(ProcessSpec spec) =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"\\?\" + spec.WorkingDirectory : spec.WorkingDirectory;

    [Test]
    [Arguments(null, TypstProcessPriority.BelowNormal)]
    [Arguments(TypstProcessPriority.Idle, TypstProcessPriority.Idle)]
    [Arguments(TypstProcessPriority.Normal, TypstProcessPriority.Normal)]
    public async Task A_request_priority_overrides_the_compiler_priority(TypstProcessPriority? requested, TypstProcessPriority expected)
    {
        using var harness = new Harness(_ => FakeProcessRunner.Exited(0, Encoding.ASCII.GetString(Pdf)), processPriority: TypstProcessPriority.BelowNormal);
        var request = TypstCompileRequest.ForMarkup("= Hi").ConfigureOptions(o => o with { ProcessPriority = requested }).Build();

        await harness.Compiler.CompileAsync(request);

        await Assert.That(harness.CompileSpecs.Single().Priority).IsEqualTo(expected);
        await Assert.That(harness.Runner.Specs.Where(FakeProcessRunner.IsInfo).All(spec => spec.Priority == TypstProcessPriority.BelowNormal)).IsTrue();
    }

    // Workspaces are prepared inside the concurrency slot, so queued requests hold no scratch folders.
    [Test]
    public async Task Queued_requests_prepare_no_workspace_until_they_get_a_slot()
    {
        var most = 0;
        Harness? harness = null;
        harness = new Harness(
            _ =>
            {
                var count = Directory.GetDirectories(harness!.TempRoot, ScratchDirectory.Prefix + "*").Length;
                InterlockedMax(ref most, count);
                return FakeProcessRunner.Exited(0, Encoding.ASCII.GetString(Pdf));
            },
            delay: TimeSpan.FromMilliseconds(50),
            maxConcurrency: 1);
        using (harness)
        {
            await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => harness.Compiler.CompileAsync(TypstCompileRequest.ForMarkup("= Hi").Build())));

            await Assert.That(most).IsEqualTo(1);
            await Assert.That(harness.NoWorkspacesLeft).IsTrue();
        }
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while ((current = Volatile.Read(ref target)) < value && Interlocked.CompareExchange(ref target, value, current) != current)
        {
        }
    }

    [Test]
    public async Task Successful_pdf_compile()
    {
        using var harness = new Harness(_ => FakeProcessRunner.Exited(0, Encoding.ASCII.GetString(Pdf)));
        var request = TypstCompileRequest.ForMarkup("= Hi").WithInput("secret", "customer-data").Build();

        var result = await harness.Compiler.CompileAsync(request);

        await Assert.That(result.Status).IsEqualTo(TypstCompileStatus.Succeeded);
        await Assert.That(((TypstDocument.SingleFile)result.Document!).Content.ToArray()).IsEquivalentTo(Pdf);
        await Assert.That(result.Invocation.ExecutableFoundBy).IsEqualTo("Fixed");
        await Assert.That(result.Invocation.TypstVersion).IsEqualTo(new TypstVersion(0, 15, 1));
        await Assert.That(result.Invocation.CommandLine).Contains("compile");
        await Assert.That(result.Invocation.CommandLine).DoesNotContain("customer-data");
        await Assert.That(harness.NoWorkspacesLeft).IsTrue();
    }

    [Test]
    public async Task Markup_goes_over_stdin_with_a_blocked_network()
    {
        using var harness = new Harness(_ => FakeProcessRunner.Exited(0, "%PDF"));
        await harness.Compiler.CompileMarkupAsync("= Hi");

        var spec = harness.CompileSpecs.Single();
        await Assert.That(Encoding.UTF8.GetString(spec.Stdin.ToArray())).IsEqualTo("= Hi");
        await Assert.That(spec.Environment.Set["HTTPS_PROXY"]).IsEqualTo(ProcessEnvironmentBuilder.DeadProxy);
        await Assert.That(spec.Timeout).IsEqualTo(TimeSpan.FromSeconds(60));
    }

    [Test]
    public async Task Template_failures_become_results_with_mapped_diagnostics()
    {
        var fixture = DiagnosticFixtures.Get(new TypstVersion(0, 15, 0), "02-unknown-variable");
        using var harness = new Harness(spec => FakeProcessRunner.Exited(1, stderr: fixture.StandardErrorFor(PrintedRoot(spec))));
        var templates = new InMemoryTemplateProvider().Add("broken", "02-unknown-variable.typ", ("02-unknown-variable.typ", "#undefined-thing"));

        var result = await harness.Compiler.CompileTemplateAsync(templates, "broken");

        await Assert.That(result.Status).IsEqualTo(TypstCompileStatus.Failed);
        var diagnostic = result.Diagnostics.Single();
        await Assert.That(diagnostic.Id.Value).IsEqualTo("TYPST1001");
        await Assert.That(diagnostic.Location!.File).IsEqualTo((TypstSourceFile)new TypstSourceFile.Project(TypstPath.Parse("02-unknown-variable.typ"), null));
        await Assert.That(diagnostic.ToString()).DoesNotContain(harness.TempRoot);
    }

    [Test]
    public async Task Exit_code_1_without_diagnostics_still_explains_itself()
    {
        using var harness = new Harness(_ => FakeProcessRunner.Exited(1));
        var result = await harness.Compiler.CompileMarkupAsync("x");
        await Assert.That(result.Status).IsEqualTo(TypstCompileStatus.Failed);
        await Assert.That(result.Errors.Single().Id.Value).IsEqualTo("TYPST0001");
    }

    [Test]
    public async Task Exit_code_2_is_a_typstium_bug()
    {
        using var harness = new Harness(_ => FakeProcessRunner.Exited(2, stderr: "error: unexpected argument '--bogus'"));
        var request = TypstCompileRequest.ForMarkup("x").WithInput("k", "secret-value").Build();
        var exception = await Assert.ThrowsAsync<TypstInvocationException>(() => harness.Compiler.CompileAsync(request));
        await Assert.That(exception!.CommandLine).DoesNotContain("secret-value");
        await Assert.That(harness.NoWorkspacesLeft).IsTrue();
    }

    [Test]
    public async Task Other_exit_codes_are_crashes()
    {
        using var harness = new Harness(_ => FakeProcessRunner.Exited(101, stderr: "thread 'main' panicked"));
        var exception = await Assert.ThrowsAsync<TypstProcessCrashedException>(() => harness.Compiler.CompileMarkupAsync("x"));
        await Assert.That(exception!.ExitCode).IsEqualTo(101);
    }

    [Test]
    public async Task Timeouts_and_output_limits_are_statuses()
    {
        using var timedOut = new Harness(_ => new ProcessOutcome.TimedOut(TimeSpan.FromSeconds(60), string.Empty));
        using var tooBig = new Harness(_ => new ProcessOutcome.OutputLimitExceeded(1024, TimeSpan.FromSeconds(1), string.Empty));

        var timeoutResult = await timedOut.Compiler.CompileMarkupAsync("x");
        var limitResult = await tooBig.Compiler.CompileMarkupAsync("x");

        await Assert.That(timeoutResult.Status).IsEqualTo(TypstCompileStatus.TimedOut);
        await Assert.That(timeoutResult.Diagnostics.Last().Id.Value).IsEqualTo("TYPST9002");
        await Assert.That(limitResult.Status).IsEqualTo(TypstCompileStatus.OutputLimitExceeded);
        await Assert.That(limitResult.Diagnostics.Last().Id.Value).IsEqualTo("TYPST9001");
    }

    [Test]
    public async Task Success_without_output_is_unreadable_output()
    {
        using var harness = new Harness(_ => FakeProcessRunner.Exited(0));
        await Assert.ThrowsAsync<TypstProcessOutputException>(() => harness.Compiler.CompileMarkupAsync("x"));
    }

    [Test]
    public async Task Png_pages_are_collected_in_order()
    {
        using var harness = new Harness(spec =>
        {
            var pattern = spec.Arguments.ToValues().Single(value => value.IndexOf("{0p}", StringComparison.Ordinal) >= 0);
            File.WriteAllBytes(pattern.Replace("{0p}", "2"), new byte[] { 2 });
            File.WriteAllBytes(pattern.Replace("{0p}", "1"), new byte[] { 1 });
            return FakeProcessRunner.Exited(0);
        });

        var result = await harness.Compiler.CompileMarkupAsync("x", TypstOutput.Png());

        var pages = ((TypstDocument.Paged)result.Document!).Pages;
        await Assert.That(pages.Select(page => page.Span[0])).IsEquivalentTo(new byte[] { 1, 2 }, CollectionOrdering.Matching);
    }

    [Test]
    public async Task Paged_output_over_the_limit_is_a_status_without_reading_pages()
    {
        using var harness = new Harness(spec =>
        {
            var pattern = spec.Arguments.ToValues().Single(value => value.IndexOf("{0p}", StringComparison.Ordinal) >= 0);
            File.WriteAllBytes(pattern.Replace("{0p}", "1"), new byte[600]);
            File.WriteAllBytes(pattern.Replace("{0p}", "2"), new byte[600]);
            return FakeProcessRunner.Exited(0);
        });
        var options = TypstCompileOptions.Default with { MaxOutputBytes = 1000 };

        var result = await harness.Compiler.CompileMarkupAsync("x", TypstOutput.Png(), options);

        await Assert.That(result.Status).IsEqualTo(TypstCompileStatus.OutputLimitExceeded);
        await Assert.That(result.Document).IsNull();
        await Assert.That(result.Diagnostics.Last().Id.Value).IsEqualTo("TYPST9001");
        await Assert.That(harness.NoWorkspacesLeft).IsTrue();
    }

    [Test]
    public async Task Pdf_standards_unknown_to_typstium_are_passed_through_to_typst()
    {
        using var harness = new Harness(_ => FakeProcessRunner.Exited(0, "%PDF"));
        var output = TypstOutput.Pdf(PdfStandard.FromCliValue("a-5"));

        var result = await harness.Compiler.CompileMarkupAsync("x", output);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(harness.CompileSpecs.Single().Arguments.ToValues()).Contains("a-5");
    }

    [Test]
    public async Task Known_pdf_standards_the_profile_lacks_are_refused_before_typst_runs()
    {
        var capabilities = TestCapabilities.V015 with
        {
            PdfStandards = ImmutableHashSet.Create(PdfStandard.Pdf17),
        };
        var request = TypstCompileRequest.ForMarkup("x").WithOutput(TypstOutput.Pdf(PdfStandard.A2b)).Build();

        var exception = Assert.Throws<TypstFeatureNotSupportedException>(() => Typstium.Compilation.CapabilityValidator.Validate(request, capabilities));
        await Assert.That(exception!.Message).Contains("a-2b");
    }

    [Test]
    public async Task Html_needs_the_html_capability()
    {
        using var harness = new Harness(_ => FakeProcessRunner.Exited(0, "<html/>"), html: false);
        await Assert.ThrowsAsync<TypstFeatureNotSupportedException>(() => harness.Compiler.CompileMarkupAsync("x", TypstOutput.Html()));
        await Assert.That(harness.CompileSpecs.Count()).IsEqualTo(0);
    }

    [Test]
    public async Task Inputs_over_the_command_line_limit_fail_before_typst_runs()
    {
        using var harness = new Harness(_ => FakeProcessRunner.Exited(0, "%PDF"));
        var request = TypstCompileRequest.ForMarkup("x").WithInput("data", new string('x', 40_000)).Build();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => harness.Compiler.CompileAsync(request));

        await Assert.That(exception!.Message).Contains("virtual file");
        await Assert.That(harness.CompileSpecs.Count()).IsEqualTo(0);
        await Assert.That(harness.NoWorkspacesLeft).IsTrue();
    }

    [Test]
    public async Task Parallel_cold_start_probes_once_and_respects_max_concurrency()
    {
        using var harness = new Harness(_ => FakeProcessRunner.Exited(0, "%PDF"), delay: TimeSpan.FromMilliseconds(30), maxConcurrency: 3);

        var results = await Task.WhenAll(Enumerable.Range(0, 30).Select(i => Task.Run(() => harness.Compiler.CompileMarkupAsync("= " + i))));

        await Assert.That(results.All(result => result.IsSuccess)).IsTrue();
        await Assert.That(harness.Runner.Specs.Count(FakeProcessRunner.IsInfo)).IsEqualTo(1);
        await Assert.That(harness.Runner.MaxObservedConcurrency).IsLessThanOrEqualTo(3);
        await Assert.That(harness.NoWorkspacesLeft).IsTrue();
    }

    [Test]
    public async Task Cancellation_propagates_and_cleans_up()
    {
        using var harness = new Harness(_ => FakeProcessRunner.Exited(0, "%PDF"), delay: TimeSpan.FromSeconds(30));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAsync<OperationCanceledException>(() => harness.Compiler.CompileMarkupAsync("x", cancellation.Token));
        await Assert.That(harness.NoWorkspacesLeft).IsTrue();
    }

    [Test]
    public async Task Each_compile_emits_an_activity()
    {
        var stopped = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Typstium",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                // Parallel tests also emit activities; the async-local flag keeps only the ones started from this test's flow.
                if (ActivityTestScope.Value)
                {
                    lock (stopped) { stopped.Add(activity); }
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        ActivityTestScope.Value = true;
        using var harness = new Harness(_ => FakeProcessRunner.Exited(0, "%PDF"));

        await harness.Compiler.CompileMarkupAsync("x");

        Activity activity;
        lock (stopped)
        {
            activity = stopped.Single(a => a.OperationName == "typst.compile" && (string?)a.GetTagItem("typst.source.kind") == "markup");
        }

        await Assert.That((string?)activity.GetTagItem("typst.status")).IsEqualTo("Succeeded");
    }
}
