namespace Typstium;

/// <summary>One place to look for Typst, e.g. TYPST_PATH or WinGet. Composed by TypstExecutableResolver.</summary>
public interface ITypstExecutableLocator
{
    string Name { get; }

    ValueTask<TypstLocatorResult> LocateAsync(CancellationToken cancellationToken);
}
