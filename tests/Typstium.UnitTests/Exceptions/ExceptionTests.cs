using System.ComponentModel;

namespace Typstium.UnitTests.Exceptions;

public class ExceptionTests
{
    [Test]
    public async Task Process_exceptions_share_the_typst_base()
    {
        Exception start = new TypstProcessStartException("C:\\typst.exe", new Win32Exception(5));
        Exception crashed = new TypstProcessCrashedException(101, "thread 'main' panicked");
        Exception output = new TypstProcessOutputException("missing page file");

        await Assert.That(start is TypstProcessException && start is TypstException).IsTrue();
        await Assert.That(crashed is TypstProcessException).IsTrue();
        await Assert.That(output is TypstProcessException).IsTrue();
    }

    [Test]
    public async Task Exceptions_carry_their_data()
    {
        var crashed = new TypstProcessCrashedException(101, "panic");
        var invocation = new TypstInvocationException("typst --bad", 2, "error: unexpected argument");
        var feature = new TypstFeatureNotSupportedException("html", new TypstVersion(0, 14, 0), new TypstVersion(0, 15, 0));

        await Assert.That(crashed.ExitCode).IsEqualTo(101);
        await Assert.That(crashed.Message).Contains("101");
        await Assert.That(invocation.CommandLine).IsEqualTo("typst --bad");
        await Assert.That(invocation.Message).Contains("bug in Typstium");
        await Assert.That(feature.InstalledVersion).IsEqualTo(new TypstVersion(0, 14, 0));
        await Assert.That(feature.Message).Contains("0.15.0");
    }
}
