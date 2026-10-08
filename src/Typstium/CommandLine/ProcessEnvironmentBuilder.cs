using System.Collections.Immutable;

namespace Typstium.CommandLine;

internal static class ProcessEnvironmentBuilder
{
    /// <summary>A closed local port: any download attempt fails fast (verified in spike 0).</summary>
    internal const string DeadProxy = "http://127.0.0.1:9";

    private static readonly string[] ProxyVariables = ["HTTPS_PROXY", "HTTP_PROXY", "ALL_PROXY", "NO_PROXY"];
    private static readonly string[] BlockingProxyVariables = ["HTTPS_PROXY", "HTTP_PROXY", "ALL_PROXY"];

    public static IEnumerable<string> CurrentVariableNames() =>
        Environment.GetEnvironmentVariables().Keys.Cast<object>().Select(key => (string)key).ToArray();

    public static EnvironmentChanges Build(IEnumerable<string> inheritedNames, TypstCompileOptions options)
    {
        var names = inheritedNames.ToArray();
        var remove = ImmutableArray.CreateBuilder<string>();
        var set = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.OrdinalIgnoreCase);

        remove.AddRange(names.Where(IsTypstVariable));

        if (options.CreationTimestamp is TypstCreationTimestamp.NowTimestamp)
        {
            remove.AddRange(names.Where(name => string.Equals(name, "SOURCE_DATE_EPOCH", StringComparison.OrdinalIgnoreCase)));
        }

        if (options.Packages.NetworkAccess == TypstNetworkAccess.Blocked)
        {
            // Remove every spelling (Unix tools read lowercase too), then set the uppercase dead proxy.
            remove.AddRange(names.Where(name => ProxyVariables.Contains(name, StringComparer.OrdinalIgnoreCase)));
            foreach (var variable in BlockingProxyVariables)
            {
                set[variable] = DeadProxy;
            }
        }

        return new EnvironmentChanges(set.ToImmutable(), remove.ToImmutable());
    }

    /// <summary>For `typst info` and `typst --version`: only host Typst settings are removed.</summary>
    public static EnvironmentChanges BuildForProbe(IEnumerable<string> inheritedNames) =>
        new(ImmutableDictionary<string, string>.Empty, inheritedNames.Where(IsTypstVariable).ToImmutableArray());

    private static bool IsTypstVariable(string name) => name.StartsWith("TYPST_", StringComparison.OrdinalIgnoreCase);
}
