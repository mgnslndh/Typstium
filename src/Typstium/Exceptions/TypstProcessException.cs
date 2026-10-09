namespace Typstium;

/// <summary>The Typst process misbehaved: it couldn't start, crashed, or produced unreadable output.</summary>
public abstract class TypstProcessException : TypstException
{
    /// <summary>Initializes the exception with a message and an optional cause.</summary>
    protected TypstProcessException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
