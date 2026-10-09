using Build.Cake.Extensions.Coverage.Comparison;
using Cake.Core.IO;

namespace Build.Cake.Extensions.GitHub;

// Workflow: the workflow file whose runs hold baselines (e.g. ci.yml). ArtifactName: the artifact holding a run's
// summary.json. CheckName: the check run created on the pull request's head.
public sealed record PullRequestCoverageSettings(
    FilePath SummaryPath,
    string Workflow,
    string ArtifactName,
    string CheckName,
    CoverageComparisonOptions Comparison);
