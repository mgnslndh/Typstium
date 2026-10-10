using System.Text.Json;
using Typstium.Utilities;

namespace Typstium.Executable;

/// <summary>What <see cref="ILocatorEnvironment.ReadRuntimeManifest"/> found in a folder.</summary>
internal abstract record RuntimeManifestResult
{
    private RuntimeManifestResult()
    {
    }

    /// <summary>There is no manifest at <paramref name="Path"/>.</summary>
    public sealed record Missing(string Path) : RuntimeManifestResult;

    /// <summary>The manifest at <paramref name="Path"/> can't be used; <paramref name="Reason"/> says why.</summary>
    public sealed record Invalid(string Path, string Reason) : RuntimeManifestResult;

    /// <summary>The manifest at <paramref name="Path"/>, validated.</summary>
    public sealed record Loaded(string Path, RuntimeManifest Manifest) : RuntimeManifestResult;
}

/// <summary>
/// typstium.runtime.json, written next to the app by Typstium's build targets: RID → executable, relative to the
/// manifest's folder (spec runtime-packages §6–§7). Every path is checked so a manifest can't point outside its folder.
/// </summary>
internal sealed class RuntimeManifest
{
    public const string FileName = "typstium.runtime.json";

    private RuntimeManifest(IReadOnlyDictionary<string, string> executables) => Executables = executables;

    /// <summary>RID → full executable path, every one inside the manifest's folder.</summary>
    public IReadOnlyDictionary<string, string> Executables { get; }

    /// <summary>Pure: no file system access. <paramref name="isWindows"/> selects the path rules for "relative".</summary>
    public static RuntimeManifestResult Parse(string path, string json, bool isWindows)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json.TrimStart('\uFEFF'));
        }
        catch (JsonException exception)
        {
            return new RuntimeManifestResult.Invalid(path, "not valid JSON: " + exception.Message);
        }

        using (document)
        {
            return Validate(path, document.RootElement, isWindows);
        }
    }

    private static RuntimeManifestResult Validate(string path, JsonElement root, bool isWindows)
    {
        RuntimeManifestResult Invalid(string reason) => new RuntimeManifestResult.Invalid(path, reason);

        if (root.ValueKind != JsonValueKind.Object)
        {
            return Invalid("the top level must be an object");
        }

        if (!root.TryGetProperty("version", out var version))
        {
            return Invalid("\"version\" is missing");
        }

        if (version.ValueKind != JsonValueKind.Number)
        {
            return Invalid("\"version\" must be a number");
        }

        if (!version.TryGetInt32(out var number) || number != 1)
        {
            return Invalid("unknown version " + version.GetRawText());
        }

        if (!root.TryGetProperty("runtimes", out var runtimes) || runtimes.ValueKind != JsonValueKind.Object || !runtimes.EnumerateObject().Any())
        {
            return Invalid("\"runtimes\" must be a non-empty object");
        }

        var directory = Path.GetFullPath(Path.GetDirectoryName(path)!);
        var prefix = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var executables = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var runtime in runtimes.EnumerateObject())
        {
            var rid = runtime.Name;
            if (rid.Length == 0)
            {
                return Invalid("a runtime identifier is empty");
            }

            if (executables.ContainsKey(rid))
            {
                return Invalid(rid + " is listed twice");
            }

            if (runtime.Value.ValueKind != JsonValueKind.String)
            {
                return Invalid($"the path for {rid} must be a string");
            }

            var entry = runtime.Value.GetString()!;
            if (entry.Length == 0)
            {
                return Invalid($"the path for {rid} is empty");
            }

            if (!IsRelative(entry, isWindows))
            {
                return Invalid($"path '{entry}' for {rid} is not relative");
            }

            if (entry.Split('/', '\\').Contains(".."))
            {
                return Invalid($"path '{entry}' for {rid} contains '..'");
            }

            var normalized = entry.Replace('/', Path.DirectorySeparatorChar);
            if (isWindows)
            {
                normalized = normalized.Replace('\\', Path.DirectorySeparatorChar);
            }

            // Defense in depth: relative paths without ".." can't leave the folder, so no test reaches this.
            string full;
            try
            {
                full = Path.GetFullPath(Path.Combine(directory, normalized));
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                // The entry comes from the manifest, so the file system's path rules can reject it (NUL, a colon, length).
                return Invalid($"path '{entry}' for {rid} is not a valid path");
            }

            if (!full.StartsWith(prefix, isWindows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            {
                return Invalid($"path '{entry}' for {rid} leaves the manifest's folder");
            }

            executables.Add(rid, full);
        }

        return new RuntimeManifestResult.Loaded(path, new RuntimeManifest(executables));
    }

    // Not fully qualified, and not relative to a drive (C:x) or to the current drive's root (\x) either.
    private static bool IsRelative(string entry, bool isWindows)
    {
        if (FullyQualifiedPath.IsFullyQualified(entry, isWindows))
        {
            return false;
        }

        return isWindows
            ? entry[0] is not ('\\' or '/') && !(entry.Length >= 2 && entry[1] == ':')
            : entry[0] != '/';
    }
}
