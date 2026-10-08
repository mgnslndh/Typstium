using System.Collections.Immutable;
using Typstium.Workspace;

namespace Typstium.Compilation;

/// <summary>Step 10: the document from stdout, or the page files in page order.</summary>
internal static class DocumentCollector
{
    /// <returns>The document, or <see langword="null"/> when the page files together exceed <paramref name="maxOutputBytes"/>. Sizes are checked before any page is read.</returns>
    public static TypstDocument? Collect(TypstOutput output, ReadOnlyMemory<byte> standardOutput, WorkspaceLayout layout, long maxOutputBytes)
    {
        if (!output.IsPaged)
        {
            if (standardOutput.IsEmpty)
            {
                throw new TypstProcessOutputException("Typst exited successfully but wrote no document to stdout.");
            }

            return TypstDocument.CreateSingleFile(output.Format, standardOutput);
        }

        var directory = layout.PagedOutputDirectory
            ?? throw new InvalidOperationException("Paged output needs an output folder in the workspace.");

        // {0p} pads every page number to the same width, so ordinal order is page order.
        var files = Directory.GetFiles(directory, "*." + output.FileExtension).OrderBy(file => file, StringComparer.Ordinal).ToArray();
        if (files.Length == 0)
        {
            throw new TypstProcessOutputException($"Typst exited successfully but wrote no {output.FileExtension} pages.");
        }

        long total = 0;
        foreach (var file in files)
        {
            total += new FileInfo(file).Length;
            if (total > maxOutputBytes)
            {
                return null;
            }
        }

        return TypstDocument.CreatePaged(output.Format, files.Select(file => (ReadOnlyMemory<byte>)File.ReadAllBytes(file)).ToImmutableArray());
    }
}
