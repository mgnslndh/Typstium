using System.Text.RegularExpressions;
using Typstium.Executable;

namespace Typstium.UnitTests.Testing;

/// <summary>An in-memory file system and environment for locator tests. Paths use the host separator.</summary>
internal sealed class FakeLocatorEnvironment : ILocatorEnvironment
{
    public static string Root { get; } = Path.Combine(Path.GetTempPath(), "fake-fs");

    public bool IsWindows { get; init; } = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);

    public Dictionary<string, string> Variables { get; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string BaseDirectory { get; init; } = Path.Combine(Root, "app");

    public string RuntimeIdentifier { get; init; } = "win-x64";

    public string? HomeDirectory { get; init; } = Path.Combine(Root, "home");

    public List<string> MadeExecutable { get; } = new();

    public string AddFile(params string[] segments)
    {
        var path = Path.Combine(segments);
        Files.Add(path);
        return path;
    }

    public string? GetVariable(string name) => Variables.TryGetValue(name, out var value) ? value : null;

    public bool FileExists(string path) => Files.Contains(path);

    public IEnumerable<string> EnumerateDirectories(string path, string searchPattern)
    {
        var prefix = path.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var pattern = new Regex("^" + Regex.Escape(searchPattern).Replace("\\*", ".*") + "\\z", RegexOptions.IgnoreCase);
        return Files
            .Where(file => file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(file => file.Substring(prefix.Length).Split(Path.DirectorySeparatorChar))
            .Where(parts => parts.Length > 1 && pattern.IsMatch(parts[0]))
            .Select(parts => prefix + parts[0])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public void EnsureExecutable(string path) => MadeExecutable.Add(path);

    /// <summary>Directory → manifest JSON. Parsed through the real parser, so a test can't use a manifest it would reject.</summary>
    public Dictionary<string, string> Manifests { get; } = new(StringComparer.OrdinalIgnoreCase);

    public RuntimeManifestResult ReadRuntimeManifest(string directory)
    {
        var path = Path.Combine(directory, RuntimeManifest.FileName);
        return Manifests.TryGetValue(directory, out var json) ? RuntimeManifest.Parse(path, json, IsWindows) : new RuntimeManifestResult.Missing(path);
    }
}
