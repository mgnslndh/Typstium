using TUnit.Assertions.Enums;
using Typstium.Diagnostics;
using Typstium.Testing.Fixtures;

namespace Typstium.UnitTests.Diagnostics;

public class HumanDiagnosticParserV015Tests
{
    private static readonly TypstVersion V015 = new(0, 15, 0);
    private readonly HumanDiagnosticParserV015 _parser = new();

    [Test]
    [Arguments("02-unknown-variable", "unknown variable: undefined-thing", 3, 2, 16, 1, 0)]
    [Arguments("03-hint", "unknown variable: a-b", 3, 3, 5, 1, 0)]
    [Arguments("04-outside-root", "path `\"../secret.txt\"` would escape the project root", 1, 7, 21, 2, 0)]
    [Arguments("06-error-in-import", "cannot add string and integer", 2, 3, 17, 0, 1)]
    [Arguments("07-infinite-loop", "condition is always true", 1, 8, 11, 0, 0)]
    [Arguments("08-recursion", "maximum function call depth exceeded", 1, 13, 20, 0, 1)]
    [Arguments("10-multiple-errors", "expected color, gradient, or tiling, found integer", 1, 13, 13, 0, 0)]
    [Arguments("11-bad-image", "failed to decode image (Format error decoding Png: Invalid PNG signature.)", 1, 13, 28, 0, 0)]
    public async Task Parses_single_error_fixtures(string name, string message, int line, int column, int endColumn, int hints, int traces)
    {
        var fixture = DiagnosticFixtures.Get(V015, name);
        var diagnostics = _parser.Parse(fixture.StandardErrorTemplate);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
        var diagnostic = diagnostics[0];
        await Assert.That(diagnostic.IsRecognized).IsTrue();
        await Assert.That(diagnostic.Severity).IsEqualTo(TypstDiagnosticSeverity.Error);
        await Assert.That(diagnostic.Message).IsEqualTo(message);
        await Assert.That(diagnostic.Location!.Line).IsEqualTo(line);
        await Assert.That(diagnostic.Location.Column).IsEqualTo(column);
        await Assert.That(diagnostic.Location.EndColumn).IsEqualTo(endColumn);
        await Assert.That(diagnostic.Hints.Length).IsEqualTo(hints);
        await Assert.That(diagnostic.Trace.Length).IsEqualTo(traces);
    }

    [Test]
    public async Task Syntax_errors_produce_several_diagnostics()
    {
        var diagnostics = _parser.Parse(DiagnosticFixtures.Get(V015, "01-syntax").StandardErrorTemplate);
        await Assert.That(diagnostics.Select(d => d.Message)).IsEquivalentTo(new[] { "unclosed delimiter", "expected comma", "expected comma", "expected identifier" }, CollectionOrdering.Matching);
        await Assert.That(diagnostics[0].Location!.Column).IsEqualTo(10);
    }

    [Test]
    public async Task Warnings_are_recognized()
    {
        var diagnostic = _parser.Parse(DiagnosticFixtures.Get(V015, "05-warning-font").StandardErrorTemplate).Single();
        await Assert.That(diagnostic.Severity).IsEqualTo(TypstDiagnosticSeverity.Warning);
        await Assert.That(diagnostic.Message).IsEqualTo("unknown font family: no such font family");
        await Assert.That(diagnostic.Location!.Column).IsEqualTo(17);
        await Assert.That(diagnostic.Location.EndColumn).IsEqualTo(37);
    }

    [Test]
    public async Task Paths_and_trace_frames_are_kept_as_printed()
    {
        var diagnostic = _parser.Parse(DiagnosticFixtures.Get(V015, "06-error-in-import").StandardErrorTemplate).Single();
        await Assert.That(diagnostic.Location!.Path).IsEqualTo("{ROOT}\\sub\\helper.typ");
        var frame = diagnostic.Trace.Single();
        await Assert.That(frame.Message).IsEqualTo("while calling `greet`");
        await Assert.That(frame.Location!.Path).IsEqualTo("{ROOT}\\06-error-in-import.typ");
        await Assert.That(frame.Location.Line).IsEqualTo(2);
        await Assert.That(frame.Location.Column).IsEqualTo(2);
    }

    [Test]
    [MethodDataSource(typeof(DiagnosticFixtures), nameof(DiagnosticFixtures.All))]
    public async Task Every_fixture_parses_without_unrecognized_blocks(DiagnosticFixture fixture)
    {
        var diagnostics = _parser.Parse(fixture.StandardErrorTemplate);
        await Assert.That(diagnostics.Length).IsGreaterThan(0);
        await Assert.That(diagnostics.All(d => d.IsRecognized)).IsTrue();
    }

    [Test]
    public async Task Empty_and_noise_only_input_gives_no_diagnostics()
    {
        await Assert.That(_parser.Parse(string.Empty).Length).IsEqualTo(0);
        var noise = "downloading @preview/cmarker:0.1.10\n0 B / 139.8 KiB (  0 %),   1.4 MiB/s, ETA: 0 s\n\n";
        await Assert.That(_parser.Parse(noise).Length).IsEqualTo(0);
    }

    [Test]
    public async Task Truncated_blocks_degrade_without_throwing()
    {
        var noCaret = "error: unknown variable: x\n  ┌─ /tmp/a.typ:3:0\n  │\n3 │ #x";
        var cutLocation = "error: boom\n  ┌─ C:\\sand";
        var first = _parser.Parse(noCaret).Single();
        var second = _parser.Parse(cutLocation).Single();

        await Assert.That(first.Location!.Column).IsEqualTo(1);
        await Assert.That(first.Location.EndColumn).IsNull();
        await Assert.That(second.IsRecognized).IsTrue();
        await Assert.That(second.Location).IsNull();
    }

    [Test]
    public async Task Multi_line_spans_have_no_end_column()
    {
        var text = "error: unclosed delimiter\n  ┌─ /tmp/a.typ:1:9\n  │\n1 │ ╭ #let x = (\n2 │ │   1\n  │ ╰─^\n";
        var diagnostic = _parser.Parse(text).Single();
        await Assert.That(diagnostic.Location!.EndColumn).IsNull();
    }

    [Test]
    public async Task Unknown_text_becomes_an_unrecognized_diagnostic_with_the_raw_text()
    {
        var panic = "thread 'main' panicked at src/main.rs:10:5:\nsomething broke";
        var diagnostic = _parser.Parse(panic).Single();
        await Assert.That(diagnostic.IsRecognized).IsFalse();
        await Assert.That(diagnostic.Message).Contains("something broke");
        await Assert.That(diagnostic.RawText).Contains("panicked");
    }

    [Test]
    public async Task Unknown_lines_inside_a_block_are_kept_as_an_extra_diagnostic()
    {
        var text = "error: boom\n  ┌─ /tmp/a.typ:1:0\nan unexpected line\n";
        var diagnostics = _parser.Parse(text);
        await Assert.That(diagnostics.Length).IsEqualTo(2);
        await Assert.That(diagnostics[1].IsRecognized).IsFalse();
        await Assert.That(diagnostics[1].Message).IsEqualTo("an unexpected line");
    }
}
