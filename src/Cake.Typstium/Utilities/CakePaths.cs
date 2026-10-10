using Cake.Core;
using Cake.Core.IO;

namespace Cake.Typstium.Utilities;

/// <summary>Cake paths as the core expects them: absolute against Cake's working directory, in the OS's own form.</summary>
internal static class CakePaths
{
    public static string Native(FilePath path, ICakeEnvironment environment) =>
        System.IO.Path.GetFullPath(path.MakeAbsolute(environment).FullPath);

    public static string Native(DirectoryPath path, ICakeEnvironment environment) =>
        System.IO.Path.GetFullPath(path.MakeAbsolute(environment).FullPath);
}
