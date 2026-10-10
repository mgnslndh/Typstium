# Writes the .tar.xz fixtures for ArchiveFilesTests. Run from this folder: python make-fixtures.py
import io
import tarfile

def write(name, files):
    with tarfile.open(name, "w:xz") as archive:
        for path, content in files.items():
            data = content.encode()
            info = tarfile.TarInfo(path)
            info.size = len(data)
            info.mode = 0o644
            info.mtime = 0
            archive.addfile(info, io.BytesIO(data))

write("release.tar.xz", {
    "typst-x86_64-unknown-linux-musl/typst": "#!/bin/sh\necho typst\n",
    "typst-x86_64-unknown-linux-musl/LICENSE": "license\n",
    "typst-x86_64-unknown-linux-musl/README.md": "readme\n",
})
write("deep.tar.xz", {"a/b/typst": "too deep\n"})
