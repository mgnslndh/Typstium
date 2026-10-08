using System.Runtime.InteropServices;

namespace Typstium.Interop.Windows;

/// <summary>P/Invoke declarations for kernel32.dll. Windows only; callers check the platform.</summary>
internal static class Kernel32
{
    internal const uint JobObjectLimitProcessMemory = 0x00000100;
    internal const uint JobObjectLimitKillOnJobClose = 0x00002000;

    private const string LibraryName = "kernel32.dll";

    internal enum JobObjectInfoClass
    {
        ExtendedLimitInformation = 9,
    }

    [DllImport(LibraryName, SetLastError = true, ExactSpelling = true)]
    internal static extern SafeJobObjectHandle CreateJobObjectW(IntPtr jobAttributes, IntPtr name);

    [DllImport(LibraryName, SetLastError = true, ExactSpelling = true)]
    internal static extern int SetInformationJobObject(SafeJobObjectHandle job, JobObjectInfoClass infoClass, ref JobObjectExtendedLimitInformation info, int length);

    [DllImport(LibraryName, SetLastError = true, ExactSpelling = true)]
    internal static extern int AssignProcessToJobObject(SafeJobObjectHandle job, IntPtr process);

    [DllImport(LibraryName, SetLastError = true, ExactSpelling = true)]
    internal static extern int TerminateJobObject(SafeJobObjectHandle job, uint exitCode);

    [DllImport(LibraryName, SetLastError = true, ExactSpelling = true)]
    internal static extern int CloseHandle(IntPtr handle);

    [DllImport(LibraryName, CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    private static extern int GetLongPathNameW(string shortPath, [Out] char[]? longPath, int bufferLength);

    /// <summary>Expands 8.3 short names (e.g. RUNNER~1). Returns null if the path does not exist.</summary>
    internal static string? GetLongPath(string path)
    {
        var length = GetLongPathNameW(path, null, 0);
        if (length == 0)
        {
            return null;
        }

        var buffer = new char[length];
        var written = GetLongPathNameW(path, buffer, buffer.Length);
        return written == 0 || written >= buffer.Length ? null : new string(buffer, 0, written);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct JobObjectBasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct JobObjectExtendedLimitInformation
    {
        public JobObjectBasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }
}
