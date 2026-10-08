using System.Collections.Immutable;
using TUnit.Assertions.Enums;

namespace Typstium.UnitTests.Output;

public class TypstDocumentTests
{
    [Test]
    public async Task Single_file_documents_write_their_content()
    {
        var document = (TypstDocument.SingleFile)TypstDocument.CreateSingleFile(TypstOutputFormat.Pdf, new byte[] { 37, 80, 68, 70 });
        using var destination = new MemoryStream();
        await document.WriteToAsync(destination, CancellationToken.None);
        await Assert.That(destination.ToArray()).IsEquivalentTo(new byte[] { 37, 80, 68, 70 }, CollectionOrdering.Matching);
    }

    [Test]
    public async Task Paged_documents_save_with_a_page_pattern()
    {
        var pages = ImmutableArray.Create<ReadOnlyMemory<byte>>(new byte[] { 1 }, new byte[] { 2 });
        var document = (TypstDocument.Paged)TypstDocument.CreatePaged(TypstOutputFormat.Png, pages);
        var directory = Path.Combine(Path.GetTempPath(), "typstium-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var written = await document.SaveAsync(Path.Combine(directory, "page-{0p}-of-{t}.png"), CancellationToken.None);
            await Assert.That(written.Select(w => Path.GetFileName(w)!)).IsEquivalentTo(new[] { "page-1-of-2.png", "page-2-of-2.png" }, CollectionOrdering.Matching);
            await Assert.That(File.ReadAllBytes(written[1])).IsEquivalentTo(new byte[] { 2 }, CollectionOrdering.Matching);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task Multi_page_patterns_must_contain_a_page_placeholder()
    {
        var pages = ImmutableArray.Create<ReadOnlyMemory<byte>>(new byte[] { 1 }, new byte[] { 2 });
        var document = (TypstDocument.Paged)TypstDocument.CreatePaged(TypstOutputFormat.Svg, pages);
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => document.SaveAsync("page.svg", CancellationToken.None));
        await Assert.That(exception!.ParamName).IsEqualTo("pathPattern");
    }

    [Test]
    public async Task Factories_reject_the_wrong_shape_for_a_format()
    {
        await Assert.That(Assert.Throws<ArgumentException>(() => TypstDocument.CreateSingleFile(TypstOutputFormat.Png, new byte[] { 1 }))).IsNotNull();
        await Assert.That(Assert.Throws<ArgumentException>(() => TypstDocument.CreatePaged(TypstOutputFormat.Pdf, ImmutableArray<ReadOnlyMemory<byte>>.Empty))).IsNotNull();
    }
}
