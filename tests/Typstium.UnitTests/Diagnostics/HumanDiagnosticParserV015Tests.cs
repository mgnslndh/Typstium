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

    [Test]
    [Arguments("  while calling `f` at /tmp/a.typ:2:1", "while calling `f`", "/tmp/a.typ", 2, 2)]
    [Arguments("  while calling `f` at C:\\docs\\a.typ:12:4", "while calling `f`", "C:\\docs\\a.typ", 12, 5)]
    [Arguments("  look at this at /tmp/a.typ:3:0", "look", "this at /tmp/a.typ", 3, 1)]
    [Arguments("\twhile calling `g` at /tmp/x:1:2:3", "while calling `g`", "/tmp/x:1", 2, 4)]
    public async Task Trace_frames_split_at_the_first_separator_that_leaves_a_location(string line, string message, string path, int lineNumber, int column)
    {
        var parsed = HumanDiagnosticParserV015.TryParseTraceFrame(line, out var frameMessage, out var location);

        await Assert.That(parsed).IsTrue();
        await Assert.That(frameMessage).IsEqualTo(message);
        await Assert.That(location!.Path).IsEqualTo(path);
        await Assert.That(location.Line).IsEqualTo(lineNumber);
        await Assert.That(location.Column).IsEqualTo(column);
    }

    [Test]
    [Arguments("while calling `f` at /tmp/a.typ:2:1")]
    [Arguments("  while calling `f` at /tmp/a.typ")]
    [Arguments("  while calling `f` at :2:1")]
    [Arguments("  no separator /tmp/a.typ:2:1")]
    [Arguments("  ")]
    [Arguments("  at /tmp/a.typ:2:1")]
    public async Task Lines_that_are_not_trace_frames_are_rejected(string line)
    {
        await Assert.That(HumanDiagnosticParserV015.TryParseTraceFrame(line, out _, out _)).IsFalse();
    }

    [Test]
    [Arguments("/tmp/a.typ:1:0", "/tmp/a.typ", 1, 1)]
    [Arguments("C:\\a b\\c.typ:7:9", "C:\\a b\\c.typ", 7, 10)]
    [Arguments("a:1:2:3", "a:1", 2, 4)]
    public async Task Locations_parse_from_the_end(string text, string path, int line, int column)
    {
        var location = HumanDiagnosticParserV015.ParseLocation(text);

        await Assert.That(location!.Path).IsEqualTo(path);
        await Assert.That(location.Line).IsEqualTo(line);
        await Assert.That(location.Column).IsEqualTo(column);
    }

    [Test]
    [Arguments(":1:2")]
    [Arguments("a.typ:1")]
    [Arguments("a.typ:x:2")]
    [Arguments("a.typ:1:")]
    [Arguments("a.typ:99999999999:1")]
    public async Task Malformed_locations_are_null(string text)
    {
        await Assert.That(HumanDiagnosticParserV015.ParseLocation(text)).IsNull();
    }

    // Worst cases for the backtracking patterns this parser used to have, at the 1 MB stderr cap. Linear parsing takes
    // milliseconds; the old trace-frame regex took hours. [Timeout] is only a hang guard, not a measured bound.
    [Test]
    [Timeout(30_000)]
    [Arguments("spaces")]
    [Arguments("separators")]
    [Arguments("digits")]
    public async Task Huge_continuation_lines_parse_in_linear_time(string shape, CancellationToken cancellationToken)
    {
        var line = shape switch
        {
            "spaces" => new string(' ', 1_000_000) + "x",
            "separators" => " " + string.Concat(Enumerable.Repeat("a at ", 200_000)) + "b:1",
            _ => " x at " + string.Concat(Enumerable.Repeat("1:", 300_000)) + "x",
        };

        var diagnostics = _parser.Parse("error: assertion failed: first line\n" + line + "\n");

        await Assert.That(diagnostics[0].Message).IsEqualTo("assertion failed: first line");
        await Assert.That(diagnostics[0].Trace.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Hints_notes_and_help_keep_their_kind_except_hints()
    {
        await Assert.That(HumanDiagnosticParserV015.TryParseHint("  = hint: try this", out var hint) ? hint : null).IsEqualTo("try this");
        await Assert.That(HumanDiagnosticParserV015.TryParseHint("= note: see that", out var note) ? note : null).IsEqualTo("note: see that");
        await Assert.That(HumanDiagnosticParserV015.TryParseHint("  = help: ask", out var help) ? help : null).IsEqualTo("help: ask");
        await Assert.That(HumanDiagnosticParserV015.TryParseHint("  = tip: no", out _)).IsFalse();
    }
}
