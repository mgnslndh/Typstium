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
        var limit = TimeSpan.FromMinutes(10);
        using var timeout = new CancellationTokenSource(limit);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            // A hung build would otherwise keep running, holding the temp folder, after the test fails.
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            string captured;
            lock (output)
            {
                captured = output.ToString();
            }

            throw new TimeoutException($"{tool} {string.Join(' ', arguments)} didn't finish within {limit.TotalMinutes} minutes. Output so far:\n{captured}");
        }

        lock (output)
        {
            return new DotNetRun(process.ExitCode, output.ToString());
        }
    }
}
