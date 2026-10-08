namespace Typstium.Testing.Fixtures;

/// <summary>Loads tests/fixtures/typst-*/diagnostics/*.human.stderr, one folder per Typst version.</summary>
public static class DiagnosticFixtures
{
    private const string Suffix = ".human.stderr";
    private static readonly Lazy<IReadOnlyList<DiagnosticFixture>> Loaded = new(Load);

    public static IEnumerable<Func<DiagnosticFixture>> All() =>
        Loaded.Value.Select(fixture => (Func<DiagnosticFixture>)(() => fixture));

    public static DiagnosticFixture Get(TypstVersion version, string name) =>
        Loaded.Value.Single(fixture => fixture.TypstVersion == version && fixture.Name == name);

    private static IReadOnlyList<DiagnosticFixture> Load()
    {
        var fixtures = new List<DiagnosticFixture>();
        foreach (var versionDirectory in Directory.GetDirectories(RepositoryPaths.Fixtures, "typst-*").OrderBy(d => d, StringComparer.Ordinal))
        {
            var version = TypstVersion.Parse(Path.GetFileName(versionDirectory).Substring("typst-".Length) + ".0");
            var diagnostics = Path.Combine(versionDirectory, "diagnostics");
            foreach (var file in Directory.GetFiles(diagnostics, "*" + Suffix).OrderBy(f => f, StringComparer.Ordinal))
            {
                var fileName = Path.GetFileName(file);
                fixtures.Add(new DiagnosticFixture(version, fileName.Substring(0, fileName.Length - Suffix.Length), File.ReadAllText(file)));
            }
        }

        return fixtures;
    }
}
