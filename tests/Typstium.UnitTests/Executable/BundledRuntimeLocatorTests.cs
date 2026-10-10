using Typstium.Executable;
using Typstium.UnitTests.Testing;

namespace Typstium.UnitTests.Executable;

public class BundledRuntimeLocatorTests
{
    private static Task<TypstLocatorResult> Locate(FakeLocatorEnvironment environment) =>
        new BundledRuntimeLocator(environment).LocateAsync(CancellationToken.None).AsTask();

    private static string[] Searched(TypstLocatorResult result) => ((TypstLocatorResult.NotFoundResult)result).SearchedLocations.ToArray();

    [Test]
    public async Task The_classic_asp_net_bin_folder_is_searched_and_the_execute_bit_set()
    {
        var environment = new FakeLocatorEnvironment { IsWindows = false, RuntimeIdentifier = "linux-x64" };
        var typst = environment.AddFile(environment.BaseDirectory, "bin", "typst", "linux-x64", TypstExecutableFileNames.Unix);

        await Assert.That(await Locate(environment)).IsEqualTo(TypstLocatorResult.Found(typst));
        await Assert.That(environment.MadeExecutable).Contains(typst);
    }

    [Test]
    [Property("Scenario", "RS31")]
    public async Task A_manifest_points_at_a_custom_folder()
    {
        var environment = new FakeLocatorEnvironment { IsWindows = false, RuntimeIdentifier = "linux-x64" };
        environment.Manifests[environment.BaseDirectory] = """{ "version": 1, "runtimes": { "linux-x64": "tools/typst" } }""";
        var typst = environment.AddFile(environment.BaseDirectory, "tools", "typst");

        await Assert.That(await Locate(environment)).IsEqualTo(TypstLocatorResult.Found(typst));
        await Assert.That(environment.MadeExecutable).Contains(typst);
    }

    [Test]
    public async Task A_manifest_is_authoritative_over_conventional_folders()
    {
        var environment = new FakeLocatorEnvironment { IsWindows = false, RuntimeIdentifier = "linux-x64" };
        environment.Manifests[environment.BaseDirectory] = """{ "version": 1, "runtimes": { "linux-x64": "tools/typst" } }""";
        environment.AddFile(environment.BaseDirectory, "typst", "linux-x64", TypstExecutableFileNames.Unix);   // stale deploy

        var result = await Locate(environment);

        await Assert.That(Searched(result)).IsEquivalentTo(
            new[] { Path.Combine(environment.BaseDirectory, RuntimeManifest.FileName), Path.Combine(environment.BaseDirectory, "tools", "typst") },
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task A_manifest_in_bin_is_used_when_the_app_folder_has_none()
    {
        var environment = new FakeLocatorEnvironment { IsWindows = true, RuntimeIdentifier = "win-x64" };
        var bin = Path.Combine(environment.BaseDirectory, "bin");
        environment.Manifests[bin] = """{ "version": 1, "runtimes": { "win-x64": "typst/win-x64/typst.exe" } }""";
        var typst = environment.AddFile(bin, "typst", "win-x64", TypstExecutableFileNames.Windows);

        await Assert.That(await Locate(environment)).IsEqualTo(TypstLocatorResult.Found(typst));
    }

    [Test]
    public async Task An_invalid_manifest_is_reported_and_nothing_else_is_searched()
    {
        var environment = new FakeLocatorEnvironment { IsWindows = false, RuntimeIdentifier = "linux-x64" };
        environment.Manifests[environment.BaseDirectory] = """{ "version": 1, "runtimes": { "linux-x64": "/usr/bin/typst" } }""";
        environment.AddFile(environment.BaseDirectory, "typst", "linux-x64", TypstExecutableFileNames.Unix);

        var searched = Searched(await Locate(environment));

        await Assert.That(searched.Single()).IsEqualTo(
            $"{Path.Combine(environment.BaseDirectory, RuntimeManifest.FileName)} (path '/usr/bin/typst' for linux-x64 is not relative)");
    }

    [Test]
    [Property("Scenario", "RS41")]
    public async Task Windows_on_arm_falls_back_to_the_x64_runtime_in_the_manifest()
    {
        var environment = new FakeLocatorEnvironment { IsWindows = true, RuntimeIdentifier = "win-arm64" };
        environment.Manifests[environment.BaseDirectory] = """{ "version": 1, "runtimes": { "win-x64": "typst/win-x64/typst.exe" } }""";
        var typst = environment.AddFile(environment.BaseDirectory, "typst", "win-x64", TypstExecutableFileNames.Windows);

        await Assert.That(await Locate(environment)).IsEqualTo(TypstLocatorResult.Found(typst));
    }

    [Test]
    [Property("Scenario", "RS41")]
    public async Task Apple_silicon_prefers_the_native_runtime_and_falls_back_to_x64_in_conventional_folders()
    {
        var environment = new FakeLocatorEnvironment { IsWindows = false, RuntimeIdentifier = "osx-arm64" };
        var x64 = environment.AddFile(environment.BaseDirectory, "typst", "osx-x64", TypstExecutableFileNames.Unix);
        await Assert.That(await Locate(environment)).IsEqualTo(TypstLocatorResult.Found(x64));

        var arm64 = environment.AddFile(environment.BaseDirectory, "bin", "typst", "osx-arm64", TypstExecutableFileNames.Unix);
        await Assert.That(await Locate(environment)).IsEqualTo(TypstLocatorResult.Found(arm64));
    }

    [Test]
    public async Task Nothing_found_lists_every_folder_for_every_candidate()
    {
        var environment = new FakeLocatorEnvironment { IsWindows = false, RuntimeIdentifier = "osx-arm64" };
        var root = environment.BaseDirectory;

        await Assert.That(Searched(await Locate(environment))).IsEquivalentTo(
            new[]
            {
                Path.Combine(root, "typst", "osx-arm64", "typst"), Path.Combine(root, "bin", "typst", "osx-arm64", "typst"),
                Path.Combine(root, "typst", "osx-x64", "typst"), Path.Combine(root, "bin", "typst", "osx-x64", "typst"),
            },
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }
}
