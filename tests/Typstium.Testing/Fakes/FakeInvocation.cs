namespace Typstium.Testing.Fakes;

public static class FakeInvocation
{
    public static TypstInvocation Create() =>
        new("typst", "Fake", new TypstVersion(0, 15, 1), "typst compile", TimeSpan.FromMilliseconds(30), TimeSpan.FromMilliseconds(35));
}
