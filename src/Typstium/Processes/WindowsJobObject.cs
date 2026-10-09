using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Typstium.Interop.Windows;

namespace Typstium.Processes;

/// <summary>A Windows job object with kill-on-close and an optional per-process memory limit.</summary>
internal sealed class WindowsJobObject : IProcessContainment
{
    private readonly SafeJobObjectHandle _handle;

    private WindowsJobObject(SafeJobObjectHandle handle) => _handle = handle;

    public static WindowsJobObject Create(long? memoryLimitBytes)
    {
        var handle = Kernel32.CreateJobObjectW(IntPtr.Zero, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw new Win32Exception(error);
        }

        var info = new Kernel32.JobObjectExtendedLimitInformation();
        info.BasicLimitInformation.LimitFlags = Kernel32.JobObjectLimitKillOnJobClose;
        if (memoryLimitBytes is { } limit)
        {
            info.BasicLimitInformation.LimitFlags |= Kernel32.JobObjectLimitProcessMemory;
            info.ProcessMemoryLimit = new UIntPtr((ulong)limit);
        }

        var size = Marshal.SizeOf<Kernel32.JobObjectExtendedLimitInformation>();
        if (Kernel32.SetInformationJobObject(handle, Kernel32.JobObjectInfoClass.ExtendedLimitInformation, ref info, size) == 0)
        {
            var error = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw new Win32Exception(error);
        }

        return new WindowsJobObject(handle);
    }

    public void Attach(Process process)
    {
        if (Kernel32.AssignProcessToJobObject(_handle, process.Handle) == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    public void Terminate()
    {
        if (!_handle.IsClosed)
        {
            Kernel32.TerminateJobObject(_handle, exitCode: 1);
        }
    }

    public void Dispose() => _handle.Dispose();
}
