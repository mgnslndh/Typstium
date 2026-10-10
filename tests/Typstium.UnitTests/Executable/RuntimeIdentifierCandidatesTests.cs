using Typstium.Executable;

namespace Typstium.UnitTests.Executable;

public class RuntimeIdentifierCandidatesTests
{
    [Test]
    [Property("Scenario", "RS41")]
    [Arguments("win-arm64", new[] { "win-arm64", "win-x64" })]
    [Arguments("osx-arm64", new[] { "osx-arm64", "osx-x64" })]
    public async Task Arm_platforms_fall_back_to_the_x64_binary_their_emulation_runs(string native, string[] expected)
    {
        await Assert.That(RuntimeIdentifierCandidates.For(native)).IsEquivalentTo(expected, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    [Arguments("win-x64")]
    [Arguments("linux-arm64")]
    [Arguments("linux-x64")]
    [Arguments("linux-arm")]
    [Arguments("osx-x64")]
    [Arguments("win-x86")]
    public async Task Other_platforms_try_only_themselves(string native)
    {
        await Assert.That(RuntimeIdentifierCandidates.For(native)).IsEquivalentTo(new[] { native }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }
}
