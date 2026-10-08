using System.Collections.Immutable;

namespace Typstium;

internal static class VirtualFileSet
{
    /// <summary>Normalizes a default array to empty and rejects nulls and case-insensitive duplicates.</summary>
    public static ImmutableArray<TypstVirtualFile> Validate(ImmutableArray<TypstVirtualFile> files, string parameterName)
    {
        if (files.IsDefault)
        {
            return ImmutableArray<TypstVirtualFile>.Empty;
        }

        var seen = new Dictionary<string, TypstPath>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            if (file is null)
            {
                throw new ArgumentException("Virtual files must not be null.", parameterName);
            }

            if (seen.TryGetValue(file.Path.Value, out var existing))
            {
                var message = existing == file.Path
                    ? $"The virtual file '{file.Path}' is added more than once."
                    : $"The virtual files '{existing}' and '{file.Path}' differ only by case; they would overwrite each other on Windows and macOS.";
                throw new ArgumentException(message, parameterName);
            }

            seen.Add(file.Path.Value, file.Path);
        }

        return files;
    }
}
