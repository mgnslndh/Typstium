using TUnit.Core.Interfaces;

[assembly: ParallelLimiter<Typstium.Runtime.EndToEndTests.TwoAtATime>]

namespace Typstium.Runtime.EndToEndTests;

// Each test restores real packages of about 20 MB and runs dotnet build or publish.
public sealed class TwoAtATime : IParallelLimit
{
    public int Limit => 2;
}
