namespace Typstium.Testing.Fixtures;

/// <summary>One captured stderr, with the absolute project root replaced by {ROOT}.</summary>
public sealed record DiagnosticFixture(TypstVersion TypstVersion, string Name, string StandardErrorTemplate)
{
    public const string RootPlaceholder = "{ROOT}";

    public string StandardErrorFor(string root) => StandardErrorTemplate.Replace(RootPlaceholder, root);

    public override string ToString() => $"typst-{TypstVersion.Major}.{TypstVersion.Minor}/{Name}";
}
