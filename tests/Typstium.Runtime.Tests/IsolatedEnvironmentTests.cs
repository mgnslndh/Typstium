using Typstium.Runtime.Testing;
using Typstium.Testing.Fixtures;

namespace Typstium.Runtime.Tests;

public class IsolatedEnvironmentTests
{
    [Test]
    public async Task Two_scenarios_share_none_of_the_per_user_folders()
    {
        using var temp = new TemporaryDirectory("isolated");

        var first = IsolatedEnvironment.Create(temp.Combine("a"), new Dictionary<string, string?>());
        var second = IsolatedEnvironment.Create(temp.Combine("b"), new Dictionary<string, string?>());

        foreach (var variable in IsolatedEnvironment.Variables)
        {
            await Assert.That(first[variable]).IsNotEqualTo(second[variable]).Because(variable);
            await Assert.That(first[variable]!).StartsWith(temp.Combine("a")).Because(variable);
            await Assert.That(Directory.Exists(first[variable])).IsTrue().Because(variable);
        }
    }

    [Test]
    public async Task Covers_the_dotnet_cli_and_nuget_state_and_the_temp_folder()
    {
        await Assert.That(IsolatedEnvironment.Variables).IsEquivalentTo(new[]
        {
            "NUGET_PACKAGES", "NUGET_HTTP_CACHE_PATH", "NUGET_SCRATCH", "NUGET_PLUGINS_CACHE_PATH", "DOTNET_CLI_HOME",
            "TMP", "TEMP", "TMPDIR",
        });
    }

    [Test]
    public async Task Keeps_the_other_variables_and_replaces_the_isolated_ones()
    {
        using var temp = new TemporaryDirectory("isolated");
        var baseEnvironment = new Dictionary<string, string?> { ["TYPST_PATH"] = null, ["NUGET_PACKAGES"] = "shared" };

        var environment = IsolatedEnvironment.Create(temp.Combine("a"), baseEnvironment);

        await Assert.That(environment.ContainsKey("TYPST_PATH")).IsTrue();
        await Assert.That(environment["TYPST_PATH"]).IsNull();
        await Assert.That(environment["NUGET_PACKAGES"]).IsNotEqualTo("shared");
    }

    [Test]
    public async Task Creating_it_again_for_the_same_root_gives_the_same_folders()
    {
        using var temp = new TemporaryDirectory("isolated");

        var first = IsolatedEnvironment.Create(temp.Combine("a"), new Dictionary<string, string?>());
        var again = IsolatedEnvironment.Create(temp.Combine("a"), new Dictionary<string, string?>());

        await Assert.That(again).IsEquivalentTo(first);
    }
}
