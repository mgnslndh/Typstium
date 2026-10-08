using System.Collections.Immutable;
using Typstium.Diagnostics;
using Typstium.Processes;
using Typstium.Workspace;

namespace Typstium.Compilation;

/// <summary>Step 9: process outcome → result status, or an exception when Typstium or the environment is at fault.</summary>
internal static class OutcomeMapper
{
    public static TypstCompileResult Map(
        ProcessOutcome outcome,
        ImmutableArray<TypstDiagnostic> diagnostics,
        TypstCompileRequest request,
        WorkspaceLayout layout,
        string commandLine,
        TypstInvocation invocation)
    {
        switch (outcome)
        {
            case ProcessOutcome.TimedOut:
                return TypstCompileResult.CreateUnsuccessful(
                    TypstCompileStatus.TimedOut,
                    diagnostics.Add(Diagnostic(DiagnosticIds.TimedOut, $"Typst did not finish within {request.Options.Timeout.TotalSeconds:0.#} seconds.")),
                    invocation);

            case ProcessOutcome.OutputLimitExceeded exceeded:
                return OutputLimitExceeded(diagnostics, exceeded.LimitBytes, invocation);

            case ProcessOutcome.Exited { ExitCode: 0 } exited:
                var document = DocumentCollector.Collect(request.Output, exited.StandardOutput, layout);
                if (document is TypstDocument.Paged paged && paged.Pages.Sum(page => (long)page.Length) > request.Options.MaxOutputBytes)
                {
                    return OutputLimitExceeded(diagnostics, request.Options.MaxOutputBytes, invocation);
                }

                return TypstCompileResult.CreateSucceeded(document, diagnostics, invocation);

            case ProcessOutcome.Exited { ExitCode: 1 }:
                if (!diagnostics.Any(diagnostic => diagnostic.Severity == TypstDiagnosticSeverity.Error))
                {
                    diagnostics = diagnostics.Add(Diagnostic(DiagnosticIds.GenericError, "Typst reported a failure without an error message."));
                }

                return TypstCompileResult.CreateUnsuccessful(TypstCompileStatus.Failed, diagnostics, invocation);

            case ProcessOutcome.Exited { ExitCode: 2 } invalid:
                throw new TypstInvocationException(commandLine, invalid.ExitCode, invalid.StandardError);

            case ProcessOutcome.Exited crashed:
                throw new TypstProcessCrashedException(crashed.ExitCode, crashed.StandardError);

            default:
                throw new InvalidOperationException($"Unknown process outcome '{outcome.GetType().Name}'.");
        }
    }

    private static TypstCompileResult OutputLimitExceeded(ImmutableArray<TypstDiagnostic> diagnostics, long limitBytes, TypstInvocation invocation) =>
        TypstCompileResult.CreateUnsuccessful(
            TypstCompileStatus.OutputLimitExceeded,
            diagnostics.Add(Diagnostic(DiagnosticIds.OutputLimitExceeded, $"The output exceeded the limit of {limitBytes} bytes (MaxOutputBytes).")),
            invocation);

    private static TypstDiagnostic Diagnostic(TypstDiagnosticId id, string message) =>
        new(id, TypstDiagnosticSeverity.Error, message, location: null, ImmutableArray<string>.Empty, ImmutableArray<TypstTraceFrame>.Empty);
}
