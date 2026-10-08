using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Typstium;

/// <summary>
/// The outcome of a compile. Template problems (errors, timeouts, too much output) are results,
/// not exceptions. Status, IsSuccess and Document always agree.
/// </summary>
public sealed class TypstCompileResult
{
    private TypstCompileResult(TypstCompileStatus status, TypstDocument? document, ImmutableArray<TypstDiagnostic> diagnostics, TypstInvocation invocation)
    {
        Status = status;
        Document = document;
        Diagnostics = diagnostics.IsDefault ? ImmutableArray<TypstDiagnostic>.Empty : diagnostics;
        Invocation = invocation ?? throw new ArgumentNullException(nameof(invocation));
    }

    public TypstCompileStatus Status { get; }

    [MemberNotNullWhen(true, nameof(Document))]
    public bool IsSuccess => Status == TypstCompileStatus.Succeeded;

    public TypstDocument? Document { get; }

    public ImmutableArray<TypstDiagnostic> Diagnostics { get; }

    public IEnumerable<TypstDiagnostic> Errors => Diagnostics.Where(d => d.Severity == TypstDiagnosticSeverity.Error);

    public IEnumerable<TypstDiagnostic> Warnings => Diagnostics.Where(d => d.Severity == TypstDiagnosticSeverity.Warning);

    public TypstInvocation Invocation { get; }

    public static TypstCompileResult CreateSucceeded(TypstDocument document, ImmutableArray<TypstDiagnostic> diagnostics, TypstInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new TypstCompileResult(TypstCompileStatus.Succeeded, document, diagnostics, invocation);
    }

    public static TypstCompileResult CreateUnsuccessful(TypstCompileStatus status, ImmutableArray<TypstDiagnostic> diagnostics, TypstInvocation invocation)
    {
        if (status == TypstCompileStatus.Succeeded)
        {
            throw new ArgumentException("Use CreateSucceeded for successful results.", nameof(status));
        }

        if (status is not (TypstCompileStatus.Failed or TypstCompileStatus.TimedOut or TypstCompileStatus.OutputLimitExceeded))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown compile status.");
        }

        return new TypstCompileResult(status, document: null, diagnostics, invocation);
    }

    /// <summary>Returns this result if it succeeded; otherwise throws <c>TypstCompileException</c>.</summary>
    public TypstCompileResult EnsureSuccess() => IsSuccess ? this : throw new TypstCompileException(this);
}
