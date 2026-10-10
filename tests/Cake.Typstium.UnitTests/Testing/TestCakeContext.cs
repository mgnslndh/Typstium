using Cake.Core;
using Cake.Core.Configuration;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Core.Tooling;
using Cake.Testing;

namespace Cake.Typstium.UnitTests.Testing;

/// <summary>The members the aliases use (environment, configuration, log, tools); everything else throws.</summary>
internal sealed class TestCakeContext(FakeEnvironment environment) : ICakeContext
{
    public FakeLog FakeLog { get; } = new() { Verbosity = Verbosity.Diagnostic };
    public FakeConfiguration FakeConfiguration { get; } = new();
    public FakeToolLocator FakeTools { get; } = new();

    public ICakeEnvironment Environment => environment;
    public ICakeLog Log => FakeLog;
    public ICakeConfiguration Configuration => FakeConfiguration;
    public IFileSystem FileSystem => throw new NotSupportedException();
    public IGlobber Globber => throw new NotSupportedException();
    public ICakeArguments Arguments => throw new NotSupportedException();
    public IProcessRunner ProcessRunner => throw new NotSupportedException();
    public IRegistry Registry => throw new NotSupportedException();
    public IToolLocator Tools => FakeTools;
    public ICakeDataResolver Data => throw new NotSupportedException();
}
