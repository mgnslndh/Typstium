using Cake.Core.IO;
using Typstium;

namespace Cake.Typstium;

/// <summary>
/// How <see cref="TypstAliases.TypstCompile(Cake.Core.ICakeContext, FilePath, FilePath, TypstCompileSettings)"/> compiles. The defaults equal <see cref="TypstCompileOptions.Default"/>: no system fonts, no
/// network, a 60 second timeout. Relative paths resolve against Cake's working directory.
/// </summary>
public sealed class TypstCompileSettings
{
    /// <summary>The Typst executable to use, ahead of every other place Typst is looked for.</summary>
    public FilePath? ToolPath { get; set; }

    /// <summary>The project root Typst may read files from; null means the input file's folder.</summary>
    public DirectoryPath? Root { get; set; }

    /// <summary>The output format; null infers it from the output file's extension (.pdf, .png, .svg, .html).</summary>
    public TypstOutputFormat? Format { get; set; }

    /// <summary>PDF standards to conform to, such as <see cref="PdfStandard.A2b"/>; PDF output only.</summary>
    public IList<PdfStandard> PdfStandards { get; } = new List<PdfStandard>();

    /// <summary>Pixels per inch for PNG output; null means 144.</summary>
    public int? Ppi { get; set; }

    /// <summary>The pages to export; null means all pages.</summary>
    public TypstPageSelection? Pages { get; set; }

    /// <summary>Values for <c>sys.inputs</c> in the document, passed as <c>--input key=value</c>.</summary>
    public IDictionary<string, string> Inputs { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Folders with fonts for the document.</summary>
    public IList<DirectoryPath> FontPaths { get; } = new List<DirectoryPath>();

    /// <summary>Whether Typst may use the machine's installed fonts; off by default for reproducible output.</summary>
    public bool IncludeSystemFonts { get; set; }

    /// <summary>A folder of vendored Typst packages, laid out as <c>namespace/name/version</c>.</summary>
    public DirectoryPath? PackagePath { get; set; }

    /// <summary>The folder Typst caches downloaded packages in; null means Typst's own.</summary>
    public DirectoryPath? PackageCachePath { get; set; }

    /// <summary>Whether Typst may download packages; off by default.</summary>
    public bool AllowNetwork { get; set; }

    /// <summary>The creation timestamp written into the document; null passes SOURCE_DATE_EPOCH through when set.</summary>
    public DateTimeOffset? CreationTimestamp { get; set; }

    /// <summary>How long one compile may run; null means the core's default of 60 seconds.</summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>Whether warnings fail the task, like MSBuild's TreatWarningsAsErrors.</summary>
    public bool TreatWarningsAsErrors { get; set; }

    /// <summary>Arguments appended after the ones Typstium generates.</summary>
    public TypstArguments ExtraArguments { get; set; } = TypstArguments.Empty;

    /// <summary>Changes the complete generated command line; can undo every guarantee Typstium gives.</summary>
    public Func<TypstArguments, TypstArguments>? ArgumentCustomization { get; set; }
}
