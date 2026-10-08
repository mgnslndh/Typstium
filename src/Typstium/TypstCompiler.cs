namespace Typstium;

/// <summary>The obvious default setup, for scripts and apps without a DI container.</summary>
public static class TypstCompiler
{
    public static ITypstCompiler CreateDefault() => CreateDefault(CliTypstCompilerSettings.Default);

    public static ITypstCompiler CreateDefault(CliTypstCompilerSettings settings) =>
        new CliTypstCompiler(TypstExecutableResolver.CreateDefault(), settings);
}
