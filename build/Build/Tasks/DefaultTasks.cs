public static partial class Program
{
    // What CI runs on every platform, and what a local ./build.sh runs.
    static void RegisterDefaultTask()
    {
        Task("Default")
            .IsDependentOn("TimingTests")
            .IsDependentOn("CoverageReport");
    }
}
