namespace Typstium;

/// <summary>Typst's output could not be read, which points to a Typst/Typstium version mismatch.</summary>
public sealed class TypstProcessOutputException : TypstProcessException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="description">What couldn't be read.</param>
    public TypstProcessOutputException(string description)
        : this(description, innerException: null)
    {
    }

    /// <summary>Creates the exception with the exception that caused it.</summary>
    /// <param name="description">What couldn't be read.</param>
    /// <param name="innerException">The cause, or null.</param>
    public TypstProcessOutputException(string description, Exception? innerException)
        : base($"Typst produced output that Typstium could not read: {description}", innerException)
    {
        Description = description;
    }

    /// <summary>What couldn't be read.</summary>
    public string Description { get; }
}
