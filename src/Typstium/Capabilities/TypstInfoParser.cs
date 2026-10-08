using System.Collections.Immutable;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Typstium.Capabilities;

internal static class TypstInfoParser
{
    private static readonly Regex VersionOutput = new("^typst (?<version>\\S+)", RegexOptions.CultureInvariant);

    public static TypstInfo Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("version", out var versionElement)
                || versionElement.ValueKind != JsonValueKind.String
                || !TypstVersion.TryParse(versionElement.GetString(), out var version))
            {
                throw new TypstProcessOutputException("'typst info --format json' did not report a version.");
            }

            var features = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
            if (root.TryGetProperty("features", out var featuresElement) && featuresElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var feature in featuresElement.EnumerateObject())
                {
                    features.Add(feature.Name);
                }
            }

            return new TypstInfo(version, features.ToImmutable());
        }
        catch (JsonException exception)
        {
            throw new TypstProcessOutputException("'typst info --format json' returned invalid JSON.", exception);
        }
    }

    public static TypstVersion ParseVersionOutput(string text)
    {
        var match = VersionOutput.Match(text.Trim());
        if (!match.Success || !TypstVersion.TryParse(match.Groups["version"].Value, out var version))
        {
            throw new TypstProcessOutputException($"Could not read a version from 'typst --version' output: '{text.Trim()}'.");
        }

        return version;
    }
}
