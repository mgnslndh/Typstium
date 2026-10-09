using Build.Cake.Extensions.Coverage.Reporting;

namespace Build.Cake.Extensions.Tests.Coverage.Reporting;

public class PublicApiCatalogTests
{
    [Test]
    public async Task Public_types_are_bare_type_lines_without_generic_arguments()
    {
        var types = PublicApiCatalog.Parse(["#nullable enable", "", "  Alpha.Api.Widget  ", "Alpha.Api.Widget.Run() -> void", "Alpha.Api.Box<T>", "Alpha.Api.Box<T>.Get() -> T"]);

        await Assert.That(types.Order(StringComparer.Ordinal)).IsEquivalentTo(["Alpha.Api.Box", "Alpha.Api.Widget"]);
    }

    [Test]
    public async Task Reads_every_PublicAPI_file_of_a_product()
    {
        var types = PublicApiCatalog.Read(Fixture.Path("EdgeCases", "src", "Alpha"));

        await Assert.That(types).IsNotNull();
        await Assert.That(types!.Contains("Alpha.Root")).IsTrue();
    }

    [Test]
    public async Task A_product_without_PublicAPI_files_has_no_catalog()
    {
        await Assert.That(PublicApiCatalog.Read(Fixture.Path("EdgeCases", "src", "Beta"))).IsNull();
    }
}
