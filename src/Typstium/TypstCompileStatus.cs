namespace Typstium;

/// <summary>How a compile ended.</summary>
public enum TypstCompileStatus
{
    /// <summary>Typst produced the document. There may still be warnings.</summary>
    Succeeded,

    /// <summary>Typst reported errors; see <see cref="TypstCompileResult.Errors"/>.</summary>
    Failed,

    /// <summary>Typst ran longer than <see cref="TypstCompileOptions.Timeout"/> and was stopped.</summary>
    TimedOut,

    /// <summary>The output was larger than <see cref="TypstCompileOptions.MaxOutputBytes"/>.</summary>
    OutputLimitExceeded,
}
