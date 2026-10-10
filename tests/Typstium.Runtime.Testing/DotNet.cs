using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Typstium.Runtime.Testing;

/// <summary>The output of one dotnet (or msbuild.exe) run.</summary>
public sealed partial record DotNetRun(int ExitCode, string Output)
{
    /// <summary>Every TYPSTIUM diagnostic code in the output, distinct, in order of appearance.</summary>
    public IReadOnlyList<string> DiagnosticCodes =>
        CodePattern().Matches(Output).Select(match => match.Value).Distinct(StringComparer.Ordinal).ToArray();

    [GeneratedRegex(@"TYPSTIUM\d{4}")]
    private static partial Regex CodePattern();
}

/// <summary>Runs dotnet with node reuse off (so temp folders can be deleted) and with the given environment variables.</summary>
public static class DotNet
{
    public static Task<DotNetRun> RunAsync(string workingDirectory, IReadOnlyDictionary<string, string?> environment, params string[] arguments) =>
        RunToolAsync("dotnet", workingDirectory, environment, arguments);

    public static async Task<DotNetRun> RunToolAsync(string tool, string workingDirectory, IReadOnlyDictionary<string, string?> environment, params string[] arguments)
    {
        var start = new ProcessStartInfo(tool)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        foreach (var (name, value) in environment)
        {
            start.Environment[name] = value;
        }

        using var process = Process.Start(start)!;
        var output = new StringBuilder();
        process.OutputDataReceived += (_, line) => { if (line.Data is not null) { lock (output) { output.AppendLine(line.Data); } } };
        process.ErrorDataReceived += (_, line) => { if (line.Data is not null) { lock (output) { output.AppendLine(line.Data); } } };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        await process.WaitForExitAsync(timeout.Token);
        return new DotNetRun(process.ExitCode, output.ToString());
    }
}
