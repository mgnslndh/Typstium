namespace Typstium.Workspace;

/// <summary>The folders for one compile. Disposing removes anything Typstium created.</summary>
internal interface IWorkspace : IAsyncDisposable
{
    WorkspaceLayout Layout { get; }
}
