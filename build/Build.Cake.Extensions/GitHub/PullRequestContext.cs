namespace Build.Cake.Extensions.GitHub;

// Repository and BaseRef come from the workflow environment (GITHUB_REPOSITORY, GITHUB_BASE_REF); the rest from the
// event payload.
public sealed record PullRequestContext(string Repository, string BaseRef, PullRequestEvent PullRequest)
{
    // Pull requests from forks, deleted forks and Dependabot run with a read-only token: they can't comment or create
    // check runs. Null when the token can write.
    public string? ReadOnlyReason =>
        PullRequest.HeadRepository is null ? "a pull request from a deleted fork"
        : !string.Equals(PullRequest.HeadRepository, Repository, StringComparison.OrdinalIgnoreCase) ? $"a pull request from a fork ({PullRequest.HeadRepository})"
        : string.Equals(PullRequest.Author, "dependabot[bot]", StringComparison.OrdinalIgnoreCase) ? "a Dependabot pull request"
        : null;
}
