namespace Typstium;

/// <summary>
/// Base class for failures caused by the environment or by Typstium, never by the template.
/// Template failures are results (<c>TypstCompileResult</c>).
/// </summary>
public abstract class TypstException : Exception
{
    protected TypstException(string message)
        : base(message)
    {
    }

    protected TypstException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
