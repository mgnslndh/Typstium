using TUnit.Core.Interfaces;

[assembly: ParallelLimiter<Typstium.Runtime.Tests.FourAtATime>]

namespace Typstium.Runtime.Tests;

// Each test runs dotnet build or publish; more than a few at once only thrashes the machine.
public sealed class FourAtATime : IParallelLimit
{
    public int Limit => 4;
}
