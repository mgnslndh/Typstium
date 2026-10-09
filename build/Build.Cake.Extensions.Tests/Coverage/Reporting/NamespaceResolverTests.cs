using Build.Cake.Extensions.Coverage.Reporting;

namespace Build.Cake.Extensions.Tests.Coverage.Reporting;

public class NamespaceResolverTests
{
    private static readonly HashSet<string> Classes = ["Alpha.Api.Widget", "Alpha.Api.Widget.Inner", "Alpha.Root", "Alpha.Internal.Cache", "Solo"];

    [Test]
    [Arguments("Alpha.Api.Widget", "Alpha.Api")]
    [Arguments("Alpha.Api.Widget.Inner", "Alpha.Api")]
    [Arguments("Alpha.Root", "Alpha")]
    [Arguments("Alpha.Internal.Cache<TKey, TValue>", "Alpha.Internal")]
    [Arguments("Solo", "Solo")]
    [Arguments("Not.In.Report", "Not.In")]
    public async Task Namespace_is_the_prefix_before_the_first_segment_that_is_a_class(string className, string expected)
    {
        await Assert.That(NamespaceResolver.Namespace(className, Classes)).IsEqualTo(expected);
    }

    [Test]
    [Arguments("Api/Widget.cs", "Api")]
    [Arguments("Internal/Deep/Parser.cs", "Internal")]
    [Arguments("Root.cs", "(root)")]
    public async Task Area_is_the_top_level_folder_or_root(string file, string expected)
    {
        await Assert.That(NamespaceResolver.Area(file)).IsEqualTo(expected);
    }
}
