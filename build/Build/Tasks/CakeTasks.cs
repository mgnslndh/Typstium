public static partial class Program
{
    // docs/specs/cake.md §8. Not part of Default: needs the real runtime packages (PackTypstRuntimes) and runs real Cake.
    static void RegisterCakeTasks()
    {
        Task("CakeEndToEndTests")
            .Does<BuildParameters>(parameters =>
                DotNetTest("tests/Cake.Typstium.EndToEndTests", new DotNetTestSettings
                {
                    PathType = DotNetTestPathType.Project,
                    Configuration = parameters.Configuration,
                    EnvironmentVariables = { ["TYPSTIUM_RUNTIME_PACKAGES"] = MakeAbsolute(Directory("artifacts/packages")).FullPath },
                }));
    }
}
