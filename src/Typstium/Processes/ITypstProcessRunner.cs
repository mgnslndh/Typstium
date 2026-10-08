namespace Typstium.Processes;

/// <summary>The seam between the pipeline and real processes; faked in unit tests.</summary>
internal interface ITypstProcessRunner
{
    Task<ProcessOutcome> RunAsync(ProcessSpec spec, CancellationToken cancellationToken);
}
