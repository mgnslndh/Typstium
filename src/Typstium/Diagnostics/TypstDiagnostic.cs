using System.Collections.Immutable;
using System.Text;

namespace Typstium;

/// <summary>An error or warning from a compile: one parsed from Typst's output, or one Typstium reports itself.</summary>
public sealed record TypstDiagnostic
{
    /// <summary>Creates a diagnostic, for tests and custom <see cref="ITypstCompiler"/> implementations.</summary>
    /// <param name="id">The diagnostic code.</param>
    /// <param name="severity">Error or warning.</param>
    /// <param name="message">The message, without location or code.</param>
    /// <param name="location">Where it occurred, or null when Typst gave no location.</param>
    /// <param name="hints">Hints that follow the message. A default array means none.</param>
    /// <param name="trace">The call stack that led here, innermost first. A default array means none.</param>
    public TypstDiagnostic(
        TypstDiagnosticId id,
        TypstDiagnosticSeverity severity,
        string message,
        TypstLocation? location,
        ImmutableArray<string> hints,
        ImmutableArray<TypstTraceFrame> trace)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Severity = severity;
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Location = location;
        Hints = hints.IsDefault ? ImmutableArray<string>.Empty : hints;
        Trace = trace.IsDefault ? ImmutableArray<TypstTraceFrame>.Empty : trace;
    }

    /// <summary>The stable code, such as <c>TYPST1001</c>, for filtering and for <c>NoWarn</c>/<c>WarningsAsErrors</c> lists.</summary>
    public TypstDiagnosticId Id { get; }

    /// <summary>Error or warning.</summary>
    public TypstDiagnosticSeverity Severity { get; }

    /// <summary>The message, without location or code. Sandbox paths are replaced by root-relative ones.</summary>
    public string Message { get; }

    /// <summary>Where it occurred, or null when Typst gave no location.</summary>
    public TypstLocation? Location { get; }

    /// <summary>Hints from Typst, then any Typstium added; empty when there are none.</summary>
    public ImmutableArray<string> Hints { get; }

    /// <summary>The call stack that led here, innermost first; empty when there is none.</summary>
    public ImmutableArray<TypstTraceFrame> Trace { get; }

    /// <summary>Returns a copy with <paramref name="additionalHints"/> appended to <see cref="Hints"/>.</summary>
    public TypstDiagnostic WithHints(IEnumerable<string> additionalHints) =>
        new(Id, Severity, Message, Location, Hints.AddRange(additionalHints), Trace);

    /// <summary>MSBuild canonical form: <c>path(line,col): error TYPST1001: message</c>.</summary>
    public override string ToString()
    {
        var builder = new StringBuilder();
        if (Location is not null)
        {
            builder.Append(Location.File.DisplayPath)
                .Append('(').Append(Location.Start.Line).Append(',').Append(Location.Start.Column).Append("): ");
        }

        builder.Append(Severity == TypstDiagnosticSeverity.Error ? "error" : "warning")
            .Append(' ').Append(Id.Value).Append(": ").Append(Message);
        return builder.ToString();
    }
}
