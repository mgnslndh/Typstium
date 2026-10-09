namespace Typstium;

/// <summary>Finds the Typst executable for a compiler.</summary>
public interface ITypstExecutableResolver
{
    /// <summary>Finds Typst. Called before each compile, so implementations should cache.</summary>
    /// <exception cref="TypstExecutableNotFoundException">No locator found Typst.</exception>
    ValueTask<TypstExecutable> ResolveAsync(CancellationToken cancellationToken);
}
