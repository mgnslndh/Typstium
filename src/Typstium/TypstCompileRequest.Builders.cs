namespace Typstium;

public sealed partial record TypstCompileRequest
{
    public static TypstCompileRequestBuilder ForTemplate(ITemplateProvider provider, string templateName) =>
        ForTemplate(provider, TypstTemplateName.Parse(templateName));

    public static TypstCompileRequestBuilder ForTemplate(ITemplateProvider provider, TypstTemplateName templateName)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(templateName);
        return new TypstCompileRequestBuilder(files => TypstSource.Template(provider, templateName, files));
    }

    public static TypstCompileRequestBuilder ForMarkup(string markup)
    {
        ArgumentNullException.ThrowIfNull(markup);
        return new TypstCompileRequestBuilder(files => TypstSource.Markup(markup, files));
    }

    public static TypstFileCompileRequestBuilder ForFile(string path) => new(TypstSource.File(path));

    public static TypstFileCompileRequestBuilder ForFile(string path, string root) => new(TypstSource.File(path, root));
}
