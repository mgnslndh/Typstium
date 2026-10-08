using Typstium.Capabilities;

namespace Typstium.Compilation;

/// <summary>Step 3: refuses requests the installed Typst can't serve, before starting a compile.</summary>
internal static class CapabilityValidator
{
    public static void Validate(TypstCompileRequest request, TypstCapabilities capabilities)
    {
        switch (request.Output)
        {
            case TypstOutput.HtmlOutput when !capabilities.SupportsHtml:
                throw new TypstFeatureNotSupportedException("HTML export", capabilities.Version, requiredVersion: null);

            case TypstOutput.PdfOutput pdf:
                foreach (var standard in pdf.Options.Standards)
                {
                    // Standards Typstium doesn't know may come from a newer Typst: pass them through.
                    if (PdfStandard.KnownInTypst015.Contains(standard) && !capabilities.PdfStandards.Contains(standard))
                    {
                        throw new TypstFeatureNotSupportedException($"PDF standard '{standard}'", capabilities.Version, requiredVersion: null);
                    }
                }

                break;
        }
    }
}
