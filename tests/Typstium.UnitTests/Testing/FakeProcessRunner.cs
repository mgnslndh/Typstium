using System.Collections.Concurrent;
using System.Text;
using Typstium.Processes;

namespace Typstium.UnitTests.Testing;

/// <summary>Answers process runs from a callback and records every spec.</summary>
internal sealed class FakeProcessRunner : ITypstProcessRunner
{
    private readonly Func<ProcessSpec, ProcessOutcome> _respond;
    private readonly ConcurrentQueue<ProcessSpec> _specs = new();
    private int _concurrency;
    private int _maxConcurrency;

    public FakeProcessRunner(Func<ProcessSpec, ProcessOutcome> respond) => _respond = respond;

    public TimeSpan Delay { get; init; }

    public IReadOnlyList<ProcessSpec> Specs => _specs.ToArray();

    public static ProcessOutcome Exited(int exitCode, string stdout = "", string stderr = "") =>
        new ProcessOutcome.Exited(exitCode, Encoding.UTF8.GetBytes(stdout), TimeSpan.FromMilliseconds(5), stderr);

    public static bool IsInfo(ProcessSpec spec) => spec.Arguments.IndexOf("info") >= 0;

    public static string InfoJson(string version, bool html = true) =>
        "{\"version\":\"" + version + "\",\"features\":{" + (html ? "\"html\":false" : string.Empty) + "}}";

    public int MaxObservedConcurrency => Volatile.Read(ref _maxConcurrency);

    public async Task<ProcessOutcome> RunAsync(ProcessSpec spec, CancellationToken cancellationToken)
    {
        _specs.Enqueue(spec);
        var current = Interlocked.Increment(ref _concurrency);
        int observed;
        while (current > (observed = Volatile.Read(ref _maxConcurrency)) && Interlocked.CompareExchange(ref _maxConcurrency, current, observed) != observed)
        {
        }

        try
        {
            if (Delay > TimeSpan.Zero)
            {
                await Task.Delay(Delay, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return _respond(spec);
        }
        finally
        {
            Interlocked.Decrement(ref _concurrency);
        }
    }
}
