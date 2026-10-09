namespace Typstium;

/// <param name="Path">Fully qualified path; never resolved through the current directory.</param>
/// <param name="FoundBy">The locator that found it, e.g. "WinGet" or "PATH".</param>
public sealed record TypstExecutable(string Path, string FoundBy);
