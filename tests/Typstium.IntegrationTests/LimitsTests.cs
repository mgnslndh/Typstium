using System.Runtime.InteropServices;
using Typstium.Testing.Fixtures;
using Typstium.Testing.Typst;

namespace Typstium.IntegrationTests;

public class LimitsTests
{
    [Test]
    public async Task Huge_output_hits_the_output_limit()
    {
        using var temp = new TemporaryDirectory("it-limits");
        var compiler = await TypstAvailability.CreateCompilerAsync(CliTypstCompilerSettings.Default with { TempRoot = temp.Path });

        // 300 pages make about 200 KB of PDF in well under a second; the 64 KB cap is passed three times over.
        var result = await compiler.CompileMarkupAsync(
            "#for i in range(300) [Page #i #pagebreak()]",
            TypstOutput.Pdf(),
            TypstCompileOptions.Default with { MaxOutputBytes = 64 * 1024 });

        await Assert.That(result.Status).IsEqualTo(TypstCompileStatus.OutputLimitExceeded);
    }

    [Test]
    public async Task Windows_job_object_memory_cap_stops_memory_hungry_templates()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Skip.Test("The memory cap uses Windows job objects.");
        }

        using var temp = new TemporaryDirectory("it-limits");
        var compiler = await TypstAvailability.CreateCompilerAsync(CliTypstCompilerSettings.Default with { TempRoot = temp.Path });
        var options = TypstCompileOptions.Default with { MemoryLimitBytes = 100L * 1024 * 1024, Timeout = TimeSpan.FromSeconds(30) };

        TypstCompileResult? result = null;
        try
        {
            result = await compiler.CompileMarkupAsync("#let a = range(50000000)\n#a.len()", TypstOutput.Pdf(), options);
        }
        catch (TypstProcessCrashedException)
        {
            // Expected: allocation fails under the cap and Typst aborts.
        }

        await Assert.That(result is null || !result.IsSuccess).IsTrue();
    }
}
