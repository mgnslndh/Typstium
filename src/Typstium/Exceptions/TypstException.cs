namespace Typstium;

/// <summary>
/// Base class for failures caused by the environment or by Typstium, never by the template.
/// Template failures are results (<c>TypstCompileResult</c>).
/// </summary>
public abstract class TypstException : Exception
{
    /// <summary>Initializes the exception with a message.</summary>
    protected TypstException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes the exception with a message and the exception that caused it.</summary>
    protected TypstException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
