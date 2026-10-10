using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Build.Cake.Extensions.Downloads;

// One file to download, pinned by its SHA-256 (lowercase hex).
public sealed record PinnedAsset(string Name, string Sha256);

// A committed lock file: the assets a build downloads, by key (for example a runtime identifier), with the hashes that
// were reviewed. Only an update task rewrites it; downloads verify against it.
public sealed partial record PinnedAssets(string Version, IReadOnlyDictionary<string, PinnedAsset> Assets)
{
    public static PinnedAssets Parse(string json)
    {
        using var document = ParseDocument(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("The lock file must be a JSON object.");
        }

        var version = root.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } text
            ? text
            : throw new InvalidDataException("The lock file needs a non-empty \"version\" string.");
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("The lock file needs an \"assets\" object.");
        }

        var result = new SortedDictionary<string, PinnedAsset>(StringComparer.Ordinal);
        foreach (var asset in assets.EnumerateObject())
        {
            if (result.ContainsKey(asset.Name))
            {
                throw new InvalidDataException($"Asset '{asset.Name}' appears more than once.");
            }

            var name = StringProperty(asset, "name");
            var sha256 = StringProperty(asset, "sha256");
            if (name.Length == 0 || name.IndexOfAny(['/', '\\']) >= 0)
            {
                throw new InvalidDataException($"Asset '{asset.Name}' needs a plain file name, not '{name}'.");
            }

            if (!Sha256Pattern().IsMatch(sha256))
            {
                throw new InvalidDataException($"Asset '{asset.Name}' needs a sha256 of 64 lowercase hex digits, not '{sha256}'.");
            }

            result.Add(asset.Name, new PinnedAsset(name, sha256));
        }

        return new PinnedAssets(version, result);
    }

    public string ToJson()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, IndentSize = 2, NewLine = "\n" }))
        {
            writer.WriteStartObject();
            writer.WriteString("version", Version);
            writer.WriteStartObject("assets");
            foreach (var (key, asset) in Assets.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                writer.WriteStartObject(key);
                writer.WriteString("name", asset.Name);
                writer.WriteString("sha256", asset.Sha256);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    private static JsonDocument ParseDocument(string json)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"The lock file isn't valid JSON: {exception.Message}", exception);
        }
    }

    private static string StringProperty(JsonProperty asset, string property)
    {
        if (asset.Value.ValueKind != JsonValueKind.Object || !asset.Value.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"Asset '{asset.Name}' needs a \"{property}\" string.");
        }

        return value.GetString() ?? "";
    }

    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex Sha256Pattern();
}
