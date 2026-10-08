using System.Collections.Immutable;

namespace Typstium;

/// <summary>A template bundle: its main file plus every file the main file may reference.</summary>
public sealed class TypstTemplate
{
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

    public TypstTemplateName Name { get; }

    public TypstPath MainFile { get; }

    public TypstTemplateVersion Version { get; }

    public IReadOnlyList<TypstTemplateFile> Files { get; }
}
