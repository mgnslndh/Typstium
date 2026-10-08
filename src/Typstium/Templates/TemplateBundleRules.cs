namespace Typstium.Templates;

internal static class TemplateBundleRules
{
    /// <summary>The main file is &lt;last name segment&gt;.typ, otherwise main.typ.</summary>
    public static TypstPath SelectMainFile(TypstTemplateName name, IEnumerable<TypstPath> files)
    {
        var paths = files.ToList();
        var byName = TypstPath.Parse(name.LastSegment + ".typ");
        var main = TypstPath.Parse("main.typ");
        if (paths.Contains(byName))
        {
            return byName;
        }

        if (paths.Contains(main))
        {
            return main;
        }

        throw new InvalidOperationException($"The template '{name}' has neither '{byName}' nor '{main}'.");
    }
}
