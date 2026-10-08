using System.Collections.Immutable;

namespace Typstium;

/// <summary>A template bundle: its main file plus every file the main file may reference.</summary>
public sealed class TypstTemplate
{
    /// <summary>Creates a template bundle, for custom <see cref="ITemplateProvider"/> implementations.</summary>
    /// <param name="name">The template's name.</param>
    /// <param name="mainFile">The file Typst compiles; must be one of <paramref name="files"/>.</param>
    /// <param name="version">Identifies the content; it must change whenever any file changes.</param>
    /// <param name="files">Every file of the bundle, with paths relative to the bundle root.</param>
    /// <exception cref="ArgumentException"><paramref name="files"/> contains null or paths that are equal or differ only by case,
    /// or <paramref name="mainFile"/> isn't one of them.</exception>
    public TypstTemplate(TypstTemplateName name, TypstPath mainFile, TypstTemplateVersion version, IReadOnlyList<TypstTemplateFile> files)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        MainFile = mainFile ?? throw new ArgumentNullException(nameof(mainFile));
        Version = version ?? throw new ArgumentNullException(nameof(version));
        ArgumentNullException.ThrowIfNull(files);

        var seen = new Dictionary<string, TypstPath>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            if (file is null)
            {
                throw new ArgumentException("Template files must not be null.", nameof(files));
            }

            if (seen.TryGetValue(file.Path.Value, out var existing))
            {
                throw new ArgumentException(
                    $"Template '{name}' contains '{existing}' and '{file.Path}', which are duplicates or differ only by case.",
                    nameof(files));
            }

            seen.Add(file.Path.Value, file.Path);
        }

        if (!files.Any(file => file.Path == mainFile))
        {
            throw new ArgumentException($"The main file '{mainFile}' is not one of the files of template '{name}'.", nameof(mainFile));
        }

        Files = files.ToImmutableArray();
    }

    /// <summary>The template's name.</summary>
    public TypstTemplateName Name { get; }

    /// <summary>The file Typst compiles.</summary>
    public TypstPath MainFile { get; }

    /// <summary>Identifies the content; changes whenever any file changes.</summary>
    public TypstTemplateVersion Version { get; }

    /// <summary>Every file of the bundle, including <see cref="MainFile"/>.</summary>
    public IReadOnlyList<TypstTemplateFile> Files { get; }
}
