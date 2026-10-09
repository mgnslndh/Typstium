namespace Typstium;

/// <summary>
/// The operating-system scheduling priority of the Typst process. Lower priority keeps the host app responsive
/// while CPU-heavy compiles run, at the cost of compile throughput when the CPU is saturated; on an idle machine
/// it makes no difference. Raising priority above normal isn't offered: it needs elevated rights on Unix.
/// </summary>
public enum TypstProcessPriority
{
    /// <summary>The same priority as any other process (Windows Normal; Unix nice 0). Typstium's default.</summary>
    Normal,

    /// <summary>Yields to normal-priority work (Windows BelowNormal; Unix nice 10). Suits web hosts.</summary>
    BelowNormal,

    /// <summary>Runs only when nothing else wants the CPU (Windows Idle; Unix nice 19). Suits background batch exports.</summary>
    Idle,
}
