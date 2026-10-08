using System.Text;

namespace Typstium.Processes;

/// <summary>
/// Blocking reads of the Typst process's pipes. Process pipes are synchronous streams on every target framework,
/// so <c>ReadAsync</c> would block a thread-pool thread for as long as Typst runs; with every concurrency slot busy
/// that starves the pool, and the timer that enforces the timeout fires seconds late. Run these on dedicated
/// threads (<see cref="DedicatedThread"/>) instead.
/// </summary>
internal static class StreamReading
{
    internal const string TruncationMarker = "[stderr truncated by Typstium]";
    private const int ChunkSize = 81920;

    /// <summary>Reads to the end, or stops and calls <paramref name="onLimitExceeded"/> once the limit is passed.</summary>
    public static (ReadOnlyMemory<byte> Content, bool LimitExceeded) ReadBounded(Stream stream, long maxBytes, Action onLimitExceeded)
    {
        var buffer = new MemoryStream();
        var chunk = new byte[ChunkSize];
        while (true)
        {
            var read = stream.Read(chunk, 0, chunk.Length);
            if (read == 0)
            {
                return (new ReadOnlyMemory<byte>(buffer.GetBuffer(), 0, (int)buffer.Length), false);
            }

            if (buffer.Length + read > maxBytes)
            {
                onLimitExceeded();
                return (ReadOnlyMemory<byte>.Empty, true);
            }

            buffer.Write(chunk, 0, read);
        }
    }

    /// <summary>Drains the stream completely (so the child never blocks) but keeps at most maxBytes.</summary>
    public static string ReadTextCapped(Stream stream, long maxBytes)
    {
        var buffer = new MemoryStream();
        var chunk = new byte[ChunkSize];
        var truncated = false;
        while (true)
        {
            var read = stream.Read(chunk, 0, chunk.Length);
            if (read == 0)
            {
                break;
            }

            var room = (int)Math.Max(0, Math.Min(read, maxBytes - buffer.Length));
            buffer.Write(chunk, 0, room);
            truncated |= room < read;
        }

        var text = Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
        return truncated ? text + "\n" + TruncationMarker : text;
    }
}
