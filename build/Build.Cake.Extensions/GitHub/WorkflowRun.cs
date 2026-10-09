using System.Text.Json;

namespace Build.Cake.Extensions.GitHub;

// One row of `gh run list --json databaseId,headSha`.
public sealed record WorkflowRun(long Id, string HeadSha)
{
    public static IReadOnlyList<WorkflowRun> ParseList(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateArray()
            .Select(run => new WorkflowRun(run.GetProperty("databaseId").GetInt64(), run.GetProperty("headSha").GetString() ?? ""))
            .ToList();
    }
}
