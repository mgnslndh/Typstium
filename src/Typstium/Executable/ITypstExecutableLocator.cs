namespace Typstium;

/// <summary>One place to look for Typst, e.g. TYPST_PATH or WinGet. Composed by TypstExecutableResolver.</summary>
public interface ITypstExecutableLocator
{
    /// <summary>A short name shown in errors and in <see cref="TypstExecutable.FoundBy"/>, such as <c>WinGet</c>.</summary>
    string Name { get; }

    /// <summary>Looks for Typst. Return a fully qualified path; the resolver rejects any other.</summary>
    /// <returns>The path found, or the locations searched, for the error message.</returns>
    ValueTask<TypstLocatorResult> LocateAsync(CancellationToken cancellationToken);
}
