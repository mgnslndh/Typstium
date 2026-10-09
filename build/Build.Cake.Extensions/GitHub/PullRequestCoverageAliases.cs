using Build.Cake.Extensions.Coverage.Comparison;
using Build.Cake.Extensions.Coverage.Model;
using Cake.Common.Build;
using Cake.Common.Diagnostics;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Cake.Extensions.GitHub;

public static class PullRequestCoverageAliases
{
    // On a pull_request run: compares this run's coverage summary with the one from the pull request's merge base on
    // the base branch, then posts (or updates) one sticky comment and creates an advisory check run.
    // Baseline: the successful run of the workflow at the merge-base commit; if that has no summary (never built, or
    // the artifact expired), the latest successful push run of the base branch, and the comment says so.
    // Needs gh (GH_TOKEN with pull-requests: write, checks: write, actions: read) and full git history.
    public static CoverageComparison PublishPullRequestCoverage(this ICakeContext context, PullRequestCoverageSettings settings)
    {
        var gitHubActions = context.GitHubActions();
        if (!gitHubActions.IsRunningOnGitHubActions || !gitHubActions.Environment.PullRequest.IsPullRequest)
        {
            throw new CakeException("The coverage comparison runs on GitHub Actions, for pull_request events only.");
        }

        var workflow = gitHubActions.Environment.Workflow;
        var pullRequest = new PullRequestContext(workflow.Repository, workflow.BaseRef, PullRequestEvent.Parse(System.IO.File.ReadAllText(workflow.EventPath.FullPath)));
        context.Information(
            "Pull request #{0} in {1}: head {2} from {3} by {4}, base {5}",
            pullRequest.PullRequest.Number, pullRequest.Repository, pullRequest.PullRequest.HeadSha,
            pullRequest.PullRequest.HeadRepository ?? "(deleted)", pullRequest.PullRequest.Author, pullRequest.BaseRef);

        var cli = new GitHubCli(context);
        var work = context.Environment.GetSpecialPath(SpecialPath.LocalTemp).Combine("coverage-comparison-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(work.FullPath);
        try
        {
            return Publish(context, cli, pullRequest, settings, work);
        }
        finally
        {
            System.IO.Directory.Delete(work.FullPath, recursive: true);
        }
    }

    private static CoverageComparison Publish(ICakeContext context, GitHubCli cli, PullRequestContext pullRequest, PullRequestCoverageSettings settings, DirectoryPath work)
    {
        var (baseline, label) = FindBaseline(cli, pullRequest, settings, work);
        var comparison = CoverageComparer.Compare(CoverageSummary.Read(settings.SummaryPath.FullPath), baseline, label, settings.Comparison);
        StepSummary.TryAppend(context, comparison.Markdown);

        if (pullRequest.ReadOnlyReason is { } reason)
        {
            context.GitHubActions().Commands.Notice($"Coverage {comparison.Conclusion}, {comparison.Title}. This is {reason}, whose token is read-only, so the comparison is in the job summary only.");
            return comparison;
        }

        var repository = pullRequest.Repository;
        var comments = cli.Gh(new ProcessArgumentBuilder().Append("api").Append("--paginate").Append("--slurp").Append($"repos/{repository}/issues/{pullRequest.PullRequest.Number}/comments"));
        var commentBody = WriteJson(work, "comment.json", GitHubJson.CommentPayload(comparison.Markdown));
        var existing = GitHubJson.FindCommentStartingWith(comments, settings.Comparison.Marker);
        cli.Gh(existing is { } id
            ? new ProcessArgumentBuilder().Append("api").Append("--method").Append("PATCH").Append($"repos/{repository}/issues/comments/{id}").Append("--input").AppendQuoted(commentBody)
            : new ProcessArgumentBuilder().Append("api").Append("--method").Append("POST").Append($"repos/{repository}/issues/{pullRequest.PullRequest.Number}/comments").Append("--input").AppendQuoted(commentBody));

        // Advisory: nothing requires this check, so a failure is visible but doesn't block merging.
        var checkRun = WriteJson(work, "check-run.json", GitHubJson.CheckRunPayload(settings.CheckName, pullRequest.PullRequest.HeadSha, comparison.Conclusion, comparison.Title, comparison.Markdown));
        cli.Gh(new ProcessArgumentBuilder().Append("api").Append("--method").Append("POST").Append($"repos/{repository}/check-runs").Append("--input").AppendQuoted(checkRun));

        context.Information("Coverage comparison posted: {0}, {1}", comparison.Conclusion, comparison.Title);
        return comparison;
    }

    private static (CoverageSummary? Baseline, string Label) FindBaseline(GitHubCli cli, PullRequestContext pullRequest, PullRequestCoverageSettings settings, DirectoryPath work)
    {
        var baseRef = pullRequest.BaseRef;
        var mergeBase = cli.Git(new ProcessArgumentBuilder().Append("merge-base").Append($"origin/{baseRef}").Append(pullRequest.PullRequest.HeadSha)).Trim();
        var shortMergeBase = mergeBase[..Math.Min(7, mergeBase.Length)];

        foreach (var run in ListRuns(cli, pullRequest.Repository, settings.Workflow, "--commit", mergeBase, limit: 5))
        {
            if (Download(cli, pullRequest.Repository, run, settings.ArtifactName, work) is { } summary)
            {
                return (summary, $"`{baseRef}` at `{shortMergeBase}` (merge base)");
            }
        }

        foreach (var run in ListRuns(cli, pullRequest.Repository, settings.Workflow, "--branch", baseRef, limit: 10, pushOnly: true))
        {
            if (Download(cli, pullRequest.Repository, run, settings.ArtifactName, work) is { } summary)
            {
                return (summary, $"`{baseRef}` at `{run.HeadSha[..Math.Min(7, run.HeadSha.Length)]}` (latest; the merge base `{shortMergeBase}` has no coverage)");
            }
        }

        return (null, $"`{baseRef}`");
    }

    private static IReadOnlyList<WorkflowRun> ListRuns(GitHubCli cli, string repository, string workflow, string filter, string value, int limit, bool pushOnly = false)
    {
        var arguments = new ProcessArgumentBuilder().Append("run").Append("list").Append("--repo").Append(repository)
            .Append("--workflow").Append(workflow).Append(filter).Append(value);
        if (pushOnly)
        {
            arguments.Append("--event").Append("push");
        }

        arguments.Append("--status").Append("success").Append("--limit").Append(limit.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("--json").Append("databaseId,headSha");
        return WorkflowRun.ParseList(cli.Gh(arguments));
    }

    // A run's coverage summary, or null when the run has none (or it expired).
    private static CoverageSummary? Download(GitHubCli cli, string repository, WorkflowRun run, string artifactName, DirectoryPath work)
    {
        var target = work.Combine(run.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var downloaded = cli.TryGh(new ProcessArgumentBuilder().Append("run").Append("download").Append(run.Id.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Append("--repo").Append(repository).Append("--name").Append(artifactName).Append("--dir").AppendQuoted(target.FullPath));
        var path = target.CombineWithFilePath("summary.json").FullPath;
        return downloaded && System.IO.File.Exists(path) ? CoverageSummary.Read(path) : null;
    }

    private static string WriteJson(DirectoryPath work, string name, string json)
    {
        var path = work.CombineWithFilePath(name).FullPath;
        System.IO.File.WriteAllText(path, json);
        return path;
    }
}
