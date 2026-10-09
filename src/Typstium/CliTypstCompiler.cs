using System.Diagnostics;
using Typstium.Capabilities;
using Typstium.CommandLine;
using Typstium.Compilation;
using Typstium.Diagnostics;
using Typstium.Processes;
using Typstium.Workspace;

namespace Typstium;

/// <summary>Compiles by running the official Typst CLI, one process per compile (spec 5).</summary>
public sealed class CliTypstCompiler : ITypstCompiler
{
    private readonly ITypstExecutableResolver _resolver;
    private readonly ITypstProcessRunner _runner;
    private readonly TypstCliProfileFactory _profiles;
    private readonly WorkspaceFactory _workspaces;
    private readonly SemaphoreSlim _concurrency;
    private readonly TypstProcessPriority _processPriority;

    /// <summary>Creates a compiler. Typst is located and probed on the first compile, not here.</summary>
    /// <param name="resolver">Finds the Typst executable.</param>
    /// <param name="settings">Concurrency and sandbox settings.</param>
    public CliTypstCompiler(ITypstExecutableResolver resolver, CliTypstCompilerSettings settings)
        : this(resolver, settings, new TypstProcessRunner())
    {
    }

    internal CliTypstCompiler(ITypstExecutableResolver resolver, CliTypstCompilerSettings settings, ITypstProcessRunner runner)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        ArgumentNullException.ThrowIfNull(settings);
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _processPriority = settings.ProcessPriority;
        _profiles = new TypstCliProfileFactory(runner, settings.ProcessPriority);
        _workspaces = new WorkspaceFactory(settings.TempRoot, settings.OrphanedWorkspaceAge);
        _concurrency = new SemaphoreSlim(settings.MaxConcurrency, settings.MaxConcurrency);
    }

    /// <inheritdoc/>
    public async Task<TypstCompileResult> CompileAsync(TypstCompileRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequestValidator.Validate(request);
        var total = Stopwatch.StartNew();
        using var activity = TypstTelemetry.StartCompile(request);
        try
        {
            var result = await CompileCoreAsync(request, total, cancellationToken).ConfigureAwait(false);
            TypstTelemetry.Complete(activity, result);
            return result;
        }
        catch (Exception exception)
        {
            TypstTelemetry.Fail(activity, exception);
            throw;
        }
    }

    private async Task<TypstCompileResult> CompileCoreAsync(TypstCompileRequest request, Stopwatch total, CancellationToken cancellationToken)
    {
        var executable = await _resolver.ResolveAsync(cancellationToken).ConfigureAwait(false);
        var profile = await _profiles.GetProfileAsync(executable, cancellationToken).ConfigureAwait(false);
        CapabilityValidator.Validate(request, profile.Capabilities);

        // The slot covers preparing the workspace too, so a queued request holds no scratch folder and none ages
        // while waiting. Folders exist only for running compiles and, briefly, ones finishing up.
        await _concurrency.WaitAsync(cancellationToken).ConfigureAwait(false);
        var holdsSlot = true;
        IWorkspace? workspace = null;
        try
        {
            workspace = await _workspaces.CreateAsync(request, cancellationToken).ConfigureAwait(false);
            var layout = workspace.Layout;
            var arguments = profile.ArgumentsBuilder.Build(new CompileArgumentsInput(request, layout, profile.Capabilities));
            CommandLineLimits.EnsureFits(executable.Path, arguments);

            var spec = new ProcessSpec(
                executable.Path,
                arguments,
                ProcessEnvironmentBuilder.Build(ProcessEnvironmentBuilder.CurrentVariableNames(), request.Options),
                layout.RootDirectory,
                layout.Stdin,
                request.Options.Timeout,
                request.Options.MaxOutputBytes,
                ProcessSpec.DefaultMaxStderrBytes,
                request.Options.MemoryLimitBytes,
                request.Options.ProcessPriority ?? _processPriority);

            ProcessOutcome outcome;
            try
            {
                outcome = await _runner.RunAsync(spec, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _concurrency.Release();
                holdsSlot = false;
            }

            var diagnostics = DiagnosticAssembler.Assemble(
                outcome.StandardError, profile.DiagnosticParser, profile.DiagnosticClassifier, new SourcePathMapper(layout.Mapping), request.Options);
            var commandLine = WindowsArgumentEscaper.Escape(executable.Path) + " " + arguments.RenderSafe();
            var invocation = new TypstInvocation(executable.Path, executable.FoundBy, profile.Capabilities.Version, commandLine, outcome.Duration, total.Elapsed);
            return OutcomeMapper.Map(outcome, diagnostics, request, layout, commandLine, invocation);
        }
        finally
        {
            if (holdsSlot)
            {
                _concurrency.Release();
            }

            if (workspace is not null)
            {
                await workspace.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
