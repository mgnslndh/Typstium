namespace Typstium;

/// <summary>Where a diagnostic's file came from. Sandbox paths never appear here.</summary>
public abstract record TypstSourceFile
{
    private TypstSourceFile()
    {
    }

    /// <summary>The text used in messages, e.g. <c>/license.typ</c> or <c>@preview/cmarker:0.1.10/lib.typ</c>.</summary>
    public abstract string DisplayPath { get; }

    /// <summary>A file of the project: a file-source root or a template bundle.</summary>
    /// <param name="Path">Root-relative path.</param>
    /// <param name="FullPath">Absolute path on disk; set only for file sources compiled in place.</param>
    public sealed record Project(TypstPath Path, string? FullPath) : TypstSourceFile
    {
        /// <inheritdoc/>
        public override string DisplayPath => FullPath ?? Path.RootRelative;
    }

    /// <summary>A <c>TypstVirtualFile</c> from the request.</summary>
    public sealed record Virtual(TypstPath Path) : TypstSourceFile
    {
        /// <inheritdoc/>
        public override string DisplayPath => Path.RootRelative;
    }

    /// <summary>A file inside a Typst package, e.g. spec <c>@preview/cmarker:0.1.10</c>, path <c>lib.typ</c>.</summary>
    public sealed record Package(string Spec, TypstPath Path) : TypstSourceFile
    {
        /// <inheritdoc/>
        public override string DisplayPath => Spec + Path.RootRelative;
    }

    /// <summary>Inline markup from <c>TypstSource.Markup</c>.</summary>
    public sealed record Markup : TypstSourceFile
    {
        private Markup()
        {
        }

        /// <summary>The single instance.</summary>
        public static Markup Instance { get; } = new();

        /// <inheritdoc/>
        public override string DisplayPath => "<markup>";
    }
}
