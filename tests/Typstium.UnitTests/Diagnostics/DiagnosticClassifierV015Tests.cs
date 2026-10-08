using Typstium.Diagnostics;

namespace Typstium.UnitTests.Diagnostics;

public class DiagnosticClassifierV015Tests
{
    private readonly DiagnosticClassifierV015 _classifier = new();

    [Test]
    [Arguments("unknown variable: x", "TYPST1001")]
    [Arguments("file not found (searched at /a/b.png)", "TYPST1002")]
    [Arguments("path `\"../secret.txt\"` would escape the project root", "TYPST1003")]
    [Arguments("package not found (searched for @preview/x:1.0.0)", "TYPST1004")]
    [Arguments("failed to download package (https://packages.typst.org/x)", "TYPST1005")]
    [Arguments("failed to decode image (bad)", "TYPST1006")]
    [Arguments("unclosed delimiter", "TYPST1007")]
    [Arguments("expected comma", "TYPST1007")]
    [Arguments("unexpected closing bracket", "TYPST1007")]
    [Arguments("maximum function call depth exceeded", "TYPST1008")]
    [Arguments("loop seems to be infinite", "TYPST1008")]
    [Arguments("condition is always true", "TYPST1008")]
    [Arguments("expected color, gradient, or tiling, found integer", "TYPST0001")]
    [Arguments("something new in a future Typst", "TYPST0001")]
    public async Task Classifies_errors(string message, string expectedId)
    {
        await Assert.That(_classifier.Classify(TypstDiagnosticSeverity.Error, message).Value).IsEqualTo(expectedId);
    }

    [Test]
    [Arguments("unknown font family: arial", "TYPST2001")]
    [Arguments("a warning nobody classified", "TYPST0002")]
    public async Task Classifies_warnings(string message, string expectedId)
    {
        await Assert.That(_classifier.Classify(TypstDiagnosticSeverity.Warning, message).Value).IsEqualTo(expectedId);
    }
}
