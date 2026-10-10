using Cake.Core.IO;
using Cake.Core.Tooling;

namespace Cake.Typstium.UnitTests.Testing;

/// <summary>Cake's tool registry, as #tool would have filled it: file name → registered path.</summary>
internal sealed class FakeToolLocator : IToolLocator
{
    private readonly Dictionary<string, FilePath> _registered = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterFile(FilePath path) => _registered[path.GetFilename().FullPath] = path;

    public FilePath? Resolve(string tool) => _registered.GetValueOrDefault(tool);

    public FilePath? Resolve(IEnumerable<string> toolExeNames) => toolExeNames.Select(Resolve).FirstOrDefault(path => path is not null);
}
