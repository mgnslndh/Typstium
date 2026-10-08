namespace Typstium;

public interface ITypstExecutableResolver
{
    /// <exception cref="TypstExecutableNotFoundException">No locator found Typst.</exception>
    ValueTask<TypstExecutable> ResolveAsync(CancellationToken cancellationToken);
}
