namespace Typstium.Testing.Fakes;

public sealed class FixedExecutableResolver(string path) : ITypstExecutableResolver
{
    public ValueTask<TypstExecutable> ResolveAsync(CancellationToken cancellationToken) =>
        new(new TypstExecutable(path, "Fixed"));
}
