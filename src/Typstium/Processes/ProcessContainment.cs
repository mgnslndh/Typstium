using System.Runtime.InteropServices;

namespace Typstium.Processes;

internal static class ProcessContainment
{
    public static IProcessContainment Create(long? memoryLimitBytes) =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? WindowsJobObject.Create(memoryLimitBytes)
            : NoProcessContainment.Instance;
}
