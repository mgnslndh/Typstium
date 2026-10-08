using System.Collections.Immutable;

namespace Typstium;

/// <summary>
/// What to compile. Exactly one of three kinds; the kind decides where --root points (spec 4.3):
/// file sources compile in place, template and markup sources compile in a sandbox.
/// </summary>
public abstract record TypstSource
{
    private TypstSource()
    {
    }

    /// <summary>A .typ file on disk, compiled in place. The root is the file's folder.</summary>
    public static TypstSource File(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new FileSource(path, root: null);
    }

    /// <summary>A .typ file on disk, compiled in place with an explicit project root.</summary>
    public static TypstSource File(string path, string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        return new FileSource(path, root);
    }

    public static TypstSource Template(ITemplateProvider provider, TypstTemplateName name) =>
        Template(provider, name, ImmutableArray<TypstVirtualFile>.Empty);

    public static TypstSource Template(ITemplateProvider provider, TypstTemplateName name, ImmutableArray<TypstVirtualFile> files)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(name);
        return new TemplateSource(provider, name, VirtualFileSet.Validate(files, nameof(files)));
    }

    public static TypstSource Markup(string markup) => Markup(markup, ImmutableArray<TypstVirtualFile>.Empty);

    public static TypstSource Markup(string markup, ImmutableArray<TypstVirtualFile> files)
    {
        ArgumentNullException.ThrowIfNull(markup);
        return new MarkupSource(markup, VirtualFileSet.Validate(files, nameof(files)));
    }

    public sealed record FileSource : TypstSource
    {
        internal FileSource(string path, string? root)
        {
            Path = path;
            Root = root;
        }

        public string Path { get; }

        public string? Root { get; }
    }

    public sealed record TemplateSource : TypstSource
    {
        internal TemplateSource(ITemplateProvider provider, TypstTemplateName name, ImmutableArray<TypstVirtualFile> files)
        {
            Provider = provider;
            Name = name;
            Files = files;
        }

        public ITemplateProvider Provider { get; }

        public TypstTemplateName Name { get; }

        public ImmutableArray<TypstVirtualFile> Files { get; }
    }

    public sealed record MarkupSource : TypstSource
    {
        internal MarkupSource(string text, ImmutableArray<TypstVirtualFile> files)
        {
            Text = text;
            Files = files;
        }

        /// <summary>The Typst markup, sent to Typst over stdin.</summary>
        public string Text { get; }

        public ImmutableArray<TypstVirtualFile> Files { get; }
    }
}
