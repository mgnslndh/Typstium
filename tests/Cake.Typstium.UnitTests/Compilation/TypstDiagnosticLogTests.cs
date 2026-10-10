using System.Collections.Immutable;
using Cake.Core.Diagnostics;
using Cake.Testing;
using Cake.Typstium.Compilation;
using Cake.Typstium.UnitTests.Testing;
using Typstium;

namespace Cake.Typstium.UnitTests.Compilation;

public class TypstDiagnosticLogTests
{
    private static FakeLog Log() => new() { Verbosity = Verbosity.Diagnostic };

    [Test]
    public async Task Errors_and_warnings_are_logged_in_canonical_form_at_their_level()
    {
        var log = Log();
        var error = Diagnostics.Error("unknown variable: x", line: 3, column: 2);
        var warning = Diagnostics.Warning("unknown font family: foo");

        TypstDiagnosticLog.Write(log, [error, warning]);

        await Assert.That(log.Entries[0].Level).IsEqualTo(LogLevel.Error);
        await Assert.That(log.Entries[0].Message).IsEqualTo(error.ToString());
        await Assert.That(log.Entries[1].Level).IsEqualTo(LogLevel.Warning);
        await Assert.That(log.Entries[1].Message).IsEqualTo(warning.ToString());
    }

    [Test]
    public async Task Braces_in_messages_reach_the_log_verbatim()
    {
        var log = Log();

        TypstDiagnosticLog.Write(log, [Diagnostics.Error("expected \"}\" after {p} and {0}")]);

        await Assert.That(log.Entries.Single().Message).Contains("expected \"}\" after {p} and {0}");
    }

    [Test]
    public async Task Hints_and_trace_frames_follow_at_verbose()
    {
        var log = Log();
        var location = new TypstLocation(new TypstSourceFile.Project(TypstPath.Parse("lib.typ"), FullPath: null), new TypstPosition(7, 4), End: null);
        var diagnostic = new TypstDiagnostic(TypstDiagnosticId.Parse("TYPST0001"), TypstDiagnosticSeverity.Error, "cannot add {a} and b", location: null,
            ImmutableArray.Create("try adding spaces"),
            ImmutableArray.Create(new TypstTraceFrame("while calling `greet`", location), new TypstTraceFrame("in {main}", null)));

        TypstDiagnosticLog.Write(log, [diagnostic]);

        await Assert.That(log.Entries.Count).IsEqualTo(4);
        await Assert.That(log.Entries.Skip(1).All(entry => entry.Verbosity == Verbosity.Verbose)).IsTrue();
        await Assert.That(log.Entries[1].Message).IsEqualTo("    hint: try adding spaces");
        await Assert.That(log.Entries[2].Message).IsEqualTo("    while calling `greet` at /lib.typ(7,4)");
        await Assert.That(log.Entries[3].Message).IsEqualTo("    in {main}");
    }
}
