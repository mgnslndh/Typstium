using System.Text;

namespace Typstium;

/// <summary>Thrown only by <see cref="TypstCompileResult.EnsureSuccess"/>, for callers that treat failure as exceptional.</summary>
public sealed class TypstCompileException : TypstException
{
    private const int MaxDiagnosticsInMessage = 10;

    /// <summary>Creates the exception; the message lists the status and up to ten errors.</summary>
    public TypstCompileException(TypstCompileResult result)
        : base(BuildMessage(result ?? throw new ArgumentNullException(nameof(result))))
    {
        Result = result;
    }

    /// <summary>The unsuccessful result, with every diagnostic.</summary>
    public TypstCompileResult Result { get; }

    private static string BuildMessage(TypstCompileResult result)
    {
        var builder = new StringBuilder($"Typst compilation did not succeed: {result.Status}.");
        foreach (var diagnostic in result.Errors.Take(MaxDiagnosticsInMessage))
        {
            builder.AppendLine().Append(diagnostic);
        }

        return builder.ToString();
    }
}
