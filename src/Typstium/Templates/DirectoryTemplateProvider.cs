using Typstium.Templates;

namespace Typstium;

/// <summary>
/// Templates from a folder: each subfolder is a template named by its relative path, containing every
/// file below it. The main file is &lt;name&gt;.typ, otherwise main.typ.
/// </summary>
public sealed class DirectoryTemplateProvider : ITemplateProvider
{
    public DirectoryTemplateProvider(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        RootDirectory = Path.GetFullPath(rootDirectory);
    }

    public string RootDirectory { get; }

    public async ValueTask<TypstTemplate?> GetTemplateAsync(TypstTemplateName name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        var directory = Path.Combine(new[] { RootDirectory }.Concat(name.Value.Split('/')).ToArray());
        if (!Directory.Exists(directory))
        {
            return null;
        }

        var prefixLength = directory.Length + 1;
        var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Select(fullPath => TypstTemplateFile.FromFile(TypstPath.Parse(fullPath.Substring(prefixLength)), fullPath))
            .ToList();

        var mainFile = TemplateBundleRules.SelectMainFile(name, files.Select(file => file.Path));
        var version = await TemplateHasher.ComputeAsync(files, cancellationToken).ConfigureAwait(false);
        return new TypstTemplate(name, mainFile, version, files);
    }
}
