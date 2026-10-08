namespace Typstium.CommandLine;

/// <summary>Version-specific: chosen by TypstCliProfileFactory (spec 5.7).</summary>
internal interface ICompileArgumentsBuilder
{
    TypstArguments Build(CompileArgumentsInput input);
}
