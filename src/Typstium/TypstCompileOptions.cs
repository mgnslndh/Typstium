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
    private TypstArguments _extraGlobalArguments = TypstArguments.Empty;
    private TypstArguments _extraArguments = TypstArguments.Empty;

    public static TypstCompileOptions Default { get; } = new();

    /// <summary>Tighter limits for templates written by end users.</summary>
    public static TypstCompileOptions Untrusted { get; } = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
        MaxOutputBytes = 20L * 1024 * 1024,
        MemoryLimitBytes = 1024L * 1024 * 1024,
    };

    public TypstFontOptions Fonts
    {
        get => _fonts;
        init => _fonts = value ?? throw new ArgumentNullException(nameof(value));
    }

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

    public TypstCreationTimestamp CreationTimestamp
    {
        get => _creationTimestamp;
        init => _creationTimestamp = value ?? throw new ArgumentNullException(nameof(value));
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
