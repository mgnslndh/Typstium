using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Build.Cake.Extensions.Coverage.Model;

public sealed record NamespaceSummary(string Name, CoverageCounts Counts);

// The counts a pull request is compared by: totals and per namespace. Written as artifacts/coverage/summary.json and
// kept by CI as the baseline of later runs, so the format is fixed: the PowerShell report's, which baselines from
// before the move to Cake still use.
public sealed record CoverageSummary(CoverageCounts Totals, IReadOnlyList<NamespaceSummary> Namespaces)
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public string ToJson()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("totals");
            WriteCounts(writer, name: null, Totals);
            writer.WriteStartArray("namespaces");
            foreach (var entry in Namespaces)
            {
                WriteCounts(writer, entry.Name, entry.Counts);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public void Write(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, ToJson() + "\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public static CoverageSummary Read(string path) => FromJson(File.ReadAllText(path));

    // PowerShell's ConvertTo-Json writes a one-element array as a bare object; both shapes are read.
    public static CoverageSummary FromJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var namespaces = root.TryGetProperty("namespaces", out var element) ? element : default;
        var entries = namespaces.ValueKind switch
        {
            JsonValueKind.Array => namespaces.EnumerateArray().ToList(),
            JsonValueKind.Object => [namespaces],
            _ => [],
        };

        return new CoverageSummary(
            ReadCounts(root.GetProperty("totals")),
            entries.Select(entry => new NamespaceSummary(entry.GetProperty("name").GetString() ?? "", ReadCounts(entry))).ToList());
    }

    private static void WriteCounts(Utf8JsonWriter writer, string? name, CoverageCounts counts)
    {
        writer.WriteStartObject();
        if (name is not null)
        {
            writer.WriteString("name", name);
        }

        writer.WriteNumber("coveredLines", counts.CoveredLines);
        writer.WriteNumber("lines", counts.Lines);
        writer.WriteNumber("coveredBranches", counts.CoveredBranches);
        writer.WriteNumber("branches", counts.Branches);
        writer.WriteNumber("coveredMethods", counts.CoveredMethods);
        writer.WriteNumber("methods", counts.Methods);
        writer.WriteEndObject();
    }

    private static CoverageCounts ReadCounts(JsonElement element) => new(
        element.GetProperty("coveredLines").GetInt32(),
        element.GetProperty("lines").GetInt32(),
        element.GetProperty("coveredBranches").GetInt32(),
        element.GetProperty("branches").GetInt32(),
        element.GetProperty("coveredMethods").GetInt32(),
        element.GetProperty("methods").GetInt32());
}
