using System.Collections.Immutable;
using System.Diagnostics;

namespace Typstium.CommandLine;

/// <summary>Changes applied to the inherited environment of the Typst process: removals first, then sets.</summary>
internal sealed record EnvironmentChanges(ImmutableDictionary<string, string> Set, ImmutableArray<string> Remove)
{
    public static EnvironmentChanges None { get; } = new(ImmutableDictionary<string, string>.Empty, ImmutableArray<string>.Empty);

    public void ApplyTo(ProcessStartInfo startInfo)
    {
        foreach (var name in Remove)
        {
            startInfo.Environment.Remove(name);
        }

        foreach (var pair in Set)
        {
            startInfo.Environment[pair.Key] = pair.Value;
        }
    }
}
