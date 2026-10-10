using System.Formats.Tar;
using System.IO.Compression;
using SharpCompress.Compressors.Xz;

namespace Build.Cake.Extensions.Downloads;

// Copies named files out of a release archive (.zip or .tar.xz). Entries are matched by file name, at the root or one
// folder down (release archives usually wrap their files in one folder), and written to paths the caller chose, so
// nothing in the archive decides where a file lands. The .NET base library has no xz decoder; SharpCompress supplies it.
public static class ArchiveFiles
{
    public static void Extract(string archivePath, IReadOnlyDictionary<string, string> destinationsByFileName)
    {
        var matches = destinationsByFileName.Keys.ToDictionary(name => name, _ => new List<string>(), StringComparer.Ordinal);
        foreach (var (entryPath, open) in Entries(archivePath))
        {
            var segments = entryPath.Split('/', StringSplitOptions.RemoveEmptyEntries).Where(segment => segment != ".").ToArray();
            if (segments.Length is < 1 or > 2 || !matches.TryGetValue(segments[^1], out var found))
            {
                continue;
            }

            found.Add(entryPath);
            if (found.Count == 1)
            {
                var destination = destinationsByFileName[segments[^1]];
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
                using var source = open();
                using var target = File.Create(destination);
                source.CopyTo(target);
            }
        }

        foreach (var (name, found) in matches)
        {
            if (found.Count == 0)
            {
                throw new InvalidDataException($"{archivePath} has no entry named {name} at its root or one folder down.");
            }

            if (found.Count > 1)
            {
                throw new InvalidDataException($"{archivePath} has more than one entry named {name}: {string.Join(", ", found)}.");
            }
        }
    }

    // Regular files only, with a function that opens the entry's content (valid until the next entry is read).
    private static IEnumerable<(string Path, Func<Stream> Open)> Entries(string archivePath)
    {
        if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var zip = ZipFile.OpenRead(archivePath);
            foreach (var entry in zip.Entries.Where(entry => !entry.FullName.EndsWith('/')))
            {
                yield return (entry.FullName.Replace('\\', '/'), entry.Open);
            }
        }
        else if (archivePath.EndsWith(".tar.xz", StringComparison.OrdinalIgnoreCase))
        {
            using var file = File.OpenRead(archivePath);
            using var xz = new XZStream(file);
            using var tar = new TarReader(xz);
            while (tar.GetNextEntry() is { } entry)
            {
                if (entry.EntryType is TarEntryType.RegularFile or TarEntryType.V7RegularFile && entry.DataStream is { } data)
                {
                    yield return (entry.Name, () => new NonClosingStream(data));
                }
            }
        }
        else
        {
            throw new NotSupportedException($"{archivePath}: only .zip and .tar.xz archives are supported.");
        }
    }

    // TarReader owns the entry's stream; the caller's using must not close it.
    private sealed class NonClosingStream(Stream inner) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
