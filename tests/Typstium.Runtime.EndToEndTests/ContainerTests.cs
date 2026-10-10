using Typstium.Runtime.Testing;

namespace Typstium.Runtime.EndToEndTests;

public class ContainerTests
{
    [Test]
    [Property(Scenario.Key, "RS9")]
    [Property(Scenario.Key, "RS11")]
    [Arguments("linux-musl-x64", "mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine")]
    [Arguments("linux-x64", "mcr.microsoft.com/dotnet/runtime-deps:10.0-noble")]
    public async Task A_self_contained_linux_app_compiles_in_a_container(string rid, string image)
    {
        await RealPackages.RequireDockerAsync();
        using var app = await RealPackages.CreateAppAsync(runtimes: ["linux-x64"]);
        await app.PublishAsync("-r", rid, "--self-contained", "true", "-p:UseAppHost=true");

        var run = await DotNet.RunToolAsync("docker", app.Directory, new Dictionary<string, string?>(),
            "run", "--rm", "-v", $"{Path.Combine(app.Directory, "pub")}:/app", image, "/app/App");

        await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
        await Assert.That(run.Output).Contains("found-by=BundledRuntime");
        await Assert.That(run.Output).Contains("pdf=True");
    }

    [Test]
    [Property(Scenario.Key, "RS10")]
    public async Task An_sdk_container_image_compiles()
    {
        await RealPackages.RequireDockerAsync();
        using var app = await RealPackages.CreateAppAsync(runtimes: ["linux-x64"]);
        var archive = Path.Combine(app.Directory, "image.tar.gz");
        await app.PublishAsync("-r", "linux-x64", "-t:PublishContainer", "-p:ContainerRepository=typstium-e2e", "-p:ContainerImageTag=test", $"-p:ContainerArchiveOutputPath={archive}");

        var load = await DotNet.RunToolAsync("docker", app.Directory, new Dictionary<string, string?>(), "load", "-i", archive);
        var run = await DotNet.RunToolAsync("docker", app.Directory, new Dictionary<string, string?>(), "run", "--rm", "typstium-e2e:test");

        await Assert.That(load.ExitCode).IsEqualTo(0).Because(load.Output);
        await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
        await Assert.That(run.Output).Contains("found-by=BundledRuntime");
        await Assert.That(run.Output).Contains("pdf=True");
    }
}
