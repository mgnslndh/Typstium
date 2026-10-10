using Cake.Core.IO;
using Cake.Testing;

namespace Cake.Typstium.UnitTests.Testing;

internal static class CakeFakes
{
    /// <summary>A fake environment whose working directory is a real folder, different from the process's.</summary>
    public static FakeEnvironment Environment(string workingDirectory)
    {
        var environment = OperatingSystem.IsWindows() ? FakeEnvironment.CreateWindowsEnvironment() : FakeEnvironment.CreateUnixEnvironment();
        environment.WorkingDirectory = new DirectoryPath(workingDirectory);
        return environment;
    }
}
