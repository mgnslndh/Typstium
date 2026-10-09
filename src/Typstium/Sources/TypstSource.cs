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

    /// <summary>A template from a provider, compiled in a fresh sandbox holding only the template's files.</summary>
    /// <remarks>The provider is called on every compile, so a changed template is picked up.</remarks>
    public static TypstSource Template(ITemplateProvider provider, TypstTemplateName name) =>
        Template(provider, name, ImmutableArray<TypstVirtualFile>.Empty);

    /// <summary>A template from a provider plus virtual files, compiled in a fresh sandbox.</summary>
    /// <param name="provider">Supplies the template.</param>
    /// <param name="name">The template's name.</param>
    /// <param name="files">Files written into the sandbox after the template's files. A virtual file with the same path as a template
    /// file replaces it. A default array means none.</param>
    /// <exception cref="ArgumentException"><paramref name="files"/> contains null, or two paths that are equal or differ only by case.</exception>
    public static TypstSource Template(ITemplateProvider provider, TypstTemplateName name, ImmutableArray<TypstVirtualFile> files)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(name);
        return new TemplateSource(provider, name, VirtualFileSet.Validate(files, nameof(files)));
    }

    /// <summary>Typst markup held in memory, compiled in a fresh, empty sandbox and sent to Typst over stdin.</summary>
    public static TypstSource Markup(string markup) => Markup(markup, ImmutableArray<TypstVirtualFile>.Empty);

    /// <summary>Typst markup plus virtual files, compiled in a fresh sandbox.</summary>
    /// <param name="markup">The Typst markup.</param>
    /// <param name="files">Files the markup can reference by root-relative path. A default array means none.</param>
    /// <exception cref="ArgumentException"><paramref name="files"/> contains null, or two paths that are equal or differ only by case.</exception>
    public static TypstSource Markup(string markup, ImmutableArray<TypstVirtualFile> files)
    {
        ArgumentNullException.ThrowIfNull(markup);
        return new MarkupSource(markup, VirtualFileSet.Validate(files, nameof(files)));
    }

    /// <summary>A .typ file on disk. Create it with <see cref="File(string)"/>.</summary>
    public sealed record FileSource : TypstSource
    {
        internal FileSource(string path, string? root)
        {
            Path = path;
            Root = root;
        }

        /// <summary>The path as given; resolved against the current directory when the compile starts.</summary>
        public string Path { get; }

        /// <summary>The project root as given, or null to use the file's folder.</summary>
        public string? Root { get; }
    }

    /// <summary>A template from a provider. Create it with <see cref="Template(ITemplateProvider, TypstTemplateName)"/>.</summary>
    public sealed record TemplateSource : TypstSource
    {
        internal TemplateSource(ITemplateProvider provider, TypstTemplateName name, ImmutableArray<TypstVirtualFile> files)
        {
            Provider = provider;
            Name = name;
            Files = files;
        }

        /// <summary>Supplies the template.</summary>
        public ITemplateProvider Provider { get; }

        /// <summary>The template's name.</summary>
        public TypstTemplateName Name { get; }

        /// <summary>The virtual files written into the sandbox; empty when there are none.</summary>
        public ImmutableArray<TypstVirtualFile> Files { get; }
    }

    /// <summary>Typst markup held in memory. Create it with <see cref="Markup(string)"/>.</summary>
    public sealed record MarkupSource : TypstSource
    {
        internal MarkupSource(string text, ImmutableArray<TypstVirtualFile> files)
        {
            Text = text;
            Files = files;
        }

        /// <summary>The Typst markup, sent to Typst over stdin.</summary>
        public string Text { get; }

        /// <summary>The virtual files written into the sandbox; empty when there are none.</summary>
        public ImmutableArray<TypstVirtualFile> Files { get; }
    }
}
