namespace Build.Cake.Extensions.Coverage.Reporting;

public static class NamespaceResolver
{
    public static string TypeName(string className) => className.Split('<')[0];

    // The prefix before the first segment that is itself a class in the report, so nested types
    // (Typstium.Capabilities.TypstCliProfileFactory.ProfileKey) land in their outer type's namespace.
    public static string Namespace(string className, IReadOnlySet<string> classNames)
    {
        var parts = TypeName(className).Split('.');
        for (var count = 1; count <= parts.Length; count++)
        {
            if (classNames.Contains(string.Join('.', parts[..count])))
            {
                return count > 1 ? string.Join('.', parts[..(count - 1)]) : parts[0];
            }
        }

        return parts.Length > 1 ? string.Join('.', parts[..^1]) : parts[0];
    }

    // The top-level folder under the product folder; files directly in it form "(root)".
    public static string Area(string relativeFile) => relativeFile.Contains('/') ? relativeFile.Split('/')[0] : "(root)";
}
