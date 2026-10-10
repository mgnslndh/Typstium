using TUnit.Core.Interfaces;

[assembly: ParallelLimiter<Cake.Typstium.EndToEndTests.TwoAtATime>]

namespace Cake.Typstium.EndToEndTests;

// Each test installs Cake.Tool or restores a Cake.Sdk project and copies a 20 MB runtime package.
public sealed class TwoAtATime : IParallelLimit
{
    public int Limit => 2;
}
