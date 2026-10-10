# Runtime packages: Typst binaries through NuGet

**Status:** Proposed · Oct 10, 2026 · Magnus Lindhe
**Part of:** [Typstium design overview](../design.md)
**Builds on:** [Foundation](foundation.md) (`BundledRuntimeLocator`), [Cake.Sdk build](cake-sdk-build.md)

## 1. Purpose and scope

An app that references Typstium should get the official Typst CLI for its platform from NuGet, with no
manual install, and the compiler should find it without configuration. This spec covers:

- `Typstium.Runtime.<rid>`: one package per platform, holding the unmodified Typst binary.
- `Typstium.Runtime.All`: a meta package that depends on every RID package.
- MSBuild props and targets in the core `Typstium` package that choose, copy and describe the binaries.
- Changes to `BundledRuntimeLocator`: a runtime manifest and emulation fallbacks.
- Cake tasks that pin, download, verify and pack the binaries.
- A catalog of the scenarios the packages must handle, used for documentation and tests (section 9).

**Goals**

- `dotnet add package Typstium.Runtime.linux-x64` is all a typical app needs.
- Correct output for build, publish, single-file, containers, .NET Framework and IIS.
- Behavior controlled by MSBuild properties with the `Typstium` prefix.
- Reproducible packages: the same commit packs the same, checksum-verified bytes.
- Every scenario in the catalog has a stated expected result and a named way to verify it.

**Out of scope**

- `Typstium.Build` (the host binary for build-time compiles) and `Typstium.Tool`. Both need investigation
  of their own (section 11).
- `Cake.Typstium`. Its requirement on this spec is recorded in section 11.
- Package signing and publishing to nuget.org. Both come before the first public release.
- Download on first use (resolution chain step 5 in the design overview).
- Automated Typst upgrades (a scheduled `UpdateTypstRuntimes`).

## 2. Decisions

| # | Decision | Choice | Rejected | Why |
|---|---|---|---|---|
| D1 | RIDs | Seven: `win-x64`, `win-arm64`, `osx-x64`, `osx-arm64`, `linux-x64`, `linux-arm64`, `linux-arm` | `win-x86` (Typst ships none); `linux-riscv64` (later) | The assets of Typst 0.15.1 (spike 0). Linux uses the static musl builds, which run on glibc too |
| D2 | Bundle of all RIDs | `Typstium.Runtime.All` is a meta package with dependencies only | One package with every binary | Restore fetches only the RID packages a project references; `Runtime.All` reuses them instead of duplicating binaries |
| D3 | Where the MSBuild logic lives | In the core `Typstium` package; RID packages only declare an item | Targets in every RID package; a separate `Typstium.Runtime.Build` package | One copy of the logic. The manifest writer (targets) and reader (locator) ship together. RID packages version purely by Typst |
| D4 | Selecting binaries | Filter by `RuntimeIdentifier`; copy every referenced RID when none is set | Always copy all; a selector package | Lean single-RID publishes, and builds without a RID keep working (option B in brainstorming) |
| D5 | Output folder | `TypstiumRuntimeDirectory`, a pattern relative to the output folder, default `typst/{rid}` | A fixed folder only | Avoids collisions such as an app's own `typst/` template folder |
| D6 | Telling the app about a custom folder | A manifest file, `typstium.runtime.json`, next to the app | `runtimeconfig.json` (no .NET Framework); assembly attribute (`GetEntryAssembly()` is null on IIS, the test host under `dotnet test`); no custom folder | The same mechanism on every host; deployed with the binaries |
| D7 | When the manifest is written | Always, whenever at least one runtime is copied | Only when `TypstiumRuntimeDirectory` isn't the default | A reused publish folder can't keep a stale manifest that overrides the default layout (decided in spec review). The conventional folders remain for binaries copied by hand |
| D8 | Flat layout | No separate setting; a pattern without `{rid}` gives it for single-RID builds | `TypstiumRuntimeLayout=Flat` | The pattern covers it; a pattern without `{rid}` and two RIDs is an error |
| D9 | Manifest access in the locator | Typed: `ILocatorEnvironment.ReadRuntimeManifest(directory)` returning `RuntimeManifestResult`; validation in pure `RuntimeManifest.Parse` | `ReadAllText(path)` | Least capability; the security rules sit in one pure function that's easy to test exhaustively |
| D10 | Manifest authority | A manifest, when present, is authoritative: conventional folders aren't searched | Manifest first, then conventional folders | A stale `typst/` folder from an earlier deploy can't win |
| D11 | Emulation fallback | `win-arm64` → `win-x64`, `osx-arm64` → `osx-x64` | Native only | A win-x64 or osx-x64 app on ARM hardware runs the emulated binary instead of finding nothing |
| D12 | `win-x86` apps | Select every referenced `win-*` runtime | Nothing (no match) | A 32-bit app runs on x64 or ARM64 Windows, and the build can't know which |
| D13 | Pinning | Committed lock file `build/typst-runtimes.json`; only `UpdateTypstRuntimes` talks to the GitHub API | Digests fetched at pack time; committed binaries | Reproducible packs; a re-uploaded asset fails the build; upgrades are reviewable diffs |
| D14 | Package version | Typst's version (`0.15.1`); a fourth part only for a repack of the same Typst version | Own semver | The package is Typst's binary; its version should say which one |
| D15 | Binary location in the package | `tools/typst/<rid>/` | `runtimes/<rid>/native/` | NuGet's native asset handling is unreliable for executables and on .NET Framework. `tools/` also serves Cake's `#tool` |
| D16 | Task names | `UpdateTypstRuntimes`, `DownloadTypstRuntimes`, `PackTypstRuntimes` | `Fetch…`, `PackRuntimes` | Consistent names that say what each task does |
| D17 | Where targets tests get packages | Fixture runtime packages with a stub executable | The real packages | Fast, offline and runnable on every OS; real packages are tested end to end (section 10) |
| D18 | Downloading and extracting assets | Own small downloader in `Build.Cake.Extensions` (`HttpClient`, streaming SHA-256, https-only redirects, retries, stall timeout; ideas taken from Cake.Download.Module's `HttpDownloader`), SharpCompress for xz, the BCL for zip and tar; only `typst[.exe]` and `LICENSE` are extracted, by name, to paths the build chooses | Cake.Download.Module | The module makes tools runnable on the build machine: host platform only, registered with Cake's tool locator, no xz (it may not take dependencies). Packaging fetches seven platforms' files. The build project may take dependencies; core and the runtime packages gain none |

## 3. Packages

### `Typstium.Runtime.<rid>`

```
tools/typst/<rid>/typst[.exe]            the official binary, unmodified
tools/typst/<rid>/LICENSE                Typst's Apache-2.0 license
build/Typstium.Runtime.<rid>.props       for packages.config (direct references only)
buildTransitive/Typstium.Runtime.<rid>.props
README.md
```

Both props files are identical and only declare the binary:

```xml
<Project>
  <ItemGroup>
    <TypstiumRuntime Include="linux-x64"
                     Executable="$(MSBuildThisFileDirectory)../tools/typst/linux-x64/typst"
                     TypstVersion="0.15.1"
                     ContractVersion="1" />
  </ItemGroup>
</Project>
```

- No dependencies. `DevelopmentDependency` is false, because the binary has to reach the consuming app.
- License `Apache-2.0` (the contents are Typst's), with Typst's `LICENSE` file. The README says which Typst
  version it holds, where it came from, and that it's repackaged unchanged.
- Version: Typst's (`0.15.1`). A repack of the same Typst version, needed only when the package layout itself
  was wrong, uses a fourth part (`0.15.1.1`).
- Referenced without the core `Typstium` package, a RID package copies nothing (there's no locator then
  either). Consumers that only want the file, such as Cake's `#tool`, read `tools/` directly.

### `Typstium.Runtime.All`

No content. Depends on all seven RID packages at exactly its own version (`[0.15.1]`).

### `Typstium` (core) additions

```
build/Typstium.props, build/Typstium.targets                    for packages.config
buildTransitive/Typstium.props, buildTransitive/Typstium.targets
```

The `build/` files import the `buildTransitive/` ones, so both entry points run the same logic.

## 4. MSBuild properties

| Property | Default | Meaning |
|---|---|---|
| `TypstiumCopyRuntime` | `true` | Master switch. `false` copies nothing and writes no manifest |
| `TypstiumCopyRuntimeOnBuild` | `true` | Copy to the output folder on build |
| `TypstiumCopyRuntimeOnPublish` | `true` | Copy to the publish folder |
| `TypstiumRuntimeIdentifiers` | (empty) | Semicolon-separated list that replaces the automatic selection (section 5) |
| `TypstiumRuntimeDirectory` | `typst/{rid}` | Folder for each binary, relative to the output or publish folder. `{rid}` is replaced by the RID |

`TypstiumRuntimeDirectory` must be relative and have no `..` segment (the same rules the locator applies to
the manifest, section 7).

**Diagnostics**

| Code | Kind | When |
|---|---|---|
| `TYPSTIUM0001` | Warning | A RID is set, but no referenced runtime matches it |
| `TYPSTIUM0002` | Error | `TypstiumRuntimeDirectory` has no `{rid}` and more than one RID is selected |
| `TYPSTIUM0003` | Error | `TypstiumRuntimeDirectory` is absolute or contains `..` |
| `TYPSTIUM0004` | Warning | `TypstiumRuntimeIdentifiers` names a RID that no referenced package provides |
| `TYPSTIUM0005` | Warning | A `TypstiumRuntime` item has a `ContractVersion` this core doesn't know; the item is ignored |
| `TYPSTIUM0006` | Message | The target platform can't start processes (android, ios, maccatalyst, tvos, browser); nothing is copied |

## 5. Choosing RIDs

In order:

1. `TypstiumRuntimeIdentifiers` set → exactly that list (`TYPSTIUM0004` for names with no package).
2. `RuntimeIdentifier` set → map it to the shipped RIDs:
   - Portable RIDs map to themselves (`linux-x64` → `linux-x64`).
   - `linux-musl-<arch>` → `linux-<arch>`.
   - Older distro RIDs map by OS family and architecture (`ubuntu.22.04-x64` → `linux-x64`,
     `win10-x64` → `win-x64`, `osx.13-arm64` → `osx-arm64`).
   - `win-x86` → every referenced `win-*` runtime (D12).
   - No referenced runtime matches → `TYPSTIUM0001`, nothing is copied.
3. Neither set → every referenced runtime.

`RuntimeIdentifiers` (plural) only affects restore and is ignored. A multi-RID container publish
(`ContainerRuntimeIdentifiers`) runs one inner publish per RID, and each inner publish selects its own.

## 6. Copying and the manifest

**Items.** A target that runs before `AssignTargetPaths` turns each selected binary into a
`ContentWithTargetPath` item:

- `TargetPath`: `TypstiumRuntimeDirectory` with `{rid}` replaced, plus the file name.
- `CopyToOutputDirectory=PreserveNewest` (or `Never` when `TypstiumCopyRuntimeOnBuild` is false).
- `CopyToPublishDirectory=PreserveNewest` (or `Never` when `TypstiumCopyRuntimeOnPublish` is false).
- `ExcludeFromSingleFile=true`.

Standard MSBuild then provides incremental copying, `Clean` and `IncrementalClean` (through `FileWrites`),
the publish copy, single-file exclusion, and flow to referencing projects through `ProjectReference`.
Libraries and test projects copy too. That's what lets packages.config apps get the binary through a
library, and test projects are libraries.

**Manifest.** Whenever at least one runtime is selected, a target writes
`typstium.runtime.json` straight into `$(OutDir)` after `CopyFilesToOutputDirectory`
and into `$(PublishDir)` after `CopyFilesToPublishDirectory` (`WriteOnlyWhenDifferent`, registered in `FileWrites`).
As a content item it would flow through `ProjectReference`, and an app and a library would collide on it at
publish (`NETSDK1152`):

```json
{
  "version": 1,
  "runtimes": {
    "linux-x64": "tools/typst/typst"
  }
}
```

Paths use `/`, are relative to the manifest's folder, and name the executable itself.

**Execute bit.** On non-Windows build hosts, `chmod +x` runs on each copied binary after
`CopyFilesToOutputDirectory` and after the publish copy. A build on Windows can't set it; the locator's
runtime `EnsureExecutable` remains the fallback (RS12).

**Skipped automatically**

- Design-time builds (`DesignTimeBuild=true`).
- Platforms that can't start processes (`TYPSTIUM0006`).
- `TypstiumCopyRuntime=false`.

**Classic web projects.** The output already goes to `bin/`. A target adds the selected files to
`FilesForPackagingFromProject`, so WebDeploy/MSDeploy packages include them (RS17).

## 7. Locating the binary at runtime

`BundledRuntimeLocator` changes. The public API doesn't.

**Candidate RIDs**, from a pure `RuntimeIdentifierCandidates.For(rid)` next to `RuntimeIdentifierFor`:

| Native | Tried in order |
|---|---|
| `win-arm64` | `win-arm64`, `win-x64` |
| `osx-arm64` | `osx-arm64`, `osx-x64` |
| any other | itself |

**Search**

1. For each of `AppContext.BaseDirectory` and `BaseDirectory/bin`, call
   `ReadRuntimeManifest(directory)`:
   - `Invalid` → `NotFound` with `"<path> (<reason>)"`; the chain continues with the next locator.
   - `Loaded` → authoritative (D10): for each candidate RID in the manifest, the first executable that
     exists gets `EnsureExecutable` and is `Found`. None → `NotFound` listing the manifest and each path tried.
   - `Missing` → next directory.
2. No manifest in either place (binaries copied by hand, or built by an older core) → `typst/<rid>/` then
   `bin/typst/<rid>/`, for each candidate RID (today's behavior, extended with the fallbacks).

**Types**

```csharp
internal interface ILocatorEnvironment
{
    // ...existing members...

    /// <summary>Reads typstium.runtime.json in <paramref name="directory"/>; never throws for I/O or format errors.</summary>
    RuntimeManifestResult ReadRuntimeManifest(string directory);
}

internal abstract record RuntimeManifestResult
{
    public sealed record Missing(string Path) : RuntimeManifestResult;
    public sealed record Invalid(string Path, string Reason) : RuntimeManifestResult;
    public sealed record Loaded(string Path, RuntimeManifest Manifest) : RuntimeManifestResult;
}

internal sealed class RuntimeManifest
{
    public const string FileName = "typstium.runtime.json";

    /// <summary>RID → full executable path, every one inside the manifest's folder.</summary>
    public IReadOnlyDictionary<string, string> Executables { get; }

    /// <summary>Pure: no file system access.</summary>
    public static RuntimeManifestResult Parse(string path, string json, bool isWindows);
}
```

`SystemLocatorEnvironment.ReadRuntimeManifest` reads the file: not found → `Missing`; other `IOException` or
`UnauthorizedAccessException` → `Invalid("unreadable: …")`; otherwise `RuntimeManifest.Parse`.
`FakeLocatorEnvironment` takes JSON per directory and also goes through `Parse`, so a test can't describe a
manifest the parser would reject.

**Validation in `Parse`** (any failure → `Invalid` with a reason naming the RID and the value):

- Valid JSON, an object with `"version": 1` and a non-empty `"runtimes"` object of string values.
- Each path is relative: not fully qualified, not drive-relative (`C:x`), not root-relative (`\x`, `/x`).
- No `..` segment, before or after normalizing separators.
- `Path.GetFullPath(Path.Combine(manifestDirectory, entry))` starts with the manifest's folder plus a separator.

Not in v1: version or SHA256 checks from the manifest. `version` leaves room for them.

## 8. Packaging pipeline (Cake build)

**Lock file** `build/typst-runtimes.json`, one Typst version at a time:

```json
{
  "typst": "0.15.1",
  "runtimes": {
    "win-x64":     { "asset": "typst-x86_64-pc-windows-msvc.zip",          "sha256": "…" },
    "win-arm64":   { "asset": "typst-aarch64-pc-windows-msvc.zip",         "sha256": "…" },
    "osx-x64":     { "asset": "typst-x86_64-apple-darwin.tar.xz",          "sha256": "…" },
    "osx-arm64":   { "asset": "typst-aarch64-apple-darwin.tar.xz",         "sha256": "…" },
    "linux-x64":   { "asset": "typst-x86_64-unknown-linux-musl.tar.xz",    "sha256": "…" },
    "linux-arm64": { "asset": "typst-aarch64-unknown-linux-musl.tar.xz",   "sha256": "…" },
    "linux-arm":   { "asset": "typst-armv7-unknown-linux-musleabi.tar.xz", "sha256": "…" }
  }
}
```

The download URL is derived, `https://github.com/typst/typst/releases/download/v{typst}/{asset}`, so it
can't drift from the version. The RID → Rust target mapping lives in code; the lock file records what was
reviewed.

**Tasks** (Typstium specifics in `build/Build`, reusable parts in `Build.Cake.Extensions`):

- `UpdateTypstRuntimes --typst-version=<v>`: reads the release from the GitHub API (with `GITHUB_TOKEN` when
  set), fails if any of the seven assets is missing, and rewrites the lock file from each asset's `digest`.
  The only task that uses the API.
- `DownloadTypstRuntimes`: downloads each asset (D18) into
  `artifacts/cache/typst/<version>/`, reusing files whose digest already matches; verifies SHA256 against the
  lock file (mismatch → build fails); extracts the single `typst[.exe]` and Typst's `LICENSE`. Then runs the
  host RID's binary with `--version` and compares it with the lock file's `typst`.
- `PackTypstRuntimes` (depends on `DownloadTypstRuntimes`): packs the template project
  `src/Typstium.Runtime/Typstium.Runtime.csproj` (`IncludeBuildOutput=false`, no code) once per RID with
  `-p:TypstiumRuntimeIdentifier=<rid>`, generating the props from section 3, then packs
  `Typstium.Runtime.All`. Output: `artifacts/packages/`.

The default target doesn't pack runtimes, so a local `./build.ps1` stays fast.

**CI.** A `pack` job on ubuntu-latest runs `PackTypstRuntimes`, caches `artifacts/cache/typst/` keyed by the
lock file's hash, and uploads the packages as an artifact for the end-to-end jobs (section 10).

## 9. Scenario catalog

Each scenario has a stable ID. Tests carry `[Property("Scenario", "RSn")]`, so one scenario's tests can be run
with `--treenode-filter` and documentation can link to them.

**Tiers:** v1, later, no. **Verified by:** U = unit tests, T = targets tests with fixture packages, E =
end-to-end CI with real packages, M = manual checklist (section 10), D = documentation only.

### Modern .NET, SDK-style projects

| ID | Scenario | Expected | Tier | Verified |
|---|---|---|---|---|
| RS1 | `dotnet build` / `run` / F5, no RID | Every referenced runtime in `bin/…/typst/<rid>/`; the locator picks the native one | v1 | T, E |
| RS2 | `dotnet publish`, no RID (portable) | As RS1, in the publish folder | v1 | T |
| RS3 | `dotnet publish -r <rid>` (framework-dependent or self-contained) | Only the matching runtime; distro and musl RIDs mapped (section 5); `TYPSTIUM0001` when none matches | v1 | T, E |
| RS4 | Single-file publish | Binary next to the app, not in the bundle | v1 | T, E |
| RS5 | Native AOT or trimmed publish | As RS4 | v1 | T |
| RS6 | Multi-targeting (`net8.0;net48`) | Each TFM's output has its own copy | v1 | T |
| RS7 | RID set in the project file, plain build | As RS3, at build time | v1 | T |
| RS8 | `dotnet run -r`, or a launch profile with a RID | As RS7 | v1 | T |

### Containers and cloud

| ID | Scenario | Expected | Tier | Verified |
|---|---|---|---|---|
| RS9 | Dockerfile running `dotnet publish -r linux-x64` | As RS3; compiles in the image | v1 | E |
| RS10 | SDK container publish (`/t:PublishContainer`), also multi-arch | Each image holds its own RID's binary | v1 | E |
| RS11 | Alpine image (`linux-musl-x64`) | `linux-x64` binary selected and runs | v1 | T, E |
| RS12 | Built on Windows without a RID, files copied into a Linux image | Runs if the root filesystem is writable (runtime `chmod`); documented fix otherwise (publish with `-r` inside the image, or `chmod` in the Dockerfile) | v1 | M, D |
| RS13 | Image already contains Typst | `TypstiumCopyRuntime=false` or no runtime package; TYPST_PATH or PATH used | v1 | T, D |
| RS14 | Azure App Service (Windows, Linux), Azure Functions isolated worker, AWS Lambda zip | As RS2 or RS3; Lambda needs the execute bit (build on Linux or `-r`) | v1 (Lambda: D) | M, D |

### ASP.NET and .NET Framework

| ID | Scenario | Expected | Tier | Verified |
|---|---|---|---|---|
| RS15 | ASP.NET Core on modern .NET under IIS | As RS2 or RS3 | v1 | M |
| RS16 | ASP.NET Core 2.x on .NET Framework, SDK-style, under IIS | Binary under the site; found through `bin/` | v1 | M |
| RS17 | Classic System.Web, old-style csproj, PackageReference; WebDeploy | `bin/typst/<rid>/`; included in the WebDeploy package | v1 | T, M |
| RS18 | Classic csproj with packages.config | Works with direct references to `Typstium` and the RID packages; `Typstium.Runtime.All` isn't supported there | v1 | T, D |
| RS19 | .NET Framework console or service, AnyCPU with `Prefer32Bit` | x64 binary found (RID from the OS architecture) and runs | v1 | E |

### Desktop and installers

| ID | Scenario | Expected | Tier | Verified |
|---|---|---|---|---|
| RS20 | WPF or WinForms on .NET or .NET Framework | As RS1 or RS7 | v1 | T |
| RS21 | ClickOnce | Binary in the application manifest | later | — |
| RS22 | MSIX / Windows Application Packaging | Binary in the package and startable | later | — |
| RS23 | Velopack, Inno Setup, WiX over the publish folder | As RS2 or RS3 | v1 | D |
| RS24 | MAUI, iOS, Android, Blazor WASM | Nothing copied, `TYPSTIUM0006`, the build succeeds | no | T |

### Libraries, tests and dependency shapes

| ID | Scenario | Expected | Tier | Verified |
|---|---|---|---|---|
| RS25 | App → class library → runtime package (transitive) | App output has the binary; the library's output has it too | v1 | T |
| RS26 | A library package that leaves the binary choice to the app | Documented: reference `Typstium`, not a runtime package | v1 | D |
| RS27 | Test projects run on CI across OSes | As RS1 | v1 | T, E |
| RS28 | Central Package Management, private feeds, offline restore | Nothing special | v1 | D |

### Customization

| ID | Scenario | Expected | Tier | Verified |
|---|---|---|---|---|
| RS29 | Copy turned off, or on build only, or on publish only | Per the switches in section 4 | v1 | T |
| RS30 | `TypstiumRuntimeIdentifiers` overrides the selection | Exactly the listed runtimes; `TYPSTIUM0004` for unknown ones | v1 | T |
| RS31 | Custom `TypstiumRuntimeDirectory` | Binaries in the pattern's folders, the manifest points at them, locator finds them; `TYPSTIUM0003` for invalid patterns | v1 | T, U, E |
| RS32 | Flat layout (no `<rid>` folder) | Covered by RS31 with a pattern without `{rid}`; `TYPSTIUM0002` with two RIDs | v1 | T |
| RS33 | Design-time builds (Visual Studio, Rider) | Nothing copied | v1 | T |
| RS34 | Incremental build and `Clean` | No copy when unchanged; `Clean` removes binaries and manifest | v1 | T |

### Platform and security edges

| ID | Scenario | Expected | Tier | Verified |
|---|---|---|---|---|
| RS35 | Building on Unix | Copied binaries are executable | v1 | T |
| RS36 | macOS Gatekeeper and code signing | Typst's signature survives the copy; no quarantine flag; the binary starts | v1 | M |
| RS37 | AppLocker/WDAC, Defender, SmartScreen on locked-down Windows | Documented: an unsigned third-party exe in the app folder | v1 | D |
| RS38 | `win-x86` OS, `linux-riscv64`, FreeBSD | No package; the locator reports not found | no (riscv: later) | — |
| RS39 | A Typst version other than the one `Runtime.All` pins | Reference a different RID package version directly; core enforces the 0.15 minimum | v1 | D |
| RS40 | `win-x86` app (publish `-r win-x86`) on 64-bit Windows | Every referenced `win-*` runtime copied; x64 binary found and runs | v1 | T, U |
| RS41 | win-x64 app on Windows ARM64; osx-x64 app on Apple silicon | Emulated binary found through the fallback when only it was referenced | v1 | U, E |

## 10. Testing

**Unit tests** (`tests/Typstium.UnitTests`)

- `RuntimeManifest.Parse`: every rule in section 7, under both Windows and Unix path rules.
- `RuntimeIdentifierCandidates.For`.
- `BundledRuntimeLocator` through the fake: manifest authority, `bin/` as second location, `Invalid` reported,
  conventional folders without a manifest, fallbacks.
- `SystemLocatorEnvironment.ReadRuntimeManifest` against real files: missing, unreadable, loaded.

**Targets tests** (new `tests/Typstium.Runtime.Tests`)

- Generate small projects in a temp folder and run real `dotnet build` and `dotnet publish` (and
  `msbuild.exe` for classic projects on Windows).
- Use fixture runtime packages, packed from the same template with a stub executable, from a local feed with
  a persistent `NUGET_PACKAGES` folder isolated from the user's (`artifacts/runtime-tests/nuget-packages`). The
  packages under test get a unique version per session (`0.0.0-t<UTC timestamp>`), so a stale fixture can't be
  served. End-to-end tests use a fresh cache for each session.
- Assert the output tree golden-file style, the manifest content, and the diagnostic codes.
- Distro and versioned RIDs (`ubuntu.22.04-x64`, `win10-x64`, `osx.13-arm64`) are built with
  `UseRidGraph=true` and `UseAppHost=false`, because .NET 8+ rejects non-portable RIDs otherwise and no apphost exists
  for them.
- RID mapping (section 5) is exercised through these builds, one case per mapping rule.

**End-to-end CI** (real packages from the `pack` job, compiling a real PDF)

- linux-x64 publish run in `alpine` and `ubuntu` containers (RS9, RS11); SDK container publish (RS10).
- Single-file publish run on each OS (RS4).
- net48 console on windows-latest (RS19).
- Emulation on real hardware (RS41): `macos-latest` (arm64) with only `osx-x64` referenced, and
  `windows-11-arm` with only `win-x64`. These runs also record what `RuntimeInformation.OSArchitecture`
  reports under emulation (risk R1).

**Manual checklist** (`docs/runtime-packages-checklist.md`, each entry with a date and result once run)

- RS12, RS14 (App Service, Functions), RS15, RS16, RS17 (WebDeploy), RS36.

## 11. Related work

- **`Cake.Typstium`** gets its binary from the runtime packages
  (`#tool nuget:?package=Typstium.Runtime.<rid>`, reading `tools/typst/<rid>/`), so it needs no xz support.
  Downloading from Typst's releases through Cake.Download.Module becomes an option only if the module gains
  xz. Its own spec decides the details.
- **`Typstium.Build`** needs a binary for the build machine, which may differ from the target's
  (build on Windows, deploy to Linux). Whether it reuses the RID packages, and how it chooses the host RID,
  needs investigation.
- **`Typstium.Tool`** (`dnx` / global tool) needs investigation: how a tool package can carry or find
  per-platform binaries.

## 12. Risks

| # | Risk | Mitigation |
|---|---|---|
| R1 | Older .NET versions report the emulated architecture from `OSArchitecture`, so the native RID is wrong under emulation | Harmless for D11 (the emulated binary runs); measured on real hardware in the RS41 jobs |
| R2 | `tar.xz` extraction in the Cake build: .NET has no xz decoder | SharpCompress in `Build.Cake.Extensions` only (D18) |
| R3 | A publish folder reused after switching directories keeps stale binaries (publish folders aren't cleaned) | The manifest is always rewritten (D7), so it points at the current binaries; stale ones are unused. Documented: publish to a clean folder |
| R4 | Seven packages of 14–22 MB each strain restore on slow feeds | Documented; `Runtime.All` is opt-in |
| R5 | packages.config imports only direct references' `build/` folders | Supported only with direct references (RS18) |
