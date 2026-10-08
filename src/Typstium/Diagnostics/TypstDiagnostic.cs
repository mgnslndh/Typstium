using System.Collections.Immutable;
using System.Text;

namespace Typstium;

public sealed record TypstDiagnostic
{
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

    public TypstDiagnosticId Id { get; }

    public TypstDiagnosticSeverity Severity { get; }

    public string Message { get; }

    public TypstLocation? Location { get; }

    public ImmutableArray<string> Hints { get; }

    public ImmutableArray<TypstTraceFrame> Trace { get; }

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
