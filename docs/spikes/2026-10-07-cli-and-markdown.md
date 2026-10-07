# Spike: Typst CLI latency, Markdown input, offline packages and diagnostics

Oct 7, 2026 · Spike 0 for [Typstium](../design.md)

## Summary

The CLI backend is fast enough. A realistic two-to-three-page PDF compiles in about **35 ms per process** on
Windows and **31 ms** on Linux, as long as system fonts aren't scanned. Scanning them costs about 70 ms of the
roughly 106 ms per call on a Windows machine with many fonts. One machine produced about 60 PDFs per second
with parallel processes. Server mode can wait for the second generation, as planned.

Markdown on top of `.typ` layouts works today with the `@preview/cmarker` package, and Scriban on top of
Markdown works too. The `.typ` file owns the layout and the Markdown is content. The spike found one security
default that Typstium must override: cmarker runs Typst code embedded in Markdown unless `raw-typst: false`
is set.

**Recommendations for the foundation**

1. Default to `--ignore-system-fonts` plus explicit font folders. It's both 3× faster and reproducible. Make
   system fonts opt-in.
2. Parse the `human` diagnostic format, not `short`. Only `human` carries hints, call traces and span length.
3. Always run with a wall-clock timeout and a maximum output size. Typst's own loop and recursion guards don't
   catch heavy-but-finite work.
4. Guarantee "no network" by giving the child process a dead proxy in its environment. The CLI has no
   offline switch.
5. Ship Markdown support as a documented layout pattern plus a .NET escaping helper, not as a new engine.
   Always pass `raw-typst: false` for content that isn't fully trusted.
6. Detect capabilities with `typst info --format json` rather than parsing `--version`.

## Environment

| | |
| --- | --- |
| Typst | 0.15.1 (9dfd3a08), installed with winget; Linux: official `x86_64-unknown-linux-musl` asset |
| Windows | Windows 11 Pro, Intel Core i9-13950HX, 32 GB, 196 font families (1491 variants) installed |
| Linux | WSL 2, Ubuntu 24.04.3 (glibc 2.39), 10 font families |
| Tools | hyperfine 1.20.0 (`--warmup 3 --runs 30 -N`), .NET SDK 10.0.401, Scriban 7.5.0, cmarker 0.1.10 |

Samples are in [`samples/`](../../samples/README.md). Scripts and raw outputs are in [`spike/`](../../spike/README.md).

## 1. Latency

### Windows (hyperfine, mean ± σ)

| Case | Mean | Min |
| --- | ---: | ---: |
| `typst --version` (process start floor) | 10.8 ± 8.5 ms | 8.6 ms |
| Hello world, system fonts | 94.4 ± 8.7 ms | 89.0 ms |
| Hello world, `--ignore-system-fonts` | 21.2 ± 8.5 ms | 18.2 ms |
| **License PDF, system fonts** | **106.5 ± 5.1 ms** | 102.1 ms |
| **License PDF, `--ignore-system-fonts` + bundled font** | **35.4 ± 8.4 ms** | 32.1 ms |
| License PDF, also `--ignore-embedded-fonts` | 33.2 ± 8.4 ms | 30.3 ms |
| License PDF, `-j 1` | 39.8 ± 11.4 ms | 34.3 ms |
| License PDF, PDF/A-2b | 35.4 ± 8.5 ms | 32.5 ms |
| License PNG, page 1 only | 50.2 ± 2.5 ms | 48.8 ms |
| License, file → file | 33.9 ± 3.8 ms | 31.1 ms |
| License, stdin → stdout | 34.6 ± 7.9 ms | 31.8 ms |
| Release notes (cmarker), system fonts | 113.0 ± 11.0 ms | 104.7 ms |
| Release notes (cmarker), `--ignore-system-fonts` | 38.9 ± 8.5 ms | 35.8 ms |

### Linux (WSL 2, mean of 30 runs, shell loop)

| Case | Mean |
| --- | ---: |
| `typst --version` | 1 ms |
| License PDF, system fonts | 34 ms |
| License PDF, `--ignore-system-fonts` | 31 ms |
| Release notes, `--ignore-system-fonts` | 40 ms |

### Throughput (Windows, license PDF, `-j 1`, 200 compiles via `xargs -P`)

| Parallel processes | Total | Docs/s |
| ---: | ---: | ---: |
| 1 | 10.4 s | 19 |
| 4 | 4.7 s | 42 |
| 8 | 3.5 s | 56 |
| 16 | 3.2 s | 62 |

The sequential figure includes Git Bash's process-spawn overhead, which is higher than .NET's
`Process.Start`.

**Takeaways**

- System font discovery dominates. Its cost grows with the number of installed fonts: about 70 ms on this
  Windows machine and about 3 ms in WSL with 10 fonts. Ignoring system fonts also makes output identical
  across machines.
- PDF/A costs nothing measurable. PNG adds about 15 ms per page at 144 ppi.
- stdin/stdout performs the same as files, so the core can choose based on API convenience. Diagnostics go
  to stderr and PDF bytes go to stdout cleanly, including when there are warnings.
- The cmarker WASM plugin adds only about 4 ms.
- **Server-mode gate:** at ~35 ms per document and ~60 documents/s, the CLI backend covers use case B
  comfortably. Server mode stays in the second generation.

## 2. Markdown as input on top of `.typ`

**What cmarker is.** [`@preview/cmarker`](https://typst.app/universe/package/cmarker) (MIT, by Sabrina
Jewson) is a Typst package that converts CommonMark to Typst markup. It does this inside the Typst
compilation, using a Rust parser compiled to a WebAssembly plugin. Version 0.1.10 requires Typst 0.15. A
layout calls:

```typ
#import "@preview/cmarker:0.1.10"
#cmarker.render(read("/release-notes.md"), raw-typst: false,
  scope: (image: (source, ..args) => image(source, width: 50%, ..args)))
```

The `.typ` layout keeps full control through ordinary `show`/`set` rules: heading colors, code block
styling, blockquote styling and table strokes all applied to Markdown content in the sample.

**What rendered correctly:** headings (feeding `outline`), emphasis and strong text, ordered and unordered
lists, inline and fenced code, GFM pipe tables with column alignment, block quotes, links, thematic breaks
and PNG/SVG images.

**Images** need the `scope: (image: ...)` override shown above. Without it, paths resolve relative to the
cmarker package rather than the project. The override is also a natural place to set a default size.

**Scriban on top** works as a .NET pre-step:

```
template.md.sbn + model (JSON) ──Scriban──► release-notes.md ──cmarker in .typ layout──► PDF
```

- Scriban rendering took about 69 ms, including first-call JIT, in a `dotnet run` file-based app.
- Data must be **Markdown-escaped**, the same problem as escaping Typst, one layer up. Backslash-escaping
  every ASCII punctuation character is always valid CommonMark, and folding newlines into spaces makes data
  render literally. All injection strings in the model rendered as literal text: emphasis, links, images,
  raw-typst comments, Typst markup, pipes inside tables.
- Typstium could provide a `MarkdownEscape` helper and a Scriban `md` filter.

**Feasibility verdict:** feasible with almost no engine work. Typstium's part is:

1. Documenting and sampling the "layout `.typ` + content `.md`" pattern.
2. Making it easy to put the `.md` in the sandbox (a virtual file).
3. Vendoring cmarker for offline builds.
4. Optionally, a Scriban-based package with escaping built in.

A native Markdig → Typst renderer isn't needed.

## 3. Security finding: cmarker `raw-typst` defaults to `true`

With the default settings, Markdown can embed Typst code in `<!--raw-typst ... -->` comments, and cmarker
runs it with `eval`. Probes in `spike/security/root/`:

| Layout | Markdown contains `<!--raw-typst #raw(read("/appsettings.txt")) -->` |
| --- | --- |
| `cmarker.render(md)` (default) | Code runs; `read` fails only because paths resolve inside the cmarker package |
| `scope: (read: read)` | Same; the built-in still resolves relative to the package |
| `scope: (read: (p) => read(p))` | **File contents from the project root end up in the PDF** |
| `raw-typst: false` | Comment is inert; nothing runs |

The README itself recommends wrapper closures like the third row for `image`, so a well-meaning layout can
easily widen what injected code can reach. The sandbox root still limits the damage to files we put there.
Injected code can still change layout, spoof content or burn CPU.

**Rule:** layouts that render user-editable Markdown set `raw-typst: false`. Typstium's samples and
documentation should do the same. The template validation step could also flag the `raw-typst` default.

## 4. Offline packages

Typst 0.15.1 has **no offline switch**. Behavior observed with a dead proxy (`HTTPS_PROXY=http://127.0.0.1:9`):

| Setup | Result |
| --- | --- |
| Package in `--package-path` (vendored), empty cache | Compiles; no network attempt |
| Package only in `--package-cache-path` | Compiles; no network attempt |
| Package missing | `error: failed to download package (https://packages.typst.org/...: Connection Failed ...)` |

**Recommendations**

- Vendor allowed packages into a folder laid out as `<namespace>/<name>/<version>/` and pass it as
  `--package-path`. Also pass an empty or controlled `--package-cache-path`, so the user's machine-wide cache
  doesn't hide a missing vendored package.
- For user-supplied templates, set the child process's `HTTPS_PROXY`, `HTTP_PROXY` and `ALL_PROXY` to an
  address that can't be reached, and clear `NO_PROXY`. Any package not vendored then fails fast with a clear
  diagnostic instead of downloading. This acts as a package allow-list.

## 5. Diagnostics

Raw captures for 14 cases (syntax, unknown variable, hint, root escape, warning, error in imported file,
loops, recursion, missing package, multiple errors, bad image) are in `spike/diagnostics/output/`.

**`short` format:** `<path>:<line>:<col>: <severity>: <message>`, one line per diagnostic.

```
\\?\C:\...\root\04-outside-root.typ:1:6: error: path `"../secret.txt"` would escape the project root
```

**`human` format** carries more than `short`:

```
error: unknown variable: a-b
  ┌─ \\?\C:\...\root\03-hint.typ:3:2
  │
3 │ #{a-b}
  │   ^^^
  │
  = hint: if you meant to use subtraction, try adding spaces around the minus sign: `a - b`
```

```
error: cannot add string and integer
  ┌─ \\?\C:\...\root\sub\helper.typ:2:2
  ...
  while calling `greet` at \\?\C:\...\root\06-error-in-import.typ:2:1
```

| Information | `short` | `human` |
| --- | :---: | :---: |
| Path, line, column, severity, message | ✓ | ✓ |
| Hints (`= hint: ...`) | ✗ | ✓ |
| Call trace (`while calling ... at path:line:col`) | ✗ | ✓ |
| Span length (caret underline → end column on the same line) | ✗ | ✓ |
| Source snippet | ✗ | ✓ |

**Observations for the `TypstDiagnostic` model**

- **Paths:**
  - On Windows, paths are absolute with the `\\?\` prefix, even for relative input.
  - stdin shows as `<root>\<stdin>`.
  - Package files show as `@preview/cmarker:0.1.10/lib.typ`.
  - The parser must strip `\\?\`, handle the drive-letter colon, and map paths back to the caller's virtual
    file names (and to the original project path when compiling in a sandbox).
- **No error codes.** Typstium assigns its own stable IDs, as planned (`TYPST0001`, ...). The message text
  changes between Typst versions, so codes can't be derived from it reliably. A generic error and warning
  code is the safe start.
- **Several errors per run** happen for syntax errors (4 in `01-syntax`). Evaluation stops at the first
  error (`10-multiple-errors` reports only one).
- **Warnings don't fail the run** (exit 0) and are printed alongside the output.
- **Exit codes:** `0` success, possibly with warnings; `1` compile error; `2` invalid CLI arguments. Exit 2
  means a Typstium bug, not a template bug, and should surface as such.
- **Argument order:** `--color` is a global option and must come before the subcommand
  (`typst --color never compile ...`). Placed after it, Typst exits with code 2. The core should always pass
  `--color never`.
- **Shape this suggests** (Roslyn-like, maps 1:1 to MSBuild canonical errors): `Id`, `Severity`, `Message`,
  `Location { Path, Start (Line, Column), End (Line, Column)? }`, `Hints[]`, `Trace[] { Message, Location }`.
  Parse `human`; keep `short` as a fallback parser.

## 6. Resource limits

| Template | Result |
| --- | --- |
| `#while true {}` | Rejected at compile time: "condition is always true" (~0.1 s) |
| `#while i >= 0 { i += 1 }` | Typst's own guard: "loop seems to be infinite" (~0.1 s) |
| Unbounded recursion | Typst's own guard: "maximum function call depth exceeded" |
| Nested loops of 100 000 × 100 000 | **Ran until killed by the 15 s timeout** (exit 124) |
| 20 000 pages | Compiled in 0.6 s into a **13.7 MB** PDF |

Typst's guards catch obvious mistakes, not deliberately heavy templates. The design's wall-clock timeout
with process-tree kill is required, and a maximum output size (or page count) is worth having. The spike
didn't measure memory.

## 7. Distribution findings

- **Release assets for 0.15.1:** `x86_64`/`aarch64` Windows (zip), `x86_64`/`aarch64` macOS, `x86_64`/
  `aarch64`/`armv7` Linux **musl**, and `riscv64gc` Linux **gnu** (tar.xz). That gives seven .NET RIDs:
  `win-x64`, `win-arm64`, `osx-x64`, `osx-arm64`, `linux-x64`, `linux-arm64`, `linux-arm`.
- **The musl build runs on glibc.** The `x86_64-unknown-linux-musl` binary is `static-pie linked` and runs
  unchanged on Ubuntu 24.04 (glibc 2.39). One package can serve `linux-x64` and `linux-musl-x64`.
- **Checksums:** Typst publishes no checksum files, but the GitHub API returns a `digest`
  (`sha256:...`) for each asset (`gh api repos/typst/typst/releases/tags/v0.15.1`). Packaging CI can pin and
  verify against it.
- **Size:** compressed assets are 14–22 MB. The unpacked Windows binary is 52 MB, so a `Runtime.All`
  package would be large, as the design doc expects.
- `typst info --format json` returns version, commit, platform, enabled features, font settings and
  package paths in one call. It's a better basis for capability detection than `--version`.

## 8. Corrections to the design doc

| Design doc says | Typst 0.15.1 reality |
| --- | --- |
| "`typst query` is exposed for metadata extraction" | `query` is deprecated and hidden from `--help`. Use `typst eval '<expr>' --in doc.typ`, which evaluates an expression in a document's context and returns JSON or YAML, e.g. `query(heading.where(level: 1)).map(h => h.body)` (verified) |
| "`--make-deps`, or the newer deps flag" | `--deps <path\|->` with `--deps-format json\|zero\|make` (default `json`). Paths mix absolute `\\?\` and relative forms with mixed separators; **font files aren't listed**, so MSBuild must add font folders as inputs itself |
| "Check whether the CLI has a true offline switch" | It doesn't; use vendored `--package-path` plus a dead proxy (section 4) |
| "Parse `--diagnostic-format short`" | Prefer `human`; `short` drops hints and traces (section 5) |
| Six RIDs | Seven (adds `linux-arm` from armv7 musl) |
| Server-mode gate | Measured: ~35 ms per PDF, ~60 PDFs/s. Gate not triggered |
| HTML export "once it stabilizes" | Still behind `--features html` in 0.15.1, as is `bundle` |

Other CLI capabilities worth exposing that the design doc doesn't mention: `--ignore-embedded-fonts`,
`--pages`, `--no-pdf-tags`, `--creation-timestamp` (same as `SOURCE_DATE_EPOCH`), `--jobs`, `--timings`.

## 9. Not covered by this spike

- .NET-side process overhead (`Process.Start`, async stream reads) and the .NET Framework polyfills.
- NuGet extraction dropping the executable bit, and single-file publish layout.
- IIS, Job Objects, memory caps.
- macOS.
- Memory use of heavy templates.
- Behavior with Typst versions older than 0.15 (cmarker 0.1.10 already requires 0.15).

These belong to the foundation work and its tests.
