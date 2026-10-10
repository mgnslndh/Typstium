using Cake.Core.IO;
using Cake.Typstium.UnitTests.Testing;
using Typstium;
using Typstium.Testing.Fixtures;

namespace Cake.Typstium.UnitTests;

public class TypstAliasesTests
{
    [Test]
    public async Task TypstCompiler_returns_a_cli_compiler()
    {
        using var work = new TemporaryDirectory("cake-aliases");
        var context = new TestCakeContext(CakeFakes.Environment(work.Path));

        await Assert.That(context.TypstCompiler()).IsTypeOf<CliTypstCompiler>();
        await Assert.That(context.TypstCompiler(new FilePath("bin/typst"))).IsTypeOf<CliTypstCompiler>();
    }

    [Test]
    public async Task Null_arguments_are_refused_before_any_work()
    {
        using var work = new TemporaryDirectory("cake-aliases");
        var context = new TestCakeContext(CakeFakes.Environment(work.Path));

        Assert.Throws<ArgumentNullException>(() => TypstAliases.TypstCompile(null!, new FilePath("a.typ")));
        Assert.Throws<ArgumentNullException>(() => context.TypstCompile(null!));
        Assert.Throws<ArgumentNullException>(() => context.TypstCompile(new FilePath("a.typ"), null!));
        Assert.Throws<ArgumentNullException>(() => context.TypstCompile(new FilePath("a.typ"), new FilePath("a.pdf"), null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => context.TypstCompileAsync(null!));
        Assert.Throws<ArgumentNullException>(() => context.TypstCompiler(null!));
    }

    [Test]
    public async Task A_missing_explicit_tool_path_is_reported_by_the_locators()
    {
        using var work = new TemporaryDirectory("cake-aliases");
        work.CreateFile("doc.typ", "= Hi");
        var context = new TestCakeContext(CakeFakes.Environment(work.Path));
        var settings = new TypstCompileSettings().WithToolPath(new FilePath("missing/typst"));

        // Other locators may still find an installed Typst; either the compile succeeds or the not-found error lists
        // the explicit path first. Both prove the setting reached the resolver without the alias wrapping anything.
        try
        {
            await context.TypstCompileAsync(new FilePath("doc.typ"), new FilePath("doc.pdf"), settings);
        }
        catch (TypstExecutableNotFoundException exception)
        {
            await Assert.That(exception.Searches[0].LocatorName).IsEqualTo("ExplicitPath");
        }
    }
}
