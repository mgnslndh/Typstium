using Typstium.Templates;

namespace Typstium;

/// <summary>
/// Templates from a folder: each subfolder is a template named by its relative path, containing every
/// file below it. The main file is &lt;name&gt;.typ, otherwise main.typ.
/// </summary>
public sealed class DirectoryTemplateProvider : ITemplateProvider
{
    /// <summary>Creates a provider over a folder of template folders.</summary>
    /// <param name="rootDirectory">The folder; resolved against the current directory now. It doesn't need to exist yet.</param>
    public DirectoryTemplateProvider(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        RootDirectory = Path.GetFullPath(rootDirectory);
    }

    /// <summary>The full path of the folder holding the template folders.</summary>
    public string RootDirectory { get; }

    /// <inheritdoc/>
    /// <remarks>Reads the folder on every call and hashes every file for <see cref="TypstTemplate.Version"/>.</remarks>
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
