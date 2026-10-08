using Typstium.Utilities;

namespace Typstium.Workspace;

/// <summary>Creates the workspace for a request. The source kind decides in place vs sandbox.</summary>
internal sealed class WorkspaceFactory
{
    private readonly Lazy<string> _tempRoot;
    private readonly TimeSpan _orphanAge;
    private int _cleanupStarted;
    private int _cleanupRuns;

    public WorkspaceFactory(string tempRoot, TimeSpan orphanAge)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tempRoot);
        _orphanAge = orphanAge;
        _tempRoot = new Lazy<string>(() =>
        {
            Directory.CreateDirectory(tempRoot);
            return PathCanonicalizer.CanonicalizeDirectory(tempRoot);
        });
    }

    public string TempRoot => _tempRoot.Value;

    public int CleanupRuns => Volatile.Read(ref _cleanupRuns);

    public async Task<IWorkspace> CreateAsync(TypstCompileRequest request, CancellationToken cancellationToken)
    {
        CleanUpOnce();
        var paged = request.Output.IsPaged;
        switch (request.Source)
        {
            case TypstSource.FileSource file:
                return InPlaceWorkspace.Create(file, paged, TempRoot);

            case TypstSource.TemplateSource source:
                var template = await source.Provider.GetTemplateAsync(source.Name, cancellationToken).ConfigureAwait(false)
                    ?? throw new ArgumentException($"The template '{source.Name}' was not found by {source.Provider.GetType().Name}.", nameof(request));
                return await SandboxWorkspace.CreateForTemplateAsync(template, source.Files, paged, TempRoot, cancellationToken).ConfigureAwait(false);

            case TypstSource.MarkupSource markup:
                return await SandboxWorkspace.CreateForMarkupAsync(markup.Text, markup.Files, paged, TempRoot, cancellationToken).ConfigureAwait(false);

            default:
                throw new ArgumentException($"Unsupported source kind '{request.Source.GetType().Name}'.", nameof(request));
        }
    }

    private void CleanUpOnce()
    {
        if (Interlocked.Exchange(ref _cleanupStarted, 1) != 0)
        {
            return;
        }

        Interlocked.Increment(ref _cleanupRuns);
        OrphanedWorkspaceCleaner.Clean(TempRoot, _orphanAge, DateTime.UtcNow);
    }
}
