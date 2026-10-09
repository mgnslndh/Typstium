namespace Typstium;

/// <summary>How to compile and under which policy. Configured once and shared by many requests.</summary>
public sealed record TypstCompileOptions
{
    private TypstFontOptions _fonts = TypstFontOptions.Default;
    private TypstPackageOptions _packages = TypstPackageOptions.Default;
    private TimeSpan _timeout = TimeSpan.FromSeconds(60);
    private long _maxOutputBytes = 100L * 1024 * 1024;
    private long? _memoryLimitBytes;
    private TypstCreationTimestamp _creationTimestamp = TypstCreationTimestamp.Inherit;
    private int? _jobs;
    private TypstProcessPriority? _processPriority;
    private TypstArguments _extraGlobalArguments = TypstArguments.Empty;
    private TypstArguments _extraArguments = TypstArguments.Empty;

    /// <summary>No system fonts, network blocked, 60 s timeout, 100 MB output cap, no memory cap.</summary>
    public static TypstCompileOptions Default { get; } = new();

    /// <summary>Tighter limits for templates written by end users.</summary>
    public static TypstCompileOptions Untrusted { get; } = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
        MaxOutputBytes = 20L * 1024 * 1024,
        MemoryLimitBytes = 1024L * 1024 * 1024,
    };

    /// <summary>Which fonts Typst may use.</summary>
    public TypstFontOptions Fonts
    {
        get => _fonts;
        init => _fonts = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>Where packages come from and whether Typst may download them.</summary>
    public TypstPackageOptions Packages
    {
        get => _packages;
        init => _packages = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>Wall-clock limit for the Typst process. Use Timeout.InfiniteTimeSpan to disable.</summary>
    public TimeSpan Timeout
    {
        get => _timeout;
        init
        {
            if (value != System.Threading.Timeout.InfiniteTimeSpan && value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The timeout must be positive or Timeout.InfiniteTimeSpan.");
            }

            _timeout = value;
        }
    }

    /// <summary>The most output a compile may produce, summed over all pages. More ends the compile with
    /// <see cref="TypstCompileStatus.OutputLimitExceeded"/>.</summary>
    public long MaxOutputBytes
    {
        get => _maxOutputBytes;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _maxOutputBytes = value;
        }
    }

    /// <summary>Memory cap enforced with a Windows job object. Ignored on other platforms.</summary>
    public long? MemoryLimitBytes
    {
        get => _memoryLimitBytes;
        init
        {
            if (value is { } limit)
            {
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit, nameof(value));
            }

            _memoryLimitBytes = value;
        }
    }

    /// <summary>The creation timestamp written into the document.</summary>
    public TypstCreationTimestamp CreationTimestamp
    {
        get => _creationTimestamp;
        init => _creationTimestamp = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// The scheduling priority of the Typst process for this request. Null (the default) uses the compiler's
    /// <see cref="CliTypstCompilerSettings.ProcessPriority"/>; see <see cref="TypstProcessPriority"/> for the trade-off.
    /// </summary>
    public TypstProcessPriority? ProcessPriority
    {
        get => _processPriority;
        init
        {
            if (value is { } priority && priority is not (TypstProcessPriority.Normal or TypstProcessPriority.BelowNormal or TypstProcessPriority.Idle))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown process priority.");
            }

            _processPriority = value;
        }
    }

    /// <summary>--jobs. Null leaves Typst's default (number of CPUs).</summary>
    public int? Jobs
    {
        get => _jobs;
        init
        {
            if (value is { } jobs)
            {
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(jobs, nameof(value));
            }

            _jobs = value;
        }
    }

    /// <summary>Arguments placed before the subcommand, e.g. global Typst flags.</summary>
    public TypstArguments ExtraGlobalArguments
    {
        get => _extraGlobalArguments;
        init => _extraGlobalArguments = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>Arguments appended after the generated ones.</summary>
    public TypstArguments ExtraArguments
    {
        get => _extraArguments;
        init => _extraArguments = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Receives the complete generated argument list and may change anything. It can undo safety
    /// guarantees (--root, font flags); use with care.
    /// </summary>
    public Func<TypstArguments, TypstArguments>? ArgumentCustomization { get; init; }
}
