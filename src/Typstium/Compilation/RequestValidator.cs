namespace Typstium.Compilation;

/// <summary>Step 1: caller errors become ArgumentException before any I/O.</summary>
internal static class RequestValidator
{
    public static void Validate(TypstCompileRequest request)
    {
        if (request.Source is not (TypstSource.FileSource or TypstSource.TemplateSource or TypstSource.MarkupSource))
        {
            throw new ArgumentException($"Unsupported source kind '{request.Source.GetType().Name}'.", nameof(request));
        }

        if (request.Output is not (TypstOutput.PdfOutput or TypstOutput.PngOutput or TypstOutput.SvgOutput or TypstOutput.HtmlOutput))
        {
            throw new ArgumentException($"Unsupported output kind '{request.Output.GetType().Name}'.", nameof(request));
        }
    }
}
