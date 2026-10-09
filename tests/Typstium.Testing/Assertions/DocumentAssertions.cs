using System.Text;

namespace Typstium.Testing.Assertions;

public static class DocumentAssertions
{
    public static async Task<byte[]> PdfBytesAsync(TypstCompileResult result)
    {
        await Assert.That(result.Status).IsEqualTo(TypstCompileStatus.Succeeded);
        var bytes = ((TypstDocument.SingleFile)result.Document!).Content.ToArray();
        await Assert.That(Encoding.ASCII.GetString(bytes, 0, 5)).IsEqualTo("%PDF-");
        return bytes;
    }

    public static async Task<int> PageCountAsync(TypstCompileResult result)
    {
        await Assert.That(result.Status).IsEqualTo(TypstCompileStatus.Succeeded);
        return ((TypstDocument.Paged)result.Document!).Pages.Length;
    }
}
