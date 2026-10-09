namespace Typstium;

/// <summary>The obvious default setup, for scripts and apps without a DI container.</summary>
public static class TypstCompiler
{
    /// <summary>A <see cref="CliTypstCompiler"/> with the default resolver and settings.</summary>
    public static ITypstCompiler CreateDefault() => CreateDefault(CliTypstCompilerSettings.Default);

    /// <summary>A <see cref="CliTypstCompiler"/> with the default resolver and the given settings.</summary>
    public static ITypstCompiler CreateDefault(CliTypstCompilerSettings settings) =>
        new CliTypstCompiler(TypstExecutableResolver.CreateDefault(), settings);
}
