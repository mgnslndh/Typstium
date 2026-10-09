using Typstium.Testing.Fixtures;
using Typstium.Testing.Typst;

namespace Typstium.TimingTests;

/// <summary>Turning Typst's stderr into diagnostics must take linear time, whatever a template prints.</summary>
public class DiagnosticParsingTests
{
    // An assert message is printed verbatim, so a template controls what the parser sees. A continuation line of
    // 900,000 spaces (just under the 1 MB stderr cap) took a backtracking regex hours; it must now take milliseconds.
    [Test]
    [Timeout(120_000)]
    public async Task A_huge_assert_message_is_parsed_in_linear_time(CancellationToken cancellationToken)
    {
        using var temp = new TemporaryDirectory("timing");
        var compiler = await TypstAvailability.CreateCompilerAsync(CliTypstCompilerSettings.Default with { TempRoot = temp.Path });
        var markup = "#assert(false, message: \"first line\\n\" + \" \" * 900000 + \"x at nowhere\")\n";

        var result = await compiler.CompileMarkupAsync(markup, TypstOutput.Pdf(), TypstCompileOptions.Default, cancellationToken);

        var invocation = result.Invocation;
        var outsideTypst = invocation.TotalDuration - invocation.ProcessDuration;
        await Assert.That(result.Status).IsEqualTo(TypstCompileStatus.Failed);
        await Assert.That(result.Errors.Any(error => error.Message.StartsWith("assertion failed: first line", StringComparison.Ordinal))).IsTrue();
        await Assert.That(outsideTypst).IsLessThan(TimeSpan.FromSeconds(1))
            .Because($"process {invocation.ProcessDuration.TotalMilliseconds:F0} ms, total {invocation.TotalDuration.TotalMilliseconds:F0} ms");
    }
}
