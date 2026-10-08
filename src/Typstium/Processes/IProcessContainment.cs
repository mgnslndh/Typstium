using System.Diagnostics;

namespace Typstium.Processes;

/// <summary>Keeps a process (and anything it starts) from outliving the compile.</summary>
internal interface IProcessContainment : IDisposable
{
    void Attach(Process process);

    /// <summary>Kills every contained process, including grandchildren that hold our pipes.</summary>
    void Terminate();
}
