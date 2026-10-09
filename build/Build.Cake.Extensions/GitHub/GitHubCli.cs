using Cake.Common;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Cake.Extensions.GitHub;

// `gh` and `git` through Cake's process runner: stdout back as text, a CakeException with stderr on failure.
internal sealed class GitHubCli(ICakeContext context)
{
    public string Gh(ProcessArgumentBuilder arguments) => Run("gh", arguments);

    public string Git(ProcessArgumentBuilder arguments) => Run("git", arguments);

    // For calls that may fail by design, e.g. downloading an artifact that expired.
    public bool TryGh(ProcessArgumentBuilder arguments) => Execute("gh", arguments).ExitCode == 0;

    private string Run(string tool, ProcessArgumentBuilder arguments)
    {
        var (exitCode, output, error) = Execute(tool, arguments);
        if (exitCode != 0)
        {
            throw new CakeException($"{tool} {arguments.RenderSafe()} failed with exit code {exitCode}: {error}");
        }

        return output;
    }

    private (int ExitCode, string Output, string Error) Execute(string tool, ProcessArgumentBuilder arguments)
    {
        var executable = context.Tools.Resolve(OperatingSystem.IsWindows() ? tool + ".exe" : tool)
            ?? throw new CakeException($"{tool} isn't on PATH.");
        var settings = new ProcessSettings { Arguments = arguments, RedirectStandardOutput = true, RedirectStandardError = true, Silent = true };
        var exitCode = context.StartProcess(executable, settings, out var output, out var error);
        return (exitCode, string.Join('\n', output), string.Join('\n', error));
    }
}
