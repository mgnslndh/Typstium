using Microsoft.Win32.SafeHandles;

namespace Typstium.Interop.Windows;

/// <summary>Owns a job object handle. Closing the last handle kills the job's processes (kill-on-close).</summary>
internal sealed class SafeJobObjectHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafeJobObjectHandle()
        : base(ownsHandle: true)
    {
    }

    protected override bool ReleaseHandle() => Kernel32.CloseHandle(handle) != 0;
}
