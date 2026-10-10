using Cake.Core;
using Cake.Core.IO;
using Cake.Typstium.Executable;
using Typstium;

namespace Cake.Typstium.Compilation;

/// <summary>A new compiler per call, over the Cake locator chain; no state survives the call (spec cake.md D16).</summary>
internal static class TypstCakeCompilers
{
    public static ITypstCompiler Create(ICakeContext context, FilePath? toolPath) =>
        new CliTypstCompiler(TypstCakeResolver.Create(context, toolPath), new CliTypstCompilerSettings());
}
