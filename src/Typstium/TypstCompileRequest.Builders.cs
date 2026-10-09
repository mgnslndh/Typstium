namespace Typstium;

public sealed partial record TypstCompileRequest
{
    /// <summary>Starts a request for a template.</summary>
    /// <exception cref="ArgumentException"><paramref name="templateName"/> isn't a valid name.</exception>
    public static TypstCompileRequestBuilder ForTemplate(ITemplateProvider provider, string templateName) =>
        ForTemplate(provider, TypstTemplateName.Parse(templateName));

    /// <summary>Starts a request for a template.</summary>
    public static TypstCompileRequestBuilder ForTemplate(ITemplateProvider provider, TypstTemplateName templateName)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(templateName);
        return new TypstCompileRequestBuilder(files => TypstSource.Template(provider, templateName, files));
    }

    /// <summary>Starts a request for markup held in memory.</summary>
    public static TypstCompileRequestBuilder ForMarkup(string markup)
    {
        ArgumentNullException.ThrowIfNull(markup);
        return new TypstCompileRequestBuilder(files => TypstSource.Markup(markup, files));
    }

    /// <summary>Starts a request for a .typ file compiled in place, with the file's folder as the project root.</summary>
    public static TypstFileCompileRequestBuilder ForFile(string path) => new(TypstSource.File(path));

    /// <summary>Starts a request for a .typ file compiled in place with an explicit project root.</summary>
    public static TypstFileCompileRequestBuilder ForFile(string path, string root) => new(TypstSource.File(path, root));
}
