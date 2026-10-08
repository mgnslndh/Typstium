using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text;
using Typstium.CommandLine;
using Typstium.Diagnostics;
using Typstium.Processes;

namespace Typstium.Capabilities;

/// <summary>
/// The only code that compares Typst versions. Probes each executable once with `typst info` and picks
/// the profile for its version range; newer unknown versions get the newest profile.
/// </summary>
internal sealed class TypstCliProfileFactory
{
    internal static readonly TypstVersion MinimumVersion = new(0, 15, 0);

    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(30);

    // One row per supported range, oldest first. Add a row when a Typst release needs different components.
    private static readonly (TypstVersion From, Func<TypstInfo, TypstCliProfile> Create)[] Profiles =
    [
        (new TypstVersion(0, 15, 0), CreateV015),
    ];

    private readonly ITypstProcessRunner _runner;
    private readonly ConcurrentDictionary<ProfileKey, Lazy<Task<TypstCliProfile>>> _cache = new();
    private int _probeCount;

    private readonly TypstProcessPriority _priority;

    public TypstCliProfileFactory(ITypstProcessRunner runner, TypstProcessPriority priority = TypstProcessPriority.Normal)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _priority = priority;
    }

    public int ProbeCount => Volatile.Read(ref _probeCount);

    public async Task<TypstCliProfile> GetProfileAsync(TypstExecutable executable, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(executable);
        var key = ProfileKey.For(executable.Path);
        var entry = _cache.GetOrAdd(key, _ => CreateEntry(key, executable));

        // The probe is shared and evicts itself on failure, so one caller's cancellation neither cancels it
        // for others nor leaves a stale failure behind.
        return await entry.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static TypstCliProfile CreateProfile(TypstInfo info)
    {
        if (info.Version < MinimumVersion)
        {
            throw new TypstFeatureNotSupportedException("Typstium", info.Version, MinimumVersion);
        }

        var row = Profiles.Last(profile => info.Version >= profile.From);
        return row.Create(info);
    }

    private static TypstCliProfile CreateV015(TypstInfo info) => new(
        new TypstCapabilities(info.Version, info.Features.Contains("html"), PdfStandard.KnownInTypst015.ToImmutableHashSet()),
        new CompileArgumentsBuilderV015(),
        new HumanDiagnosticParserV015(),
        new DiagnosticClassifierV015());

    private Lazy<Task<TypstCliProfile>> CreateEntry(ProfileKey key, TypstExecutable executable)
    {
        Lazy<Task<TypstCliProfile>>? entry = null;
        entry = new Lazy<Task<TypstCliProfile>>(() => ProbeAndEvictOnFailureAsync(key, entry!, executable));
        return entry;
    }

    private async Task<TypstCliProfile> ProbeAndEvictOnFailureAsync(ProfileKey key, Lazy<Task<TypstCliProfile>> entry, TypstExecutable executable)
    {
        try
        {
            return await ProbeAsync(executable).ConfigureAwait(false);
        }
        catch
        {
            // Failures are not cached, whether or not any caller is still waiting.
            ((ICollection<KeyValuePair<ProfileKey, Lazy<Task<TypstCliProfile>>>>)_cache).Remove(new KeyValuePair<ProfileKey, Lazy<Task<TypstCliProfile>>>(key, entry));
            throw;
        }
    }

    private async Task<TypstCliProfile> ProbeAsync(TypstExecutable executable)
    {
        Interlocked.Increment(ref _probeCount);
        var outcome = await RunAsync(executable, TypstArguments.Of("--color", "never", "info", "--format", "json")).ConfigureAwait(false);
        switch (outcome)
        {
            case ProcessOutcome.Exited { ExitCode: 0 } info:
                return CreateProfile(TypstInfoParser.Parse(Utf8(info.StandardOutput)));

            case ProcessOutcome.Exited { ExitCode: 2 }:
                // Typst older than 0.15 has no `info` subcommand.
                var versionOutcome = await RunAsync(executable, TypstArguments.Of("--version")).ConfigureAwait(false);
                if (versionOutcome is ProcessOutcome.Exited { ExitCode: 0 } version)
                {
                    throw new TypstFeatureNotSupportedException("Typstium", TypstInfoParser.ParseVersionOutput(Utf8(version.StandardOutput)), MinimumVersion);
                }

                throw new TypstProcessOutputException("'typst info' is not supported and 'typst --version' failed.");

            case ProcessOutcome.Exited crashed:
                throw new TypstProcessCrashedException(crashed.ExitCode, crashed.StandardError);

            default:
                throw new TypstProcessOutputException($"'typst info' did not finish within {ProbeTimeout.TotalSeconds:0} seconds.");
        }
    }

    private Task<ProcessOutcome> RunAsync(TypstExecutable executable, TypstArguments arguments) =>
        _runner.RunAsync(
            new ProcessSpec(
                executable.Path,
                arguments,
                ProcessEnvironmentBuilder.BuildForProbe(ProcessEnvironmentBuilder.CurrentVariableNames()),
                Path.GetTempPath(),
                ReadOnlyMemory<byte>.Empty,
                ProbeTimeout,
                MaxStdoutBytes: 1024 * 1024,
                ProcessSpec.DefaultMaxStderrBytes,
                MemoryLimitBytes: null,
                _priority),
            CancellationToken.None);

    private static string Utf8(ReadOnlyMemory<byte> bytes) => Encoding.UTF8.GetString(bytes.ToArray());

    /// <summary>Identifies an executable file version: a replaced binary gets a new probe.</summary>
    private readonly record struct ProfileKey(string Path, long Length, DateTime LastWriteUtc)
    {
        public static ProfileKey For(string path)
        {
            var file = new FileInfo(path);
            return file.Exists ? new ProfileKey(file.FullName, file.Length, file.LastWriteTimeUtc) : new ProfileKey(path, -1, DateTime.MinValue);
        }
    }
}
