using System.Runtime.InteropServices;

namespace Typstium.Utilities;

internal static class ByteMemory
{
    /// <summary>The array behind the memory, without copying when possible (netstandard2.0 has no span I/O).</summary>
    public static ArraySegment<byte> AsSegment(ReadOnlyMemory<byte> memory) =>
        MemoryMarshal.TryGetArray(memory, out var segment) ? segment : new ArraySegment<byte>(memory.ToArray());
}
