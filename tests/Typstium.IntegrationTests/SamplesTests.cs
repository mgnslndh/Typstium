using Typstium.Testing;

namespace Typstium.IntegrationTests;

public class SamplesTests
{
    [Test]
    public async Task License_sample_exists()
    {
        var exists = File.Exists(Path.Combine(RepositoryPaths.Samples, "templates", "license", "license.typ"));
        await Assert.That(exists).IsTrue();
    }
}
