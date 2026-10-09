using Cake.Common.Build;
using Cake.Core;

namespace Build.Cake.Extensions.GitHub;

internal static class StepSummary
{
    // Appends to the GitHub Actions job summary when there is one: emulators such as act set GITHUB_ACTIONS without
    // GITHUB_STEP_SUMMARY, where Cake's SetStepSummary would throw.
    public static void TryAppend(ICakeContext context, string markdown)
    {
        var gitHubActions = context.GitHubActions();
        if (gitHubActions.IsRunningOnGitHubActions && gitHubActions.Environment.Runtime.StepSummary is not null)
        {
            gitHubActions.Commands.SetStepSummary(markdown);
        }
    }
}
