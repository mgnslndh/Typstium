using Cake.Core;
using Cake.Core.Configuration;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Core.Tooling;
using Cake.Testing;

namespace Cake.Typstium.IntegrationTests;

/// <summary>The members the aliases use (environment, configuration, log); everything else throws.</summary>
internal sealed class TestCakeContext(FakeEnvironment environment) : ICakeContext
{
    public static TestCakeContext In(string workingDirectory)
    {
        var environment = OperatingSystem.IsWindows() ? FakeEnvironment.CreateWindowsEnvironment() : FakeEnvironment.CreateUnixEnvironment();
        environment.WorkingDirectory = new DirectoryPath(workingDirectory);
        return new TestCakeContext(environment);
    }

    public FakeLog FakeLog { get; } = new() { Verbosity = Verbosity.Diagnostic };
    public FakeConfiguration FakeConfiguration { get; } = new();

    public ICakeEnvironment Environment => environment;
    public ICakeLog Log => FakeLog;
    public ICakeConfiguration Configuration => FakeConfiguration;
    public IFileSystem FileSystem => throw new NotSupportedException();
    public IGlobber Globber => throw new NotSupportedException();
    public ICakeArguments Arguments => throw new NotSupportedException();
    public IProcessRunner ProcessRunner => throw new NotSupportedException();
    public IRegistry Registry => throw new NotSupportedException();
    public IToolLocator Tools { get; } = new ToolLocatorWithNothingRegistered();
    public ICakeDataResolver Data => throw new NotSupportedException();

    private sealed class ToolLocatorWithNothingRegistered : IToolLocator
    {
        public void RegisterFile(FilePath path)
        {
        }

        public FilePath? Resolve(string tool) => null;

        public FilePath? Resolve(IEnumerable<string> toolExeNames) => null;
    }
}
