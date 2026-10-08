namespace Typstium.Executable;

/// <summary>The file name of the Typst CLI executable per platform family.</summary>
internal static class TypstExecutableFileNames
{
    public const string Windows = "typst.exe";

    /// <summary>Linux, macOS and other Unix-like systems (the .NET <c>unix</c> runtime identifier family).</summary>
    public const string Unix = "typst";
}
