using System.Text.Json;

namespace Build.Cake.Extensions.GitHub;

// The parts of a pull_request event payload (GITHUB_EVENT_PATH) the coverage comparison needs. HeadSha is the pull
// request's head, not GITHUB_SHA (the merge commit). HeadRepository is null when the fork was deleted.
public sealed record PullRequestEvent(int Number, string HeadSha, string? HeadRepository, string Author)
{
    public static PullRequestEvent Parse(string eventJson)
    {
        using var document = JsonDocument.Parse(eventJson);
        var pullRequest = document.RootElement.GetProperty("pull_request");
        var head = pullRequest.GetProperty("head");
        var repository = head.GetProperty("repo");
        return new PullRequestEvent(
            pullRequest.GetProperty("number").GetInt32(),
            head.GetProperty("sha").GetString() ?? "",
            repository.ValueKind == JsonValueKind.Object ? repository.GetProperty("full_name").GetString() : null,
            pullRequest.GetProperty("user").GetProperty("login").GetString() ?? "");
    }
}
