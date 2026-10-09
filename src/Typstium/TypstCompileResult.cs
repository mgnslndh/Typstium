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

    /// <summary>How the compile ended.</summary>
    public TypstCompileStatus Status { get; }

    /// <summary>Whether <see cref="Status"/> is <see cref="TypstCompileStatus.Succeeded"/>; then <see cref="Document"/> is set.</summary>
    [MemberNotNullWhen(true, nameof(Document))]
    public bool IsSuccess => Status == TypstCompileStatus.Succeeded;

    /// <summary>The compiled document, set only when the compile succeeded.</summary>
    public TypstDocument? Document { get; }

    /// <summary>Every error and warning, in the order reported. A success may still have warnings.</summary>
    public ImmutableArray<TypstDiagnostic> Diagnostics { get; }

    /// <summary>The errors in <see cref="Diagnostics"/>.</summary>
    public IEnumerable<TypstDiagnostic> Errors => Diagnostics.Where(d => d.Severity == TypstDiagnosticSeverity.Error);

    /// <summary>The warnings in <see cref="Diagnostics"/>.</summary>
    public IEnumerable<TypstDiagnostic> Warnings => Diagnostics.Where(d => d.Severity == TypstDiagnosticSeverity.Warning);

    /// <summary>How Typst ran: executable, version, redacted command line and durations.</summary>
    public TypstInvocation Invocation { get; }

    /// <summary>A successful result, for tests and custom <see cref="ITypstCompiler"/> implementations.</summary>
    public static TypstCompileResult CreateSucceeded(TypstDocument document, ImmutableArray<TypstDiagnostic> diagnostics, TypstInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new TypstCompileResult(TypstCompileStatus.Succeeded, document, diagnostics, invocation);
    }

    /// <summary>An unsuccessful result, for tests and custom <see cref="ITypstCompiler"/> implementations.</summary>
    /// <param name="status">Any status except <see cref="TypstCompileStatus.Succeeded"/>.</param>
    /// <param name="diagnostics">The errors and warnings. A default array means none.</param>
    /// <param name="invocation">How Typst ran.</param>
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
