using Typstium.Testing;

namespace Typstium.UnitTests;

public class RepositoryPathsTests
{
    [Test]
    public async Task Root_contains_the_solution_file()
    {
        var exists = File.Exists(Path.Combine(RepositoryPaths.Root, "Typstium.slnx"));
        await Assert.That(exists).IsTrue();
    }
}
