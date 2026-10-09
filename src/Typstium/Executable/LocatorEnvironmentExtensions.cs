namespace Typstium.Executable;

internal static class LocatorEnvironmentExtensions
{
    extension(ILocatorEnvironment environment)
    {
        /// <summary>The Typst executable's file name on this environment's platform (not a path).</summary>
        public string ExecutableFileName =>
            environment.IsWindows ? TypstExecutableFileNames.Windows : TypstExecutableFileNames.Unix;
    }
}
