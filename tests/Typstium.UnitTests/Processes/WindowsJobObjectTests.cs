using System.Diagnostics;
using Typstium.Processes;
using Typstium.UnitTests.Testing;

namespace Typstium.UnitTests.Processes;

public class WindowsJobObjectTests
{
    [Test]
    public async Task Disposing_the_job_kills_its_processes()
    {
        if (!ShellCommands.IsWindows)
        {
            Skip.Test("Job objects are Windows-only.");
        }

        using var process = Process.Start(new ProcessStartInfo(ShellCommands.Shell, "/d /c ping -n 30 127.0.0.1 >nul") { UseShellExecute = false, CreateNoWindow = true })!;
        var job = WindowsJobObject.Create(memoryLimitBytes: 256L * 1024 * 1024);
        job.Attach(process);

        job.Dispose();

        var exited = process.WaitForExit(5000);
        await Assert.That(exited).IsTrue();
    }
}
