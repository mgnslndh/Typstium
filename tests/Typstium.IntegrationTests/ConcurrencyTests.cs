using Typstium.Testing.Fixtures;
using Typstium.Testing.Typst;

namespace Typstium.IntegrationTests;

public class ConcurrencyTests
{
    [Test]
    public async Task Fifty_parallel_compiles_on_a_cold_compiler()
    {
        using var temp = new TemporaryDirectory("it-parallel");
        var compiler = await TypstAvailability.CreateCompilerAsync(CliTypstCompilerSettings.Default with { TempRoot = temp.Path, MaxConcurrency = 4 });

        var results = await Task.WhenAll(Enumerable.Range(0, 50).Select(i =>
            Task.Run(() => compiler.CompileMarkupAsync($"= Document {i}\n#lorem(50)"))));

        await Assert.That(results.All(result => result.IsSuccess)).IsTrue();
        await Assert.That(Directory.GetDirectories(temp.Path, "ws-*").Length).IsEqualTo(0);
    }

    [Test]
    [Arguments(TypstProcessPriority.BelowNormal)]
    [Arguments(TypstProcessPriority.Idle)]
    public async Task Compiles_at_a_lowered_process_priority(TypstProcessPriority priority)
    {
        using var temp = new TemporaryDirectory("it-priority");
        var compiler = await TypstAvailability.CreateCompilerAsync(CliTypstCompilerSettings.Default with { TempRoot = temp.Path, ProcessPriority = priority });

        var result = await compiler.CompileMarkupAsync("= Low priority");

        await Assert.That(result.IsSuccess).IsTrue();
    }
}
