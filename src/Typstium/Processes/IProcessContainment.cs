using System.Diagnostics;

namespace Typstium.Processes;

/// <summary>Keeps a process (and anything it starts) from outliving the compile.</summary>
internal interface IProcessContainment : IDisposable
{
    void Attach(Process process);

    /// <summary>Whether <see cref="Terminate"/> kills the whole process tree, so the runner needn't walk it.</summary>
    bool TerminatesTree { get; }

    /// <summary>Kills every contained process, including grandchildren that hold our pipes.</summary>
    void Terminate();
}
