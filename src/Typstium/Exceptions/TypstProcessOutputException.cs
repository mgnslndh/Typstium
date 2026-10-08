namespace Typstium;

/// <summary>Typst's output could not be read, which points to a Typst/Typstium version mismatch.</summary>
public sealed class TypstProcessOutputException : TypstProcessException
{
    public TypstProcessOutputException(string description)
        : this(description, innerException: null)
    {
    }

    public TypstProcessOutputException(string description, Exception? innerException)
        : base($"Typst produced output that Typstium could not read: {description}", innerException)
    {
        Description = description;
    }

    public string Description { get; }
}
