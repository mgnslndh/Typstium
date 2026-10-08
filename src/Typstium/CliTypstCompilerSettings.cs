namespace Typstium;

/// <summary>Settings of the compiler itself, as opposed to per-request options.</summary>
public sealed record CliTypstCompilerSettings
{
    private int _maxConcurrency = Environment.ProcessorCount;
    private string _tempRoot = Path.Combine(Path.GetTempPath(), "typstium");
    private TimeSpan _orphanedWorkspaceAge = TimeSpan.FromHours(1);

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

    /// <summary>Sandbox folders older than this are deleted on the first compile (left behind by hard kills).</summary>
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
}
