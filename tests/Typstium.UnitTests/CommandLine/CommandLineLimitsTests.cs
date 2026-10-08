using Typstium.CommandLine;

namespace Typstium.UnitTests.CommandLine;

public class CommandLineLimitsTests
{
    [Test]
    public async Task Long_command_lines_are_rejected_with_advice()
    {
        var arguments = TypstArguments.Of("compile").AddSwitchSecret("--input", "data=" + new string('x', CommandLineLimits.MaxLength));
        var exception = Assert.Throws<ArgumentException>(() => CommandLineLimits.EnsureFits("typst", arguments));
        await Assert.That(exception!.Message).Contains("virtual file");
        await Assert.That(exception.ParamName).IsEqualTo("request");
    }

    [Test]
    public async Task Normal_command_lines_fit()
    {
        CommandLineLimits.EnsureFits("typst", TypstArguments.Of("compile", "main.typ", "-"));
        await Assert.That(TypstArguments.Of("compile", "main.typ", "-").Render()).IsEqualTo("compile main.typ -");
    }
}
