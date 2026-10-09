public static partial class Program
{
    static void RegisterBuildTasks()
    {
        Task("Build")
            .IsDependentOn("BuildTests")
            .Does<BuildParameters>(parameters =>
                DotNetBuild("Typstium.slnx", new DotNetBuildSettings { Configuration = parameters.Configuration }));
    }
}
