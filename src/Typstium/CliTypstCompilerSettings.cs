namespace Typstium;

/// <summary>Settings of the compiler itself, as opposed to per-request options.</summary>
public sealed record CliTypstCompilerSettings
{
    private int _maxConcurrency = Environment.ProcessorCount;
    private string _tempRoot = Path.Combine(Path.GetTempPath(), "typstium");
    private TimeSpan _orphanedWorkspaceAge = TimeSpan.FromHours(1);
    private TypstProcessPriority _processPriority = TypstProcessPriority.Normal;

    /// <summary>Normal priority, one Typst process per CPU, sandboxes in a <c>typstium</c> folder under the system temp folder, orphans removed after an hour.</summary>
    public static CliTypstCompilerSettings Default { get; } = new();

    /// <summary>The maximum number of Typst processes running at once.</summary>
    public int MaxConcurrency
    {
        get => _maxConcurrency;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _maxConcurrency = value;
        }
    }

    /// <summary>Where sandbox folders are created.</summary>
    public string TempRoot
    {
        get => _tempRoot;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _tempRoot = value;
        }
    }

    /// <summary>
    /// Scratch folders older than this, whose owner is gone, are deleted on the first compile (left behind by hard
    /// kills). A folder still in use, by this or another compiler sharing <see cref="TempRoot"/>, is never deleted
    /// whatever its age, so this needn't exceed any timeout; it only decides how soon leftovers go.
    /// </summary>
    public TimeSpan OrphanedWorkspaceAge
    {
        get => _orphanedWorkspaceAge;
        init
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The age must be positive.");
            }

            _orphanedWorkspaceAge = value;
        }
    }

    /// <summary>
    /// The scheduling priority of Typst processes, unless a request sets <see cref="TypstCompileOptions.ProcessPriority"/>.
    /// <see cref="TypstProcessPriority.BelowNormal"/> keeps a busy host responsive; see <see cref="TypstProcessPriority"/>.
    /// </summary>
    public TypstProcessPriority ProcessPriority
    {
        get => _processPriority;
        init
        {
            if (value is not (TypstProcessPriority.Normal or TypstProcessPriority.BelowNormal or TypstProcessPriority.Idle))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown process priority.");
            }

            _processPriority = value;
        }
    }
}
