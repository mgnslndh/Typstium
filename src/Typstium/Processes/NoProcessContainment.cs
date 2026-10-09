using System.Diagnostics;

namespace Typstium.Processes;

/// <summary>Non-Windows: no job objects; the runner kills the process tree itself.</summary>
internal sealed class NoProcessContainment : IProcessContainment
{
    public static NoProcessContainment Instance { get; } = new();

    public void Attach(Process process)
    {
    }

    public void Terminate()
    {
    }

    public void Dispose()
    {
    }
}
