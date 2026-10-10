# Runtime Packages Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the official Typst CLI through NuGet as `Typstium.Runtime.<rid>` packages plus a `Typstium.Runtime.All`
meta package. MSBuild targets in the core `Typstium` package copy the binaries next to the app and write
`typstium.runtime.json`, and `BundledRuntimeLocator` reads that manifest at runtime.

**Architecture:**

- **Core (`src/Typstium`)** gains:
  - a pure `RuntimeIdentifierCandidates` (emulation fallbacks)
  - a pure `RuntimeManifest.Parse` with the path-safety rules
  - `ILocatorEnvironment.ReadRuntimeManifest`
  - a rewritten `BundledRuntimeLocator`
  - `build/` and `buildTransitive/` targets that select, copy and describe the runtimes
- **RID packages** are packed from one template project (`src/Typstium.Runtime`), and each carries only Typst's binary and
  a props file that declares a `TypstiumRuntime` item. `src/Typstium.Runtime.All` depends on all seven.
- **The Cake build** pins the assets in `build/typst-runtimes.json`, downloads and verifies them with its own downloader,
  extracts with SharpCompress (xz) and the BCL, and packs.
- **Tests:**
  - unit tests (core and the build library)
  - targets tests against fixture packages with stub binaries (`tests/Typstium.Runtime.Tests`)
  - end-to-end tests against the real packages (`tests/Typstium.Runtime.EndToEndTests`)
  - a catalog test that checks every scenario has a test

**Tech Stack:**

- .NET 10 SDK (`global.json` 10.0.401). Core targets `netstandard2.0;net8.0`.
- MSBuild props and targets (no compiled tasks)
- TUnit 1.73.5 on Microsoft.Testing.Platform
- Cake.Sdk 6.3.0, SharpCompress 1.0.0 (build only), `System.Formats.Tar` and `System.IO.Compression` (BCL)

**Spec:** [`docs/specs/runtime-packages.md`](../specs/runtime-packages.md). Section numbers (§n), decisions (Dn), scenarios
(RSn) and risks (Rn) below refer to it. Refinements found while planning are listed below. The task that owns each
refinement writes it into the spec.

## Global Constraints

- **Branch and commits**
  - Work on `feature/runtime-packages`, branched from `docs/runtime-packages-spec` (spec and plan travel with the code), with
    one pull request at the end. No pushing or PR creation without the user's confirmation.
  - Commits end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- **The core library**
  - No new dependencies, and the public API is unchanged: `PublicAPI.*.txt` must not change. Everything new is `internal`.
  - Core code must compile for `netstandard2.0` and `net8.0`, and unit tests run on `net10.0` and, on Windows, `net472`.
  - The root `Directory.Build.props` applies (`TreatWarningsAsErrors`, `Nullable`, `LangVersion latest`). Never silence
    warnings wholesale. A narrowly scoped `NoWarn` with a comment is allowed where a step says so.
- **MSBuild names, exactly as in the spec**
  - Item: `TypstiumRuntime` with metadata `Executable`, `TypstVersion`, `ContractVersion` (`1`).
  - Properties: `TypstiumCopyRuntime`, `TypstiumCopyRuntimeOnBuild`, `TypstiumCopyRuntimeOnPublish`,
    `TypstiumRuntimeIdentifiers`, `TypstiumRuntimeDirectory` (default `typst/{rid}`).
  - Diagnostics: `TYPSTIUM0001` (warning, RID set but no runtime matches), `TYPSTIUM0002` (error, no `{rid}` with more than one
    RID), `TYPSTIUM0003` (error, directory absolute or with `..`), `TYPSTIUM0004` (warning, listed RID not provided),
    `TYPSTIUM0005` (warning, unknown `ContractVersion`), `TYPSTIUM0006` (message, platform can't start processes).
- **RIDs:** exactly seven: `win-x64`, `win-arm64`, `osx-x64`, `osx-arm64`, `linux-x64`, `linux-arm64`, `linux-arm`.
- **Manifest:** file name `typstium.runtime.json`, `"version": 1`, paths relative with `/`, written whenever at least one
  runtime is selected (D7).
- **Packages:** `Typstium.Runtime.<rid>` is versioned as Typst's version (`0.15.1`) and licensed `Apache-2.0`, with the
  binary under `tools/typst/<rid>/`. `Typstium.Runtime.All` depends on `[<version>]` of each.
- **Cake tasks:** `UpdateTypstRuntimes` (`--typst-version=<v>`), `DownloadTypstRuntimes` and `PackTypstRuntimes`, plus test
  targets `RuntimeTests` and `RuntimeEndToEndTests`. None are part of `Default`.
- **Build library rules** (`build/Build.Cake.Extensions` never mentions Typstium):
  - Typstium values live in `build/Build/TypstRuntimes.cs`.
  - Pure units take no `ICakeContext`.
- **Tests**
  - Test names are `Sentence_with_underscores`, and assertions use `await Assert.That(...)`.
  - Every test that verifies a scenario carries `[Property("Scenario", "RSn")]` (one attribute per scenario).
  - OS-specific tests use the repo's pattern: `if (!OperatingSystem.IsWindows()) { Skip.Test("…"); return; }`.
- **Invariant formatting:** all number formatting uses `CultureInfo.InvariantCulture`, because the developer's machine may
  be `sv-SE`.

## Refinements to the spec (the owning task writes them into the spec)

1. **The manifest is written straight to the output and publish folders, not added as a content item** (Task 6, §6).
   As a content item it would flow through `ProjectReference` to referencing apps. An app and a library would then each
   contribute a `typstium.runtime.json` from different sources to the same relative path, and publish fails with
   `NETSDK1152`.
   - A target writes it after `CopyFilesToOutputDirectory` (into `$(OutDir)`) and after `CopyFilesToPublishDirectory`
     (into `$(PublishDir)`), with `WriteOnlyWhenDifferent`, and registers it in `FileWrites`.
   - The binaries stay `ContentWithTargetPath` items.
2. **Lock file keys are generic** (Task 10, §8): `{ "version": "0.15.1", "assets": { "<rid>": { "name": "…", "sha256": "…" } } }`.
   That lets the lock model live in the Typstium-agnostic build library, where it gets tests.
3. **The test package cache** (Task 5, §10). Targets tests use a persistent cache isolated from the user's
   (`artifacts/runtime-tests/nuget-packages`) and give the packages they build a unique version for each test session
   (`0.0.0-t<UTC timestamp>`). Dependencies such as System.Text.Json stay cached between runs, and stale fixture
   packages can't be picked up. End-to-end tests use a fresh cache for each session, because the real packages keep
   version `0.15.1` between repacks.
4. **Distro and legacy RIDs in targets tests** (Task 7). The tests build them with `UseRidGraph=true` and `UseAppHost=false`,
   because .NET 8+ rejects non-portable RIDs otherwise and no apphost exists for them.
5. **Arm runners** (Task 15, §10). GitHub's `windows-11-arm` hosted runners are free for public repositories only. While the
   repository is private, the Windows half of RS41 is a manual check, and its CI job runs only when
   `github.event.repository.private == false`. The `macos-latest` (arm64) half runs everywhere.

## Review Focus

These are the input classes and failure modes the spec implies but no scenario names. Each has a test in the owning task.

1. **Paths with spaces and non-ASCII characters** in the project and output folders (`My App ü/`). The `chmod` `Exec`
   quoting and the manifest's JSON must survive them. *Expected:* builds succeed, and the manifest parses. (Task 9,
   `Projects_in_folders_with_spaces_and_non_ascii_characters_copy_and_describe_their_runtimes`.)
2. **Backslashes in `TypstiumRuntimeDirectory`** on Windows (`tools\typst\{rid}`). *Expected:* the manifest uses `/`, and the
   locator finds the binary. (Task 8, `A_directory_with_backslashes_is_written_with_forward_slashes`.)
3. **RIDs in mixed case** (`Linux-X64` as `RuntimeIdentifier`, `WIN-X64` in `TypstiumRuntimeIdentifiers`). *Expected:*
   treated as lowercase. (Task 7, `Runtime_identifiers_are_matched_ignoring_case`.)
4. **A manifest with a UTF-8 BOM or CRLF line endings**, for example after hand editing. *Expected:* it parses. (Task 2,
   `A_byte_order_mark_and_crlf_line_endings_are_accepted`.)
5. **Republishing into the same folder with a different RID** (`-r linux-x64`, then `-r win-x64`, then `-r linux-x64`
   again). *Expected:* the manifest names only the last RID. (Task 7,
   `Republishing_into_the_same_folder_rewrites_the_manifest_for_the_last_runtime`.)

Also covered, in Task 14: an app started from a different working directory still finds its bundled binary (the
locator uses `AppContext.BaseDirectory`, not the current directory).

## File Map

| Path | Task | Responsibility |
|---|---|---|
| `src/Typstium/Executable/RuntimeIdentifierCandidates.cs` | 1 | Native RID first, then the emulated one |
| `src/Typstium/Executable/RuntimeManifest.cs` | 2 | `RuntimeManifest`, `RuntimeManifestResult`, pure `Parse` |
| `src/Typstium/Executable/ILocatorEnvironment.cs` | 3 | `+ ReadRuntimeManifest(directory)` |
| `src/Typstium/Executable/SystemLocatorEnvironment.cs` | 3 | Reads the file |
| `src/Typstium/Executable/Locators.cs` | 4 | `BundledRuntimeLocator` rewritten |
| `tests/Typstium.UnitTests/Executable/RuntimeIdentifierCandidatesTests.cs` | 1 | |
| `tests/Typstium.UnitTests/Executable/RuntimeManifestTests.cs` | 2 | |
| `tests/Typstium.UnitTests/Executable/SystemLocatorEnvironmentTests.cs` | 3 | `+` manifest file tests |
| `tests/Typstium.UnitTests/Testing/FakeLocatorEnvironment.cs` | 3 | `+ Manifests` |
| `tests/Typstium.UnitTests/Executable/BundledRuntimeLocatorTests.cs` | 4 | |
| `src/Typstium.Runtime/Typstium.Runtime.csproj`, `README.md` | 5 | Template packed once per RID |
| `src/Typstium.Runtime.All/Typstium.Runtime.All.csproj`, `README.md` | 5 | Meta package |
| `src/Typstium/build/Typstium.targets`, `src/Typstium/buildTransitive/Typstium.targets` | 6–9 | Selection, copy, manifest, `chmod`, WebDeploy |
| `src/Typstium/Typstium.csproj` | 6 | Packs the targets |
| `tests/Typstium.Runtime.Testing/` | 5 | `PackageFeed`, `SampleProject`, `DotNet`, `Scenario` constants |
| `tests/Typstium.Runtime.Tests/` | 5–9, 15 | Targets tests with fixture packages, catalog test |
| `tests/Typstium.Runtime.EndToEndTests/` | 14 | Real packages, real compiles |
| `build/Build.Cake.Extensions/Downloads/PinnedAssets.cs` | 10 | Lock model, read and write |
| `build/Build.Cake.Extensions/GitHub/GitHubReleaseDigests.cs` | 10 | Release JSON → name→SHA-256 |
| `build/Build.Cake.Extensions/Downloads/VerifiedDownloader.cs` | 11 | HTTPS download with a pinned SHA-256 |
| `build/Build.Cake.Extensions/Downloads/ArchiveFiles.cs` | 12 | Named files out of `.zip` and `.tar.xz` |
| `build/Build.Cake.Extensions.Tests/Downloads/*` | 10–12 | Tests and xz fixtures |
| `build/Build/TypstRuntimes.cs` | 13 | RIDs, Rust targets, asset names, URLs |
| `build/Build/Tasks/RuntimeTasks.cs` | 13 | The three tasks and the two test targets |
| `build/typst-runtimes.json` | 13 | The lock file |
| `.github/workflows/ci.yml` | 15 | `runtime-targets`, `pack`, `runtime-e2e` jobs |
| `docs/runtime-packages-checklist.md` | 15 | Manual checks |
| `Directory.Packages.props`, `Typstium.slnx` | 5, 12 | SharpCompress; new test projects |

---

### Task 1: `RuntimeIdentifierCandidates`

**Files:**
- Create: `src/Typstium/Executable/RuntimeIdentifierCandidates.cs`
- Test: `tests/Typstium.UnitTests/Executable/RuntimeIdentifierCandidatesTests.cs`

**Interfaces:**
- Produces: `internal static class RuntimeIdentifierCandidates { public static IReadOnlyList<string> For(string runtimeIdentifier); }`

- [ ] **Step 1: Write the failing test**

```csharp
using Typstium.Executable;

namespace Typstium.UnitTests.Executable;

public class RuntimeIdentifierCandidatesTests
{
    [Test]
    [Property("Scenario", "RS41")]
    [Arguments("win-arm64", new[] { "win-arm64", "win-x64" })]
    [Arguments("osx-arm64", new[] { "osx-arm64", "osx-x64" })]
    public async Task Arm_platforms_fall_back_to_the_x64_binary_their_emulation_runs(string native, string[] expected)
    {
        await Assert.That(RuntimeIdentifierCandidates.For(native)).IsEquivalentTo(expected, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    [Arguments("win-x64")]
    [Arguments("linux-arm64")]
    [Arguments("linux-x64")]
    [Arguments("linux-arm")]
    [Arguments("osx-x64")]
    [Arguments("win-x86")]
    public async Task Other_platforms_try_only_themselves(string native)
    {
        await Assert.That(RuntimeIdentifierCandidates.For(native)).IsEquivalentTo(new[] { native }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Typstium.UnitTests -- --treenode-filter "/*/*/RuntimeIdentifierCandidatesTests/*"`
Expected: build error, `RuntimeIdentifierCandidates` does not exist.

- [ ] **Step 3: Write the implementation**

```csharp
namespace Typstium.Executable;

/// <summary>
/// The bundled runtimes worth trying on a machine, best first: the native RID, then the x64 binary that Windows on Arm
/// and Rosetta run under emulation (spec runtime-packages §7, D11). Linux has no general x64 emulation.
/// </summary>
internal static class RuntimeIdentifierCandidates
{
    public static IReadOnlyList<string> For(string runtimeIdentifier) => runtimeIdentifier switch
    {
        "win-arm64" => ["win-arm64", "win-x64"],
        "osx-arm64" => ["osx-arm64", "osx-x64"],
        _ => [runtimeIdentifier],
    };
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Typstium.UnitTests -- --treenode-filter "/*/*/RuntimeIdentifierCandidatesTests/*"`
Expected: PASS (on Windows for both `net10.0` and `net472`).

- [ ] **Step 5: Commit**

```bash
git add src/Typstium/Executable/RuntimeIdentifierCandidates.cs tests/Typstium.UnitTests/Executable/RuntimeIdentifierCandidatesTests.cs
git commit -m "Try the emulated x64 runtime after the native one on Arm"
```

---

### Task 2: `RuntimeManifest.Parse`

**Files:**
- Create: `src/Typstium/Executable/RuntimeManifest.cs`
- Test: `tests/Typstium.UnitTests/Executable/RuntimeManifestTests.cs`

**Interfaces:**
- Produces:

```csharp
internal abstract record RuntimeManifestResult
{
    public sealed record Missing(string Path) : RuntimeManifestResult;
    public sealed record Invalid(string Path, string Reason) : RuntimeManifestResult;
    public sealed record Loaded(string Path, RuntimeManifest Manifest) : RuntimeManifestResult;
}

internal sealed class RuntimeManifest
{
    public const string FileName = "typstium.runtime.json";
    public IReadOnlyDictionary<string, string> Executables { get; }   // RID → full path inside the manifest's folder
    public static RuntimeManifestResult Parse(string path, string json, bool isWindows);
}
```

`Parse` never throws for malformed input. Each reason below is exact, because tests and §7 depend on the wording:

| Input | Reason |
|---|---|
| Not JSON | `not valid JSON: <JsonException.Message>` |
| Top level not an object | `the top level must be an object` |
| No `version` | `"version" is missing` |
| `version` not a number | `"version" must be a number` |
| `version` ≠ 1 | `unknown version <n>` |
| `runtimes` missing, not an object, or empty | `"runtimes" must be a non-empty object` |
| Empty RID key | `a runtime identifier is empty` |
| RID listed twice | `<rid> is listed twice` |
| Value not a string | `the path for <rid> must be a string` |
| Empty path | `the path for <rid> is empty` |
| Fully qualified, drive-relative (`C:x`) or root-relative (`\x`, `/x`) | `path '<p>' for <rid> is not relative` |
| A `..` segment (split on `/` and `\`) | `path '<p>' for <rid> contains '..'` |
| Resolves outside the folder | `path '<p>' for <rid> leaves the manifest's folder` |

- [ ] **Step 1: Write the failing tests**

```csharp
using Typstium.Executable;
using Typstium.UnitTests.Testing;

namespace Typstium.UnitTests.Executable;

public class RuntimeManifestTests
{
    private static readonly string Directory = Path.Combine(FakeLocatorEnvironment.Root, "app");

    private static readonly string ManifestPath = Path.Combine(Directory, RuntimeManifest.FileName);

    private static RuntimeManifestResult Parse(string json, bool isWindows = false) => RuntimeManifest.Parse(ManifestPath, json, isWindows);

    private static string Runtimes(string rid, string path) => $$"""{ "version": 1, "runtimes": { "{{rid}}": "{{path}}" } }""";

    [Test]
    [Property("Scenario", "RS31")]
    public async Task Paths_resolve_against_the_manifests_folder()
    {
        var result = Parse("""{ "version": 1, "runtimes": { "linux-x64": "tools/typst/typst", "win-x64": "typst/win-x64/typst.exe" } }""");

        var loaded = (RuntimeManifestResult.Loaded)result;
        await Assert.That(loaded.Path).IsEqualTo(ManifestPath);
        await Assert.That(loaded.Manifest.Executables["linux-x64"]).IsEqualTo(Path.Combine(Directory, "tools", "typst", "typst"));
        await Assert.That(loaded.Manifest.Executables["win-x64"]).IsEqualTo(Path.Combine(Directory, "typst", "win-x64", "typst.exe"));
    }

    [Test]
    public async Task A_byte_order_mark_and_crlf_line_endings_are_accepted()
    {
        var result = Parse("\uFEFF{\r\n  \"version\": 1,\r\n  \"runtimes\": { \"linux-x64\": \"typst/linux-x64/typst\" }\r\n}\r\n");

        await Assert.That(result is RuntimeManifestResult.Loaded).IsTrue();
    }

    [Test]
    [Arguments("not json", "not valid JSON: ")]
    [Arguments("[]", "the top level must be an object")]
    [Arguments("""{ "runtimes": { "linux-x64": "a" } }""", "\"version\" is missing")]
    [Arguments("""{ "version": "1", "runtimes": { "linux-x64": "a" } }""", "\"version\" must be a number")]
    [Arguments("""{ "version": 2, "runtimes": { "linux-x64": "a" } }""", "unknown version 2")]
    [Arguments("""{ "version": 1 }""", "\"runtimes\" must be a non-empty object")]
    [Arguments("""{ "version": 1, "runtimes": [] }""", "\"runtimes\" must be a non-empty object")]
    [Arguments("""{ "version": 1, "runtimes": {} }""", "\"runtimes\" must be a non-empty object")]
    [Arguments("""{ "version": 1, "runtimes": { "": "a" } }""", "a runtime identifier is empty")]
    [Arguments("""{ "version": 1, "runtimes": { "linux-x64": "a", "linux-x64": "b" } }""", "linux-x64 is listed twice")]
    [Arguments("""{ "version": 1, "runtimes": { "linux-x64": 1 } }""", "the path for linux-x64 must be a string")]
    [Arguments("""{ "version": 1, "runtimes": { "linux-x64": "" } }""", "the path for linux-x64 is empty")]
    public async Task Malformed_manifests_are_invalid_with_a_reason(string json, string reason)
    {
        var invalid = (RuntimeManifestResult.Invalid)Parse(json);

        await Assert.That(invalid.Path).IsEqualTo(ManifestPath);
        await Assert.That(invalid.Reason).StartsWith(reason);
    }

    [Test]
    [Arguments("/usr/bin/typst", false)]
    [Arguments(@"C:\\typst\\typst.exe", true)]
    [Arguments("C:typst.exe", true)]
    [Arguments(@"\\typst.exe", true)]
    [Arguments("/typst.exe", true)]
    [Arguments(@"\\\\server\\share\\typst.exe", true)]
    public async Task Paths_that_are_not_relative_are_rejected(string path, bool isWindows)
    {
        var invalid = (RuntimeManifestResult.Invalid)Parse(Runtimes("linux-x64", path), isWindows);
        var unescaped = path.Replace(@"\\", @"\");

        await Assert.That(invalid.Reason).IsEqualTo($"path '{unescaped}' for linux-x64 is not relative");
    }

    [Test]
    [Arguments("../typst", false)]
    [Arguments("tools/../../typst", false)]
    [Arguments(@"tools\\..\\typst", true)]
    [Arguments(@"tools\\..\\typst", false)]
    public async Task Parent_segments_are_rejected(string path, bool isWindows)
    {
        var invalid = (RuntimeManifestResult.Invalid)Parse(Runtimes("linux-x64", path), isWindows);

        await Assert.That(invalid.Reason).IsEqualTo($"path '{path.Replace(@"\\", @"\")}' for linux-x64 contains '..'");
    }
}
```

(The "leaves the manifest's folder" check is defense in depth: with relative paths and no `..`, nothing reaches it. It has
no test of its own, and the code comment says so.)

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Typstium.UnitTests -- --treenode-filter "/*/*/RuntimeManifestTests/*"`
Expected: build error, `RuntimeManifest` does not exist.

- [ ] **Step 3: Write the implementation**

```csharp
using System.Text.Json;
using Typstium.Utilities;

namespace Typstium.Executable;

/// <summary>What <see cref="ILocatorEnvironment.ReadRuntimeManifest"/> found in a folder.</summary>
internal abstract record RuntimeManifestResult
{
    private RuntimeManifestResult()
    {
    }

    /// <summary>There is no manifest at <paramref name="Path"/>.</summary>
    public sealed record Missing(string Path) : RuntimeManifestResult;

    /// <summary>The manifest at <paramref name="Path"/> can't be used; <paramref name="Reason"/> says why.</summary>
    public sealed record Invalid(string Path, string Reason) : RuntimeManifestResult;

    /// <summary>The manifest at <paramref name="Path"/>, validated.</summary>
    public sealed record Loaded(string Path, RuntimeManifest Manifest) : RuntimeManifestResult;
}

/// <summary>
/// typstium.runtime.json, written next to the app by Typstium's build targets: RID → executable, relative to the
/// manifest's folder (spec runtime-packages §6–§7). Every path is checked so a manifest can't point outside its folder.
/// </summary>
internal sealed class RuntimeManifest
{
    public const string FileName = "typstium.runtime.json";

    private RuntimeManifest(IReadOnlyDictionary<string, string> executables) => Executables = executables;

    /// <summary>RID → full executable path, every one inside the manifest's folder.</summary>
    public IReadOnlyDictionary<string, string> Executables { get; }

    /// <summary>Pure: no file system access. <paramref name="isWindows"/> selects the path rules for "relative".</summary>
    public static RuntimeManifestResult Parse(string path, string json, bool isWindows)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json.TrimStart('\uFEFF'));
        }
        catch (JsonException exception)
        {
            return new RuntimeManifestResult.Invalid(path, "not valid JSON: " + exception.Message);
        }

        using (document)
        {
            return Validate(path, document.RootElement, isWindows);
        }
    }

    private static RuntimeManifestResult Validate(string path, JsonElement root, bool isWindows)
    {
        RuntimeManifestResult Invalid(string reason) => new RuntimeManifestResult.Invalid(path, reason);

        if (root.ValueKind != JsonValueKind.Object)
        {
            return Invalid("the top level must be an object");
        }

        if (!root.TryGetProperty("version", out var version))
        {
            return Invalid("\"version\" is missing");
        }

        if (version.ValueKind != JsonValueKind.Number)
        {
            return Invalid("\"version\" must be a number");
        }

        if (!version.TryGetInt32(out var number) || number != 1)
        {
            return Invalid("unknown version " + version.GetRawText());
        }

        if (!root.TryGetProperty("runtimes", out var runtimes) || runtimes.ValueKind != JsonValueKind.Object || !runtimes.EnumerateObject().Any())
        {
            return Invalid("\"runtimes\" must be a non-empty object");
        }

        var directory = Path.GetFullPath(Path.GetDirectoryName(path)!);
        var prefix = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var executables = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var runtime in runtimes.EnumerateObject())
        {
            var rid = runtime.Name;
            if (rid.Length == 0)
            {
                return Invalid("a runtime identifier is empty");
            }

            if (executables.ContainsKey(rid))
            {
                return Invalid(rid + " is listed twice");
            }

            if (runtime.Value.ValueKind != JsonValueKind.String)
            {
                return Invalid($"the path for {rid} must be a string");
            }

            var entry = runtime.Value.GetString()!;
            if (entry.Length == 0)
            {
                return Invalid($"the path for {rid} is empty");
            }

            if (!IsRelative(entry, isWindows))
            {
                return Invalid($"path '{entry}' for {rid} is not relative");
            }

            if (entry.Split('/', '\\').Contains(".."))
            {
                return Invalid($"path '{entry}' for {rid} contains '..'");
            }

            var normalized = entry.Replace('/', Path.DirectorySeparatorChar);
            if (isWindows)
            {
                normalized = normalized.Replace('\\', Path.DirectorySeparatorChar);
            }

            // Defense in depth: relative paths without ".." can't leave the folder, so no test reaches this.
            var full = Path.GetFullPath(Path.Combine(directory, normalized));
            if (!full.StartsWith(prefix, isWindows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            {
                return Invalid($"path '{entry}' for {rid} leaves the manifest's folder");
            }

            executables.Add(rid, full);
        }

        return new RuntimeManifestResult.Loaded(path, new RuntimeManifest(executables));
    }

    // Not fully qualified, and not relative to a drive (C:x) or to the current drive's root (\x) either.
    private static bool IsRelative(string entry, bool isWindows)
    {
        if (FullyQualifiedPath.IsFullyQualified(entry, isWindows))
        {
            return false;
        }

        return isWindows
            ? entry[0] is not ('\\' or '/') && !(entry.Length >= 2 && entry[1] == ':')
            : entry[0] != '/';
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Typstium.UnitTests -- --treenode-filter "/*/*/RuntimeManifestTests/*"`
Expected: PASS on `net10.0` (and `net472` on Windows). On `net472`, `JsonDocument` comes from the System.Text.Json package
the test project already references.

- [ ] **Step 5: Commit**

```bash
git add src/Typstium/Executable/RuntimeManifest.cs tests/Typstium.UnitTests/Executable/RuntimeManifestTests.cs
git commit -m "Parse and validate typstium.runtime.json"
```

---

### Task 3: `ILocatorEnvironment.ReadRuntimeManifest`

**Files:**
- Modify: `src/Typstium/Executable/ILocatorEnvironment.cs`, `src/Typstium/Executable/SystemLocatorEnvironment.cs`
- Modify: `tests/Typstium.UnitTests/Testing/FakeLocatorEnvironment.cs`
- Test: `tests/Typstium.UnitTests/Executable/SystemLocatorEnvironmentTests.cs`

**Interfaces:**
- Consumes: `RuntimeManifest.Parse`, `RuntimeManifestResult` (Task 2).
- Produces:
  - `RuntimeManifestResult ILocatorEnvironment.ReadRuntimeManifest(string directory)`
  - `FakeLocatorEnvironment.Manifests`, a `Dictionary<string, string>` of directory → JSON, parsed through
    `RuntimeManifest.Parse` with the fake's `IsWindows`

- [ ] **Step 1: Write the failing tests** (append to `SystemLocatorEnvironmentTests`, which already has an `Environment`
  field for `SystemLocatorEnvironment.Instance` and uses `TemporaryDirectory`)

```csharp
    [Test]
    public async Task A_folder_without_a_manifest_reports_it_missing()
    {
        using var temp = new TemporaryDirectory("environment");

        var result = Environment.ReadRuntimeManifest(temp.Path);

        await Assert.That(result).IsEqualTo(new RuntimeManifestResult.Missing(Path.Combine(temp.Path, RuntimeManifest.FileName)));
    }

    [Test]
    public async Task A_manifest_file_is_read_and_parsed()
    {
        using var temp = new TemporaryDirectory("environment");
        temp.CreateFile(RuntimeManifest.FileName, """{ "version": 1, "runtimes": { "linux-x64": "tools/typst" } }""");

        var loaded = (RuntimeManifestResult.Loaded)Environment.ReadRuntimeManifest(temp.Path);

        await Assert.That(loaded.Manifest.Executables["linux-x64"]).IsEqualTo(temp.Combine("tools", "typst"));
    }

    [Test]
    public async Task An_unreadable_manifest_is_invalid()
    {
        using var temp = new TemporaryDirectory("environment");
        // A folder where the file should be: reading it throws UnauthorizedAccessException (Windows) or IOException (Unix).
        temp.CreateDirectory(RuntimeManifest.FileName);

        var invalid = (RuntimeManifestResult.Invalid)Environment.ReadRuntimeManifest(temp.Path);

        await Assert.That(invalid.Reason).StartsWith("unreadable: ");
    }

    [Test]
    public async Task A_missing_folder_reports_the_manifest_missing()
    {
        using var temp = new TemporaryDirectory("environment");

        var result = Environment.ReadRuntimeManifest(temp.Combine("nope"));

        await Assert.That(result is RuntimeManifestResult.Missing).IsTrue();
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Typstium.UnitTests -- --treenode-filter "/*/*/SystemLocatorEnvironmentTests/*"`
Expected: build error, `ReadRuntimeManifest` is not defined.

- [ ] **Step 3: Write the implementation**

In `ILocatorEnvironment`:

```csharp
    /// <summary>Reads typstium.runtime.json in <paramref name="directory"/>; never throws for I/O or format errors.</summary>
    RuntimeManifestResult ReadRuntimeManifest(string directory);
```

In `SystemLocatorEnvironment`:

```csharp
    public RuntimeManifestResult ReadRuntimeManifest(string directory)
    {
        var path = Path.Combine(directory, RuntimeManifest.FileName);
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return new RuntimeManifestResult.Missing(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new RuntimeManifestResult.Invalid(path, "unreadable: " + exception.Message);
        }

        return RuntimeManifest.Parse(path, json, IsWindows);
    }
```

In `FakeLocatorEnvironment`:

```csharp
    /// <summary>Directory → manifest JSON. Parsed through the real parser, so a test can't use a manifest it would reject.</summary>
    public Dictionary<string, string> Manifests { get; } = new(StringComparer.OrdinalIgnoreCase);

    public RuntimeManifestResult ReadRuntimeManifest(string directory)
    {
        var path = Path.Combine(directory, RuntimeManifest.FileName);
        return Manifests.TryGetValue(directory, out var json) ? RuntimeManifest.Parse(path, json, IsWindows) : new RuntimeManifestResult.Missing(path);
    }
```

- [ ] **Step 4: Run all unit tests to verify they pass**

Run: `dotnet test tests/Typstium.UnitTests`
Expected: PASS. Existing locator tests are unaffected, because `Manifests` is empty and returns `Missing`.

- [ ] **Step 5: Commit**

```bash
git add src/Typstium/Executable tests/Typstium.UnitTests
git commit -m "Read the runtime manifest through the locator environment"
```

---

### Task 4: `BundledRuntimeLocator` with the manifest and fallbacks

**Files:**
- Modify: `src/Typstium/Executable/Locators.cs` (the `BundledRuntimeLocator` class only)
- Create: `tests/Typstium.UnitTests/Executable/BundledRuntimeLocatorTests.cs`
- Modify: `tests/Typstium.UnitTests/Executable/LocatorTests.cs` (move the existing bundled test into the new file)
- Modify: `docs/specs/foundation.md` (the "Bundled runtime" row in the locator table)

**Interfaces:**
- Consumes: `RuntimeIdentifierCandidates.For` (Task 1), `ReadRuntimeManifest` (Task 3).
- Produces: behavior only. `Name` stays `"BundledRuntime"`.

- [ ] **Step 1: Write the failing tests**

```csharp
using Typstium.Executable;
using Typstium.UnitTests.Testing;

namespace Typstium.UnitTests.Executable;

public class BundledRuntimeLocatorTests
{
    private static Task<TypstLocatorResult> Locate(FakeLocatorEnvironment environment) =>
        new BundledRuntimeLocator(environment).LocateAsync(CancellationToken.None).AsTask();

    private static string[] Searched(TypstLocatorResult result) => ((TypstLocatorResult.NotFoundResult)result).SearchedLocations.ToArray();

    [Test]
    public async Task The_classic_asp_net_bin_folder_is_searched_and_the_execute_bit_set()
    {
        var environment = new FakeLocatorEnvironment { IsWindows = false, RuntimeIdentifier = "linux-x64" };
        var typst = environment.AddFile(environment.BaseDirectory, "bin", "typst", "linux-x64", TypstExecutableFileNames.Unix);

        await Assert.That(await Locate(environment)).IsEqualTo(TypstLocatorResult.Found(typst));
        await Assert.That(environment.MadeExecutable).Contains(typst);
    }

    [Test]
    [Property("Scenario", "RS31")]
    public async Task A_manifest_points_at_a_custom_folder()
    {
        var environment = new FakeLocatorEnvironment { IsWindows = false, RuntimeIdentifier = "linux-x64" };
        environment.Manifests[environment.BaseDirectory] = """{ "version": 1, "runtimes": { "linux-x64": "tools/typst" } }""";
        var typst = environment.AddFile(environment.BaseDirectory, "tools", "typst");

        await Assert.That(await Locate(environment)).IsEqualTo(TypstLocatorResult.Found(typst));
        await Assert.That(environment.MadeExecutable).Contains(typst);
    }

    [Test]
    public async Task A_manifest_is_authoritative_over_conventional_folders()
    {
        var environment = new FakeLocatorEnvironment { IsWindows = false, RuntimeIdentifier = "linux-x64" };
        environment.Manifests[environment.BaseDirectory] = """{ "version": 1, "runtimes": { "linux-x64": "tools/typst" } }""";
        environment.AddFile(environment.BaseDirectory, "typst", "linux-x64", TypstExecutableFileNames.Unix);   // stale deploy

        var result = await Locate(environment);

        await Assert.That(Searched(result)).IsEquivalentTo(
            new[] { Path.Combine(environment.BaseDirectory, RuntimeManifest.FileName), Path.Combine(environment.BaseDirectory, "tools", "typst") },
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task A_manifest_in_bin_is_used_when_the_app_folder_has_none()
    {
        var environment = new FakeLocatorEnvironment { IsWindows = true, RuntimeIdentifier = "win-x64" };
        var bin = Path.Combine(environment.BaseDirectory, "bin");
        environment.Manifests[bin] = """{ "version": 1, "runtimes": { "win-x64": "typst/win-x64/typst.exe" } }""";
        var typst = environment.AddFile(bin, "typst", "win-x64", TypstExecutableFileNames.Windows);

        await Assert.That(await Locate(environment)).IsEqualTo(TypstLocatorResult.Found(typst));
    }

    [Test]
    public async Task An_invalid_manifest_is_reported_and_nothing_else_is_searched()
    {
        var environment = new FakeLocatorEnvironment { IsWindows = false, RuntimeIdentifier = "linux-x64" };
        environment.Manifests[environment.BaseDirectory] = """{ "version": 1, "runtimes": { "linux-x64": "/usr/bin/typst" } }""";
        environment.AddFile(environment.BaseDirectory, "typst", "linux-x64", TypstExecutableFileNames.Unix);

        var searched = Searched(await Locate(environment));

        await Assert.That(searched.Single()).IsEqualTo(
            $"{Path.Combine(environment.BaseDirectory, RuntimeManifest.FileName)} (path '/usr/bin/typst' for linux-x64 is not relative)");
    }

    [Test]
    [Property("Scenario", "RS41")]
    public async Task Windows_on_arm_falls_back_to_the_x64_runtime_in_the_manifest()
    {
        var environment = new FakeLocatorEnvironment { IsWindows = true, RuntimeIdentifier = "win-arm64" };
        environment.Manifests[environment.BaseDirectory] = """{ "version": 1, "runtimes": { "win-x64": "typst/win-x64/typst.exe" } }""";
        var typst = environment.AddFile(environment.BaseDirectory, "typst", "win-x64", TypstExecutableFileNames.Windows);

        await Assert.That(await Locate(environment)).IsEqualTo(TypstLocatorResult.Found(typst));
    }

    [Test]
    [Property("Scenario", "RS41")]
    public async Task Apple_silicon_prefers_the_native_runtime_and_falls_back_to_x64_in_conventional_folders()
    {
        var environment = new FakeLocatorEnvironment { IsWindows = false, RuntimeIdentifier = "osx-arm64" };
        var x64 = environment.AddFile(environment.BaseDirectory, "typst", "osx-x64", TypstExecutableFileNames.Unix);
        await Assert.That(await Locate(environment)).IsEqualTo(TypstLocatorResult.Found(x64));

        var arm64 = environment.AddFile(environment.BaseDirectory, "bin", "typst", "osx-arm64", TypstExecutableFileNames.Unix);
        await Assert.That(await Locate(environment)).IsEqualTo(TypstLocatorResult.Found(arm64));
    }

    [Test]
    public async Task Nothing_found_lists_every_folder_for_every_candidate()
    {
        var environment = new FakeLocatorEnvironment { IsWindows = false, RuntimeIdentifier = "osx-arm64" };
        var root = environment.BaseDirectory;

        await Assert.That(Searched(await Locate(environment))).IsEquivalentTo(
            new[]
            {
                Path.Combine(root, "typst", "osx-arm64", "typst"), Path.Combine(root, "bin", "typst", "osx-arm64", "typst"),
                Path.Combine(root, "typst", "osx-x64", "typst"), Path.Combine(root, "bin", "typst", "osx-x64", "typst"),
            },
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }
}
```

Delete `Bundled_runtime_supports_the_classic_asp_net_bin_folder_and_sets_the_execute_bit` from `LocatorTests.cs` (it moved).

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Typstium.UnitTests -- --treenode-filter "/*/*/BundledRuntimeLocatorTests/*"`
Expected: FAIL. The manifest tests fail (no manifest support yet), and the fallback tests fail (no fallback yet).

- [ ] **Step 3: Write the implementation** (replace the `BundledRuntimeLocator` class in `Locators.cs`)

```csharp
/// <summary>
/// The binaries Typstium's build targets copy next to the app (spec runtime-packages §7): typstium.runtime.json in
/// the app folder or bin/ when there is one (authoritative), otherwise typst/&lt;rid&gt;/ and bin/typst/&lt;rid&gt;/,
/// for the native RID and then its emulated fallback.
/// </summary>
internal sealed class BundledRuntimeLocator(ILocatorEnvironment environment) : ITypstExecutableLocator
{
    public string Name => "BundledRuntime";

    public ValueTask<TypstLocatorResult> LocateAsync(CancellationToken cancellationToken)
    {
        var rids = RuntimeIdentifierCandidates.For(environment.RuntimeIdentifier);
        foreach (var directory in new[] { environment.BaseDirectory, Path.Combine(environment.BaseDirectory, "bin") })
        {
            switch (environment.ReadRuntimeManifest(directory))
            {
                case RuntimeManifestResult.Invalid invalid:
                    return new(TypstLocatorResult.NotFound(new[] { $"{invalid.Path} ({invalid.Reason})" }));

                case RuntimeManifestResult.Loaded loaded:
                    return new(FromManifest(loaded, rids));
            }
        }

        return new(FromConventionalFolders(rids));
    }

    private TypstLocatorResult FromManifest(RuntimeManifestResult.Loaded loaded, IReadOnlyList<string> rids)
    {
        var searched = new List<string> { loaded.Path };
        foreach (var rid in rids)
        {
            if (loaded.Manifest.Executables.TryGetValue(rid, out var executable))
            {
                searched.Add(executable);
                if (environment.FileExists(executable))
                {
                    environment.EnsureExecutable(executable);
                    return TypstLocatorResult.Found(executable);
                }
            }
        }

        return TypstLocatorResult.NotFound(searched);
    }

    private TypstLocatorResult FromConventionalFolders(IReadOnlyList<string> rids)
    {
        var executable = environment.ExecutableFileName;
        var candidates = rids
            .SelectMany(rid => new[]
            {
                Path.Combine(environment.BaseDirectory, "typst", rid, executable),
                Path.Combine(environment.BaseDirectory, "bin", "typst", rid, executable),
            })
            .ToArray();

        foreach (var candidate in candidates)
        {
            if (environment.FileExists(candidate))
            {
                environment.EnsureExecutable(candidate);
                return TypstLocatorResult.Found(candidate);
            }
        }

        return TypstLocatorResult.NotFound(candidates);
    }
}
```

- [ ] **Step 4: Update the foundation spec's locator table row**

Replace the "Bundled runtime" row's text in `docs/specs/foundation.md` with:
`typstium.runtime.json in AppContext.BaseDirectory or its bin/ (authoritative when present), otherwise typst/<rid>/ and bin/typst/<rid>/ for the native RID and its emulated fallback; see [runtime packages §7](runtime-packages.md#7-locating-the-binary-at-runtime). On Unix it makes sure the execute bit is set`.

- [ ] **Step 5: Run all unit tests**

Run: `dotnet test tests/Typstium.UnitTests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Typstium/Executable/Locators.cs tests/Typstium.UnitTests docs/specs/foundation.md
git commit -m "Find bundled runtimes through the manifest, with emulation fallbacks"
```

---

### Task 5: Runtime package projects and the fixture feed

**Files:**
- Create: `src/Typstium.Runtime/Typstium.Runtime.csproj`, `src/Typstium.Runtime/README.md`
- Create: `src/Typstium.Runtime.All/Typstium.Runtime.All.csproj`, `src/Typstium.Runtime.All/README.md`
- Create: `tests/Typstium.Runtime.Testing/Typstium.Runtime.Testing.csproj` with `DotNet.cs`, `PackageFeed.cs`, `Scenario.cs`
- Create: `tests/Typstium.Runtime.Tests/Typstium.Runtime.Tests.csproj`, `AssemblyInfo.cs`, `PackageLayoutTests.cs`
- Modify: `Typstium.slnx` (add both test projects under `/tests/`; the two `src/Typstium.Runtime*` projects are **not** added,
  because they only pack with properties)
- Modify: `docs/specs/runtime-packages.md` §10 (refinement 3)

**Interfaces:**
- Produces:
  - `DotNet.RunAsync(string workingDirectory, IReadOnlyDictionary<string, string?> environment, params string[] arguments)` →
    `Task<DotNetRun>`, with `record DotNetRun(int ExitCode, string Output)` and
    `IReadOnlyList<string> DiagnosticCodes` (every `TYPSTIUM\d{4}` in the output, distinct, in order)
  - `PackageFeed.GetFixtureAsync()` → `Task<Feed>`, with
    `record Feed(string Directory, string Version, string NuGetPackages, IReadOnlyList<string> RuntimeIdentifiers)`
  - `Scenario` constants (`public const string Key = "Scenario";`)
  - Template project properties: `TypstiumRuntimeIdentifier`, `TypstiumRuntimeSource` (a folder holding `typst[.exe]`
    and `LICENSE`), `TypstVersion`

- [ ] **Step 1: Write the template project**

`src/Typstium.Runtime/Typstium.Runtime.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <!-- Packed once per RID by PackTypstRuntimes and by the runtime tests (docs/specs/runtime-packages.md §3, §8): no
       code, only Typst's binary, its license and a props file that declares the binary to Typstium's targets.
       Required properties: TypstiumRuntimeIdentifier, TypstiumRuntimeSource (folder with typst[.exe] and LICENSE),
       TypstVersion. -->
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <IncludeSymbols>false</IncludeSymbols>
    <EnableDefaultItems>false</EnableDefaultItems>
    <SuppressDependenciesWhenPacking>true</SuppressDependenciesWhenPacking>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
    <!-- NU5128: no lib/ folder for netstandard2.0. Intended: the package has no assemblies at all. -->
    <NoWarn>$(NoWarn);NU5128</NoWarn>
    <PackageId>Typstium.Runtime.$(TypstiumRuntimeIdentifier)</PackageId>
    <Version>$(TypstVersion)</Version>
    <Description>The official Typst CLI $(TypstVersion) for $(TypstiumRuntimeIdentifier), repackaged unchanged for Typstium.</Description>
    <PackageLicenseExpression>Apache-2.0</PackageLicenseExpression>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PackageTags>$(PackageTags);typst-cli;runtime</PackageTags>
    <TypstiumRuntimeExecutable>typst</TypstiumRuntimeExecutable>
    <TypstiumRuntimeExecutable Condition="$(TypstiumRuntimeIdentifier.StartsWith('win-'))">typst.exe</TypstiumRuntimeExecutable>
    <_TypstiumPropsFile>$(IntermediateOutputPath)Typstium.Runtime.$(TypstiumRuntimeIdentifier).props</_TypstiumPropsFile>
    <TargetsForTfmSpecificContentInPackage>$(TargetsForTfmSpecificContentInPackage);_TypstiumAddRuntimeProps</TargetsForTfmSpecificContentInPackage>
    <!-- %24 is an escaped $, so MSBuild writes these references into the props instead of expanding them here. -->
    <_TypstiumPropsContent><![CDATA[<Project>
  <!-- Declares the binary in this package to Typstium's build targets (docs/specs/runtime-packages.md §3). -->
  <ItemGroup>
    <TypstiumRuntime Include="{rid}"
                     Executable="%24([MSBuild]::NormalizePath('%24(MSBuildThisFileDirectory)', '..', 'tools', 'typst', '{rid}', '{exe}'))"
                     TypstVersion="{version}"
                     ContractVersion="1" />
  </ItemGroup>
</Project>]]></_TypstiumPropsContent>
  </PropertyGroup>

  <ItemGroup>
    <None Include="README.md" Pack="true" PackagePath="/" />
    <None Include="$(TypstiumRuntimeSource)/$(TypstiumRuntimeExecutable)" Pack="true" PackagePath="tools/typst/$(TypstiumRuntimeIdentifier)/" />
    <None Include="$(TypstiumRuntimeSource)/LICENSE" Pack="true" PackagePath="tools/typst/$(TypstiumRuntimeIdentifier)/" />
  </ItemGroup>

  <Target Name="_TypstiumValidateRuntimeProperties" BeforeTargets="Build;Pack">
    <Error Condition="'$(TypstiumRuntimeIdentifier)' == '' or '$(TypstiumRuntimeSource)' == '' or '$(TypstVersion)' == ''"
           Text="Pack with -p:TypstiumRuntimeIdentifier=&lt;rid&gt; -p:TypstiumRuntimeSource=&lt;folder&gt; -p:TypstVersion=&lt;version&gt;." />
    <Error Condition="!Exists('$(TypstiumRuntimeSource)/$(TypstiumRuntimeExecutable)')" Text="$(TypstiumRuntimeSource)/$(TypstiumRuntimeExecutable) doesn't exist." />
  </Target>

  <Target Name="_TypstiumAddRuntimeProps">
    <WriteLinesToFile File="$(_TypstiumPropsFile)"
                      Lines="$(_TypstiumPropsContent.Replace('{rid}', '$(TypstiumRuntimeIdentifier)').Replace('{exe}', '$(TypstiumRuntimeExecutable)').Replace('{version}', '$(TypstVersion)'))"
                      Overwrite="true" WriteOnlyWhenDifferent="true" />
    <ItemGroup>
      <TfmSpecificPackageFile Include="$(_TypstiumPropsFile)" PackagePath="build/;buildTransitive/" />
    </ItemGroup>
  </Target>

</Project>
```

`src/Typstium.Runtime/README.md`:

```markdown
# Typstium.Runtime.<rid>

The official [Typst](https://typst.app) command-line compiler for one platform, repackaged unchanged from
[Typst's GitHub releases](https://github.com/typst/typst/releases) for [Typstium](https://github.com/mgnslndh/Typstium).
The package version is the Typst version it contains. Typst is licensed under Apache-2.0; its license is included next
to the binary.

Reference it together with the `Typstium` package. Typstium's build targets copy the binary next to your app, and
Typstium finds it there at runtime. Use `Typstium.Runtime.All` to reference every platform at once.
```

`src/Typstium.Runtime.All/Typstium.Runtime.All.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <!-- No content: depends on every Typstium.Runtime.<rid> at exactly its own version (docs/specs/runtime-packages.md §3,
       D2). Packed after them, restoring from the folder they were packed to (RestoreAdditionalProjectSources). -->
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <IncludeSymbols>false</IncludeSymbols>
    <EnableDefaultItems>false</EnableDefaultItems>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
    <!-- NU5128: no lib/ folder for netstandard2.0. Intended: a meta package with dependencies only. -->
    <NoWarn>$(NoWarn);NU5128</NoWarn>
    <!-- The RID packages' versions follow Typst, not the repository, so they can't live in Directory.Packages.props. -->
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
    <PackageId>Typstium.Runtime.All</PackageId>
    <Version>$(TypstVersion)</Version>
    <Description>The official Typst CLI $(TypstVersion) for every platform Typst ships, through the Typstium.Runtime.&lt;rid&gt; packages.</Description>
    <PackageLicenseExpression>Apache-2.0</PackageLicenseExpression>
    <PackageReadmeFile>README.md</PackageReadmeFile>
  </PropertyGroup>

  <ItemGroup>
    <None Include="README.md" Pack="true" PackagePath="/" />
    <!-- PrivateAssets="none": the RID packages' build assets must reach whoever references this package. -->
    <PackageReference Include="Typstium.Runtime.win-x64" Version="[$(TypstVersion)]" PrivateAssets="none" />
    <PackageReference Include="Typstium.Runtime.win-arm64" Version="[$(TypstVersion)]" PrivateAssets="none" />
    <PackageReference Include="Typstium.Runtime.osx-x64" Version="[$(TypstVersion)]" PrivateAssets="none" />
    <PackageReference Include="Typstium.Runtime.osx-arm64" Version="[$(TypstVersion)]" PrivateAssets="none" />
    <PackageReference Include="Typstium.Runtime.linux-x64" Version="[$(TypstVersion)]" PrivateAssets="none" />
    <PackageReference Include="Typstium.Runtime.linux-arm64" Version="[$(TypstVersion)]" PrivateAssets="none" />
    <PackageReference Include="Typstium.Runtime.linux-arm" Version="[$(TypstVersion)]" PrivateAssets="none" />
  </ItemGroup>

</Project>
```

`src/Typstium.Runtime.All/README.md`: the same text as the RID README, starting with
"Every `Typstium.Runtime.<rid>` package at the same Typst version: about 110 MB to restore. Prefer referencing only the
platforms you deploy to."

- [ ] **Step 2: Write the test helpers** (`tests/Typstium.Runtime.Testing`, a `net10.0` library)

`Typstium.Runtime.Testing.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="TUnit.Core" />
    <ProjectReference Include="..\Typstium.Testing\Typstium.Testing.csproj" />
  </ItemGroup>

</Project>
```

`DotNet.cs`:

```csharp
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Typstium.Runtime.Testing;

/// <summary>The output of one dotnet (or msbuild.exe) run.</summary>
public sealed partial record DotNetRun(int ExitCode, string Output)
{
    /// <summary>Every TYPSTIUM diagnostic code in the output, distinct, in order of appearance.</summary>
    public IReadOnlyList<string> DiagnosticCodes =>
        CodePattern().Matches(Output).Select(match => match.Value).Distinct(StringComparer.Ordinal).ToArray();

    [GeneratedRegex(@"TYPSTIUM\d{4}")]
    private static partial Regex CodePattern();
}

/// <summary>Runs dotnet with node reuse off (so temp folders can be deleted) and with the given environment variables.</summary>
public static class DotNet
{
    public static Task<DotNetRun> RunAsync(string workingDirectory, IReadOnlyDictionary<string, string?> environment, params string[] arguments) =>
        RunToolAsync("dotnet", workingDirectory, environment, arguments);

    public static async Task<DotNetRun> RunToolAsync(string tool, string workingDirectory, IReadOnlyDictionary<string, string?> environment, params string[] arguments)
    {
        var start = new ProcessStartInfo(tool)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        foreach (var (name, value) in environment)
        {
            start.Environment[name] = value;
        }

        using var process = Process.Start(start)!;
        var output = new StringBuilder();
        process.OutputDataReceived += (_, line) => { if (line.Data is not null) { lock (output) { output.AppendLine(line.Data); } } };
        process.ErrorDataReceived += (_, line) => { if (line.Data is not null) { lock (output) { output.AppendLine(line.Data); } } };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        await process.WaitForExitAsync(timeout.Token);
        return new DotNetRun(process.ExitCode, output.ToString());
    }
}
```

`Scenario.cs`:

```csharp
namespace Typstium.Runtime.Testing;

/// <summary>The property key that links a test to a scenario in docs/specs/runtime-packages.md §9.</summary>
public static class Scenario
{
    public const string Key = "Scenario";
}
```

`PackageFeed.cs` packs the core package and the fixture runtime packages once per test session:

```csharp
using System.Globalization;
using Typstium.Testing;

namespace Typstium.Runtime.Testing;

/// <summary>A local package feed and the isolated NuGet cache the generated projects restore with.</summary>
public sealed record Feed(string Directory, string Version, string NuGetPackages, IReadOnlyList<string> RuntimeIdentifiers)
{
    public IReadOnlyDictionary<string, string?> Environment => new Dictionary<string, string?> { ["NUGET_PACKAGES"] = NuGetPackages };
}

/// <summary>
/// Packs Typstium and fixture Typstium.Runtime.&lt;rid&gt; packages, with a text stub instead of Typst, into
/// artifacts/runtime-tests/feed-&lt;version&gt;/, once per test session. The version is unique per session, so the
/// persistent, isolated cache (artifacts/runtime-tests/nuget-packages) never serves a stale fixture.
/// </summary>
public static class PackageFeed
{
    public static readonly string[] AllRuntimeIdentifiers = ["win-x64", "win-arm64", "osx-x64", "osx-arm64", "linux-x64", "linux-arm64", "linux-arm"];

    private static readonly Lazy<Task<Feed>> Fixture = new(CreateFixtureAsync);

    public static Task<Feed> GetFixtureAsync() => Fixture.Value;

    public static string ArtifactsRoot => Path.Combine(RepositoryPaths.Root, "artifacts", "runtime-tests");

    /// <summary>The executable's file name in a runtime package for <paramref name="rid"/>.</summary>
    public static string ExecutableName(string rid) => rid.StartsWith("win-", StringComparison.Ordinal) ? "typst.exe" : "typst";

    private static async Task<Feed> CreateFixtureAsync()
    {
        var version = "0.0.0-t" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
        var directory = Path.Combine(ArtifactsRoot, "feed-" + version);
        var cache = Path.Combine(ArtifactsRoot, "nuget-packages");
        Directory.CreateDirectory(directory);
        var environment = new Dictionary<string, string?> { ["NUGET_PACKAGES"] = cache };

        await PackAsync(environment, Path.Combine("src", "Typstium", "Typstium.csproj"), "-o", directory, "-p:Version=" + version);

        foreach (var rid in AllRuntimeIdentifiers)
        {
            var source = Path.Combine(directory, "sources", rid);
            Directory.CreateDirectory(source);
            await File.WriteAllTextAsync(Path.Combine(source, ExecutableName(rid)), $"stub typst for {rid}\n");
            await File.WriteAllTextAsync(Path.Combine(source, "LICENSE"), "stub license\n");
            await PackAsync(environment, Path.Combine("src", "Typstium.Runtime", "Typstium.Runtime.csproj"),
                "-o", directory, "-p:TypstiumRuntimeIdentifier=" + rid, "-p:TypstiumRuntimeSource=" + source, "-p:TypstVersion=" + version);
        }

        await PackAsync(environment, Path.Combine("src", "Typstium.Runtime.All", "Typstium.Runtime.All.csproj"),
            "-o", directory, "-p:TypstVersion=" + version, "-p:RestoreAdditionalProjectSources=" + directory);

        return new Feed(directory, version, cache, AllRuntimeIdentifiers);
    }

    private static async Task PackAsync(IReadOnlyDictionary<string, string?> environment, string project, params string[] arguments)
    {
        var run = await DotNet.RunAsync(RepositoryPaths.Root, environment, ["pack", project, "-c", "Release", "-nodeReuse:false", .. arguments]);
        if (run.ExitCode != 0)
        {
            throw new InvalidOperationException($"dotnet pack {project} failed:\n{run.Output}");
        }
    }
}
```

- [ ] **Step 3: Write the failing package layout tests** (`tests/Typstium.Runtime.Tests`)

`Typstium.Runtime.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="TUnit" />
    <ProjectReference Include="..\Typstium.Runtime.Testing\Typstium.Runtime.Testing.csproj" />
  </ItemGroup>

</Project>
```

`AssemblyInfo.cs` keeps concurrent `dotnet` runs bounded:

```csharp
using TUnit.Core.Interfaces;

[assembly: ParallelLimiter<Typstium.Runtime.Tests.FourAtATime>]

namespace Typstium.Runtime.Tests;

// Each test runs dotnet build or publish; more than a few at once only thrashes the machine.
public sealed class FourAtATime : IParallelLimit
{
    public int Limit => 4;
}
```

`PackageLayoutTests.cs`:

```csharp
using System.IO.Compression;
using Typstium.Runtime.Testing;

namespace Typstium.Runtime.Tests;

public class PackageLayoutTests
{
    private static async Task<(string[] Entries, string Nuspec, ZipArchive Archive)> OpenAsync(string id)
    {
        var feed = await PackageFeed.GetFixtureAsync();
        var archive = ZipFile.OpenRead(Path.Combine(feed.Directory, $"{id}.{feed.Version}.nupkg"));
        var nuspec = archive.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
        using var reader = new StreamReader(nuspec.Open());
        var entries = archive.Entries.Select(entry => entry.FullName)
            .Where(name => !name.StartsWith("_rels/", StringComparison.Ordinal) && !name.StartsWith("package/", StringComparison.Ordinal) && name != "[Content_Types].xml" && !name.EndsWith(".nuspec", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal).ToArray();
        return (entries, await reader.ReadToEndAsync(), archive);
    }

    [Test]
    [Arguments("linux-x64", "typst")]
    [Arguments("win-arm64", "typst.exe")]
    public async Task A_runtime_package_holds_the_binary_its_license_and_the_props(string rid, string executable)
    {
        var (entries, nuspec, archive) = await OpenAsync("Typstium.Runtime." + rid);
        using (archive)
        {
            await Assert.That(entries).IsEquivalentTo(new[]
            {
                $"README.md",
                $"build/Typstium.Runtime.{rid}.props",
                $"buildTransitive/Typstium.Runtime.{rid}.props",
                $"tools/typst/{rid}/LICENSE",
                $"tools/typst/{rid}/{executable}",
            }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(nuspec).Contains("<license type=\"expression\">Apache-2.0</license>");
            await Assert.That(nuspec).DoesNotContain("<dependency ");

            using var props = new StreamReader(archive.GetEntry($"buildTransitive/Typstium.Runtime.{rid}.props")!.Open());
            var text = await props.ReadToEndAsync();
            await Assert.That(text).Contains($"<TypstiumRuntime Include=\"{rid}\"");
            await Assert.That(text).Contains($"$([MSBuild]::NormalizePath('$(MSBuildThisFileDirectory)', '..', 'tools', 'typst', '{rid}', '{executable}'))");
            await Assert.That(text).Contains("ContractVersion=\"1\"");
        }
    }

    [Test]
    public async Task The_meta_package_depends_on_every_runtime_at_exactly_its_version()
    {
        var feed = await PackageFeed.GetFixtureAsync();
        var (entries, nuspec, archive) = await OpenAsync("Typstium.Runtime.All");
        using (archive)
        {
            await Assert.That(entries).IsEquivalentTo(new[] { "README.md" });
            foreach (var rid in PackageFeed.AllRuntimeIdentifiers)
            {
                await Assert.That(nuspec).Contains($"<dependency id=\"Typstium.Runtime.{rid}\" version=\"[{feed.Version}]\" />");
            }
        }
    }
}
```

- [ ] **Step 4: Run the tests and make them pass**

Run: `dotnet test tests/Typstium.Runtime.Tests -- --treenode-filter "/*/*/PackageLayoutTests/*"`
Expected at first: the core pack succeeds, because no targets exist yet. Fix the template until both tests pass. Three
likely adjustments:
- If `TfmSpecificPackageFile` with a non-`lib/` `PackagePath` is rejected, add the props as
  `<None Include="$(_TypstiumPropsFile)" Pack="true" PackagePath="build/;buildTransitive/" />` from a target with
  `BeforeTargets="_GetPackageFiles"`.
- If the `%24` escape is written literally, use `$([MSBuild]::Escape(...))` on the property content.
- If `PrivateAssets="none"` still produces an `exclude` attribute, change the assertion's expected string to what
  NuGet writes, as long as it has no `exclude="Build`.

Record the fix in a comment in the project file.

- [ ] **Step 5: Write refinement 3 into the spec**

In §10 "Targets tests", replace "an isolated `NUGET_PACKAGES` folder" with: "a persistent `NUGET_PACKAGES` folder isolated
from the user's (`artifacts/runtime-tests/nuget-packages`). The packages under test get a unique version per session
(`0.0.0-t<UTC timestamp>`), so a stale fixture can't be served. End-to-end tests use a fresh cache for each session."

- [ ] **Step 6: Commit**

```bash
git add src/Typstium.Runtime src/Typstium.Runtime.All tests/Typstium.Runtime.Testing tests/Typstium.Runtime.Tests Typstium.slnx docs/specs/runtime-packages.md
git commit -m "Pack runtime packages from a template, with fixture packages for tests"
```

---

### Task 6: Core targets: copying every referenced runtime and writing the manifest

**Files:**
- Create: `src/Typstium/buildTransitive/Typstium.targets`, `src/Typstium/build/Typstium.targets`
- Modify: `src/Typstium/Typstium.csproj` (pack them)
- Create: `tests/Typstium.Runtime.Testing/SampleProject.cs`
- Create: `tests/Typstium.Runtime.Tests/CopyTests.cs`
- Modify: `docs/specs/runtime-packages.md` §6 (refinement 1)

**Interfaces:**
- Consumes: `PackageFeed`, `DotNet` (Task 5).
- Produces:
  - `SampleProject` (below)
  - the private item `_TypstiumSelected`, with metadata `TypstiumTargetPath`, and the property `_TypstiumManifestJson`;
    Tasks 7–9 extend the same targets file

- [ ] **Step 1: Write `SampleProject`**

```csharp
using System.Text;
using Typstium.Testing.Fixtures;

namespace Typstium.Runtime.Testing;

/// <summary>
/// A generated SDK-style project in a temp folder (outside the repository, so its Directory.Build.props don't apply)
/// that references the feed's Typstium and runtime packages.
/// </summary>
public sealed class SampleProject : IDisposable
{
    private readonly TemporaryDirectory _temp;
    private readonly Feed _feed;

    private SampleProject(Feed feed, string folder)
    {
        _feed = feed;
        _temp = new TemporaryDirectory("runtime");
        Directory = _temp.CreateDirectory(folder);
    }

    /// <summary>The project's folder.</summary>
    public string Directory { get; }

    public string Name { get; private init; } = "App";

    public string Sdk { get; set; } = "Microsoft.NET.Sdk";

    public string TargetFrameworks { get; set; } = "net10.0";

    public string OutputType { get; set; } = "Exe";

    public List<string> Runtimes { get; } = [];

    public bool ReferenceAll { get; set; }

    public bool ReferenceTypstium { get; set; } = true;

    public Dictionary<string, string> Properties { get; } = new(StringComparer.Ordinal) { ["UseAppHost"] = "false" };

    public List<string> ExtraItems { get; } = [];

    public static async Task<SampleProject> CreateAsync(string name = "App", string folder = "App", params string[] runtimes)
    {
        var project = new SampleProject(await PackageFeed.GetFixtureAsync(), folder) { Name = name };
        project.Runtimes.AddRange(runtimes);
        return project;
    }

    /// <summary>Writes the project, Program.cs, nuget.config and global.json.</summary>
    public SampleProject Write()
    {
        var references = new StringBuilder();
        if (ReferenceTypstium)
        {
            references.AppendLine($"""    <PackageReference Include="Typstium" Version="{_feed.Version}" />""");
        }

        foreach (var rid in Runtimes)
        {
            references.AppendLine($"""    <PackageReference Include="Typstium.Runtime.{rid}" Version="{_feed.Version}" />""");
        }

        if (ReferenceAll)
        {
            references.AppendLine($"""    <PackageReference Include="Typstium.Runtime.All" Version="{_feed.Version}" />""");
        }

        var properties = string.Concat(Properties.Select(property => $"    <{property.Key}>{property.Value}</{property.Key}>\n"));
        File.WriteAllText(Path.Combine(Directory, Name + ".csproj"), $"""
            <Project Sdk="{Sdk}">
              <PropertyGroup>
                <TargetFrameworks>{TargetFrameworks}</TargetFrameworks>
                <OutputType>{OutputType}</OutputType>
            {properties}  </PropertyGroup>
              <ItemGroup>
            {references}{string.Concat(ExtraItems.Select(item => "    " + item + "\n"))}  </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(Directory, "Program.cs"), OutputType == "Exe" ? "System.Console.WriteLine(\"hello\");\n" : "public static class Library { }\n");
        File.WriteAllText(Path.Combine(Directory, "nuget.config"), $"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="feed" value="{_feed.Directory}" />
                <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
              </packageSources>
            </configuration>
            """);
        File.Copy(Path.Combine(Typstium.Testing.RepositoryPaths.Root, "global.json"), Path.Combine(Directory, "global.json"), overwrite: true);
        return this;
    }

    /// <summary>Runs <c>dotnet &lt;command&gt;</c> in the project folder with the feed's cache.</summary>
    public Task<DotNetRun> RunAsync(params string[] arguments) =>
        DotNet.RunAsync(Directory, _feed.Environment, [.. arguments, "-nodeReuse:false"]);

    /// <summary>The Typst-related files under <paramref name="relativeFolder"/>: binaries and the manifest, '/'-separated, sorted.</summary>
    public string[] TypstFiles(string relativeFolder)
    {
        var root = Path.Combine(Directory, relativeFolder);
        return !System.IO.Directory.Exists(root)
            ? []
            : System.IO.Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Where(file => Path.GetFileName(file) is "typst" or "typst.exe" or "typstium.runtime.json")
                .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))
                .Order(StringComparer.Ordinal)
                .ToArray();
    }

    public string ReadText(string relativePath) => File.ReadAllText(Path.Combine(Directory, relativePath)).ReplaceLineEndings("\n");

    public void Dispose() => _temp.Dispose();
}
```

- [ ] **Step 2: Write the failing tests**

`CopyTests.cs`:

```csharp
using Typstium.Runtime.Testing;

namespace Typstium.Runtime.Tests;

public class CopyTests
{
    private static async Task<SampleProject> BuiltAsync(SampleProject project, params string[] extra)
    {
        project.Write();
        var run = await project.RunAsync(["build", "-c", "Release", "-o", "out", .. extra]);
        await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
        return project;
    }

    [Test]
    [Property(Scenario.Key, "RS1")]
    [Property(Scenario.Key, "RS27")]
    public async Task A_build_without_a_runtime_identifier_copies_every_referenced_runtime()
    {
        using var project = await BuiltAsync(await SampleProject.CreateAsync(runtimes: ["linux-x64", "win-x64"]));

        await Assert.That(project.TypstFiles("out")).IsEquivalentTo(new[] { "typst/linux-x64/typst", "typst/win-x64/typst.exe", "typstium.runtime.json" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(project.ReadText("out/typstium.runtime.json")).IsEqualTo("""
            {
              "version": 1,
              "runtimes": {
                "linux-x64": "typst/linux-x64/typst",
                "win-x64": "typst/win-x64/typst.exe"
              }
            }

            """);
    }

    [Test]
    [Property(Scenario.Key, "RS1")]
    public async Task The_meta_package_brings_all_seven_runtimes()
    {
        var project = await SampleProject.CreateAsync();
        project.ReferenceAll = true;
        using var built = await BuiltAsync(project);

        await Assert.That(built.TypstFiles("out").Count(file => file.StartsWith("typst/", StringComparison.Ordinal))).IsEqualTo(7);
    }

    [Test]
    [Property(Scenario.Key, "RS2")]
    public async Task A_portable_publish_copies_every_referenced_runtime_and_the_manifest()
    {
        using var project = await SampleProject.CreateAsync(runtimes: ["linux-x64", "osx-arm64"]);
        project.Write();
        var run = await project.RunAsync("publish", "-c", "Release", "-o", "pub");

        await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
        await Assert.That(project.TypstFiles("pub")).IsEquivalentTo(new[] { "typst/linux-x64/typst", "typst/osx-arm64/typst", "typstium.runtime.json" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    [Property(Scenario.Key, "RS13")]
    [Property(Scenario.Key, "RS29")]
    public async Task Turning_the_copy_off_copies_nothing_and_writes_no_manifest()
    {
        var project = await SampleProject.CreateAsync(runtimes: ["linux-x64"]);
        project.Properties["TypstiumCopyRuntime"] = "false";
        using var built = await BuiltAsync(project);

        await Assert.That(built.TypstFiles("out")).IsEmpty();
    }

    [Test]
    [Property(Scenario.Key, "RS29")]
    public async Task Copying_on_publish_only_leaves_the_build_output_without_runtimes()
    {
        var project = await SampleProject.CreateAsync(runtimes: ["linux-x64"]);
        project.Properties["TypstiumCopyRuntimeOnBuild"] = "false";
        using var built = await BuiltAsync(project);
        var publish = await built.RunAsync("publish", "-c", "Release", "-o", "pub");

        await Assert.That(publish.ExitCode).IsEqualTo(0).Because(publish.Output);
        await Assert.That(built.TypstFiles("out")).IsEmpty();
        await Assert.That(built.TypstFiles("pub")).IsEquivalentTo(new[] { "typst/linux-x64/typst", "typstium.runtime.json" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    [Property(Scenario.Key, "RS29")]
    public async Task Copying_on_build_only_leaves_the_publish_folder_without_runtimes()
    {
        var project = await SampleProject.CreateAsync(runtimes: ["linux-x64"]);
        project.Properties["TypstiumCopyRuntimeOnPublish"] = "false";
        project.Write();
        var publish = await project.RunAsync("publish", "-c", "Release", "-o", "pub");

        await Assert.That(publish.ExitCode).IsEqualTo(0).Because(publish.Output);
        await Assert.That(project.TypstFiles("pub")).IsEmpty();
    }

    [Test]
    [Property(Scenario.Key, "RS33")]
    public async Task Design_time_builds_copy_nothing()
    {
        using var project = await BuiltAsync(await SampleProject.CreateAsync(runtimes: ["linux-x64"]), "-p:DesignTimeBuild=true");

        await Assert.That(project.TypstFiles("out")).IsEmpty();
    }

    [Test]
    [Property(Scenario.Key, "RS34")]
    public async Task An_unchanged_runtime_is_not_copied_again_and_clean_removes_everything()
    {
        using var project = await SampleProject.CreateAsync(runtimes: ["linux-x64"]);
        project.Write();
        await project.RunAsync("build", "-c", "Release");
        var output = Path.Combine(project.Directory, "bin", "Release", "net10.0");
        var binary = Path.Combine(output, "typst", "linux-x64", "typst");
        File.WriteAllText(binary, "marker");
        File.SetLastWriteTimeUtc(binary, DateTime.UtcNow.AddDays(1));

        await project.RunAsync("build", "-c", "Release");
        await Assert.That(File.ReadAllText(binary)).IsEqualTo("marker");

        var clean = await project.RunAsync("clean", "-c", "Release");
        await Assert.That(clean.ExitCode).IsEqualTo(0).Because(clean.Output);
        await Assert.That(project.TypstFiles(Path.Combine("bin", "Release", "net10.0"))).IsEmpty();
    }

    [Test]
    [Property(Scenario.Key, "RS25")]
    public async Task An_app_gets_the_runtime_through_a_library_and_publishes_without_conflicts()
    {
        using var library = await SampleProject.CreateAsync(name: "Lib", folder: "Lib", runtimes: ["linux-x64"]);
        library.OutputType = "Library";
        library.Write();
        using var app = await SampleProject.CreateAsync();
        app.ReferenceTypstium = false;
        app.ExtraItems.Add($"""<ProjectReference Include="{Path.Combine(library.Directory, "Lib.csproj")}" />""");
        app.Write();

        var build = await app.RunAsync("build", "-c", "Release", "-o", "out");
        var publish = await app.RunAsync("publish", "-c", "Release", "-o", "pub");

        await Assert.That(build.ExitCode).IsEqualTo(0).Because(build.Output);
        await Assert.That(publish.ExitCode).IsEqualTo(0).Because(publish.Output);
        await Assert.That(app.TypstFiles("out")).IsEquivalentTo(new[] { "typst/linux-x64/typst", "typstium.runtime.json" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(app.TypstFiles("pub")).IsEquivalentTo(new[] { "typst/linux-x64/typst", "typstium.runtime.json" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/Typstium.Runtime.Tests -- --treenode-filter "/*/*/CopyTests/*"`
Expected: FAIL. The output folders contain no Typst files, because the core package has no targets yet.

- [ ] **Step 4: Write the targets**

`src/Typstium/build/Typstium.targets` (packages.config imports only `build/`):

```xml
<Project>
  <!-- packages.config imports build/ only; the logic lives in buildTransitive/ (docs/specs/runtime-packages.md §3). -->
  <Import Project="$(MSBuildThisFileDirectory)../buildTransitive/Typstium.targets" Condition="'$(_TypstiumTargetsImported)' != 'true'" />
</Project>
```

`src/Typstium/buildTransitive/Typstium.targets` (this task's part; Tasks 7–9 add to it):

```xml
<Project>
  <!--
    Copies the Typst binaries that referenced Typstium.Runtime.<rid> packages declare (TypstiumRuntime items) next to
    the app, and writes typstium.runtime.json, which BundledRuntimeLocator reads (docs/specs/runtime-packages.md §4–§6).
  -->
  <PropertyGroup>
    <_TypstiumTargetsImported>true</_TypstiumTargetsImported>
    <TypstiumCopyRuntime Condition="'$(TypstiumCopyRuntime)' == ''">true</TypstiumCopyRuntime>
    <TypstiumCopyRuntimeOnBuild Condition="'$(TypstiumCopyRuntimeOnBuild)' == ''">true</TypstiumCopyRuntimeOnBuild>
    <TypstiumCopyRuntimeOnPublish Condition="'$(TypstiumCopyRuntimeOnPublish)' == ''">true</TypstiumCopyRuntimeOnPublish>
    <TypstiumRuntimeDirectory Condition="'$(TypstiumRuntimeDirectory)' == ''">typst/{rid}</TypstiumRuntimeDirectory>
  </PropertyGroup>

  <!-- BeforeTargets, not AssignTargetPathsDependsOn: this file may be imported before Microsoft.Common sets that property. -->
  <Target Name="_TypstiumAddRuntimeFiles" BeforeTargets="AssignTargetPaths"
          Condition="'$(TypstiumCopyRuntime)' == 'true' and '$(DesignTimeBuild)' != 'true'">

    <!-- build/ and buildTransitive/ of one package can both be imported; keep one item per RID. -->
    <RemoveDuplicates Inputs="@(TypstiumRuntime)">
      <Output TaskParameter="Filtered" ItemName="_TypstiumKnown" />
    </RemoveDuplicates>

    <ItemGroup>
      <_TypstiumSelected Include="@(_TypstiumKnown)" />
    </ItemGroup>

    <PropertyGroup>
      <_TypstiumDirectory>$([System.Text.RegularExpressions.Regex]::Replace('$(TypstiumRuntimeDirectory.Replace(`\`, `/`))', '/+$', ''))</_TypstiumDirectory>
    </PropertyGroup>

    <ItemGroup>
      <_TypstiumSelected>
        <TypstiumTargetPath>$([System.String]::Copy('$(_TypstiumDirectory)').Replace('{rid}', '%(Identity)'))/$([System.IO.Path]::GetFileName('%(Executable)'))</TypstiumTargetPath>
      </_TypstiumSelected>
      <ContentWithTargetPath Include="@(_TypstiumSelected->'%(Executable)')">
        <TargetPath>%(TypstiumTargetPath)</TargetPath>
        <CopyToOutputDirectory Condition="'$(TypstiumCopyRuntimeOnBuild)' == 'true'">PreserveNewest</CopyToOutputDirectory>
        <CopyToOutputDirectory Condition="'$(TypstiumCopyRuntimeOnBuild)' != 'true'">Never</CopyToOutputDirectory>
        <CopyToPublishDirectory Condition="'$(TypstiumCopyRuntimeOnPublish)' == 'true'">PreserveNewest</CopyToPublishDirectory>
        <CopyToPublishDirectory Condition="'$(TypstiumCopyRuntimeOnPublish)' != 'true'">Never</CopyToPublishDirectory>
        <ExcludeFromSingleFile>true</ExcludeFromSingleFile>
      </ContentWithTargetPath>
    </ItemGroup>

    <PropertyGroup>
      <_TypstiumManifestRuntimes>@(_TypstiumSelected->'    &quot;%(Identity)&quot;: &quot;%(TypstiumTargetPath)&quot;', ',%0A')</_TypstiumManifestRuntimes>
      <_TypstiumManifestJson>{%0A  &quot;version&quot;: 1,%0A  &quot;runtimes&quot;: {%0A$(_TypstiumManifestRuntimes)%0A  }%0A}</_TypstiumManifestJson>
    </PropertyGroup>
  </Target>

  <!-- The manifest is written into each folder rather than added as an item: as an item it would flow through
       ProjectReference, and an app and a library would collide on it at publish (NETSDK1152). -->
  <Target Name="_TypstiumWriteOutputManifest" AfterTargets="CopyFilesToOutputDirectory"
          Condition="'@(_TypstiumSelected)' != '' and '$(TypstiumCopyRuntimeOnBuild)' == 'true'">
    <WriteLinesToFile File="$(OutDir)typstium.runtime.json" Lines="$(_TypstiumManifestJson)" Overwrite="true" WriteOnlyWhenDifferent="true" />
    <ItemGroup>
      <FileWrites Include="$(OutDir)typstium.runtime.json" />
    </ItemGroup>
  </Target>

  <Target Name="_TypstiumWritePublishManifest" AfterTargets="CopyFilesToPublishDirectory"
          Condition="'@(_TypstiumSelected)' != '' and '$(TypstiumCopyRuntimeOnPublish)' == 'true'">
    <WriteLinesToFile File="$(PublishDir)typstium.runtime.json" Lines="$(_TypstiumManifestJson)" Overwrite="true" WriteOnlyWhenDifferent="true" />
  </Target>
</Project>
```

In `src/Typstium/Typstium.csproj`, add:

```xml
  <ItemGroup>
    <!-- MSBuild logic for the runtime packages (docs/specs/runtime-packages.md §3–§6). -->
    <None Include="build/Typstium.targets" Pack="true" PackagePath="build/" />
    <None Include="buildTransitive/Typstium.targets" Pack="true" PackagePath="buildTransitive/" />
  </ItemGroup>
```

- [ ] **Step 5: Run the tests and make them pass**

Run: `dotnet test tests/Typstium.Runtime.Tests -- --treenode-filter "/*/*/CopyTests/*"`
Expected: PASS. If the ProjectReference publish test fails with `NETSDK1152` on the binary (an app and a library contribute
the same source to the same path), stop creating items in libraries that aren't test projects. Add
`and ('$(OutputType)' != 'Library' or '$(IsTestProject)' == 'true')` to `_TypstiumAddRuntimeFiles`'s condition, change RS25's
expected result in §9 to "App output has the binary; the library's output doesn't", and say so in the commit message.

- [ ] **Step 6: Write refinement 1 into the spec**

In §6, replace the **Manifest** paragraph's "and adds it as a content item with the same copy and single-file settings"
with the targets above: "and writes it straight into `$(OutDir)` after `CopyFilesToOutputDirectory` and into `$(PublishDir)`
after `CopyFilesToPublishDirectory` (`WriteOnlyWhenDifferent`, registered in `FileWrites`). As a content item it would
flow through `ProjectReference`, and an app and a library would collide on it at publish (`NETSDK1152`)."

- [ ] **Step 7: Commit**

```bash
git add src/Typstium tests/Typstium.Runtime.Testing tests/Typstium.Runtime.Tests docs/specs/runtime-packages.md
git commit -m "Copy referenced runtimes next to the app and write the runtime manifest"
```

---

### Task 7: Choosing RIDs

**Files:**
- Modify: `src/Typstium/buildTransitive/Typstium.targets` (`_TypstiumAddRuntimeFiles`)
- Create: `tests/Typstium.Runtime.Tests/SelectionTests.cs`
- Modify: `docs/specs/runtime-packages.md` §10 (refinement 4)

**Interfaces:**
- Consumes: `SampleProject` (Task 6).
- Produces: the private properties `_TypstiumRid` and `_TypstiumMappedRid`. The `_TypstiumSelected` contents now follow §5.

- [ ] **Step 1: Write the failing tests**

```csharp
using Typstium.Runtime.Testing;

namespace Typstium.Runtime.Tests;

public class SelectionTests
{
    private static async Task<(SampleProject Project, DotNetRun Run)> PublishAsync(string rid, string[] runtimes, params string[] extra)
    {
        var project = await SampleProject.CreateAsync(runtimes: runtimes);
        project.Write();
        var run = await project.RunAsync(["publish", "-c", "Release", "-r", rid, "--self-contained", "false", "-o", "pub", .. extra]);
        return (project, run);
    }

    private static string[] Typst(params string[] paths) => [.. paths, "typstium.runtime.json"];

    [Test]
    [Property(Scenario.Key, "RS3")]
    [Arguments("linux-x64", "typst/linux-x64/typst")]
    [Arguments("win-arm64", "typst/win-arm64/typst.exe")]
    [Arguments("osx-arm64", "typst/osx-arm64/typst")]
    public async Task A_publish_for_a_runtime_identifier_copies_only_its_runtime(string rid, string expected)
    {
        var (project, run) = await PublishAsync(rid, ["linux-x64", "win-arm64", "osx-arm64"]);
        using (project)
        {
            await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
            await Assert.That(project.TypstFiles("pub")).IsEquivalentTo(Typst(expected), TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }
    }

    [Test]
    [Property(Scenario.Key, "RS3")]
    [Property(Scenario.Key, "RS11")]
    [Arguments("linux-musl-x64", "typst/linux-x64/typst")]
    [Arguments("linux-musl-arm64", "typst/linux-arm64/typst")]
    [Arguments("ubuntu.22.04-x64", "typst/linux-x64/typst")]
    [Arguments("win10-x64", "typst/win-x64/typst.exe")]
    [Arguments("osx.13-arm64", "typst/osx-arm64/typst")]
    public async Task Musl_distro_and_versioned_runtime_identifiers_map_to_the_shipped_ones(string rid, string expected)
    {
        var (project, run) = await PublishAsync(rid, ["linux-x64", "linux-arm64", "win-x64", "osx-arm64"], "-p:UseRidGraph=true");
        using (project)
        {
            await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
            await Assert.That(project.TypstFiles("pub")).IsEquivalentTo(Typst(expected), TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }
    }

    [Test]
    [Property(Scenario.Key, "RS40")]
    public async Task A_32_bit_windows_app_gets_every_windows_runtime()
    {
        var (project, run) = await PublishAsync("win-x86", ["win-x64", "win-arm64", "linux-x64"]);
        using (project)
        {
            await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
            await Assert.That(project.TypstFiles("pub")).IsEquivalentTo(Typst("typst/win-arm64/typst.exe", "typst/win-x64/typst.exe"), TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }
    }

    [Test]
    [Property(Scenario.Key, "RS3")]
    public async Task No_matching_runtime_warns_and_copies_nothing()
    {
        var (project, run) = await PublishAsync("linux-x64", ["win-x64"]);
        using (project)
        {
            await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
            await Assert.That(run.DiagnosticCodes).Contains("TYPSTIUM0001");
            await Assert.That(project.TypstFiles("pub")).IsEmpty();
        }
    }

    [Test]
    [Property(Scenario.Key, "RS7")]
    [Property(Scenario.Key, "RS8")]
    public async Task A_runtime_identifier_in_the_project_filters_a_plain_build()
    {
        using var project = await SampleProject.CreateAsync(runtimes: ["linux-x64", "win-x64"]);
        project.Properties["RuntimeIdentifier"] = "win-x64";
        project.Properties["SelfContained"] = "false";
        project.Write();
        var run = await project.RunAsync("build", "-c", "Release", "-o", "out");

        await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
        await Assert.That(project.TypstFiles("out")).IsEquivalentTo(Typst("typst/win-x64/typst.exe"), TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    [Property(Scenario.Key, "RS30")]
    public async Task An_explicit_list_overrides_the_runtime_identifier_and_warns_about_unknown_names()
    {
        var (project, run) = await PublishAsync("linux-x64", ["linux-x64", "win-x64", "osx-arm64"], "-p:TypstiumRuntimeIdentifiers=win-x64;osx-arm64;linux-riscv64");
        using (project)
        {
            await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
            await Assert.That(run.DiagnosticCodes).Contains("TYPSTIUM0004");
            await Assert.That(run.Output).Contains("linux-riscv64");
            await Assert.That(project.TypstFiles("pub")).IsEquivalentTo(Typst("typst/osx-arm64/typst", "typst/win-x64/typst.exe"), TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }
    }

    [Test]
    public async Task Runtime_identifiers_are_matched_ignoring_case()
    {
        var (project, run) = await PublishAsync("Linux-X64", ["linux-x64", "win-x64"], "-p:TypstiumRuntimeIdentifiers=WIN-X64");
        using (project)
        {
            await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
            await Assert.That(project.TypstFiles("pub")).IsEquivalentTo(Typst("typst/win-x64/typst.exe"), TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }

        var (second, secondRun) = await PublishAsync("Linux-X64", ["linux-x64", "win-x64"]);
        using (second)
        {
            await Assert.That(secondRun.ExitCode).IsEqualTo(0).Because(secondRun.Output);
            await Assert.That(second.TypstFiles("pub")).IsEquivalentTo(Typst("typst/linux-x64/typst"), TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }
    }

    [Test]
    public async Task Republishing_into_the_same_folder_rewrites_the_manifest_for_the_last_runtime()
    {
        using var project = await SampleProject.CreateAsync(runtimes: ["linux-x64", "win-x64"]);
        project.Write();
        foreach (var rid in new[] { "linux-x64", "win-x64", "linux-x64" })
        {
            var run = await project.RunAsync("publish", "-c", "Release", "-r", rid, "--self-contained", "false", "-o", "pub");
            await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
        }

        await Assert.That(project.ReadText("pub/typstium.runtime.json")).Contains("\"linux-x64\": \"typst/linux-x64/typst\"");
        await Assert.That(project.ReadText("pub/typstium.runtime.json")).DoesNotContain("win-x64");
    }

    [Test]
    public async Task An_unknown_contract_version_warns_and_the_runtime_is_ignored()
    {
        using var project = await SampleProject.CreateAsync(runtimes: ["linux-x64"]);
        project.ExtraItems.Add("""<TypstiumRuntime Include="win-x64" Executable="$(MSBuildProjectDirectory)/Program.cs" ContractVersion="2" />""");
        project.Write();
        var run = await project.RunAsync("build", "-c", "Release", "-o", "out");

        await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
        await Assert.That(run.DiagnosticCodes).Contains("TYPSTIUM0005");
        await Assert.That(project.TypstFiles("out")).IsEquivalentTo(Typst("typst/linux-x64/typst"), TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Typstium.Runtime.Tests -- --treenode-filter "/*/*/SelectionTests/*"`
Expected: FAIL. Every referenced runtime is copied regardless of the RID, and no diagnostics appear.

- [ ] **Step 3: Write the implementation**

In `_TypstiumAddRuntimeFiles`, replace

```xml
    <ItemGroup>
      <_TypstiumSelected Include="@(_TypstiumKnown)" />
    </ItemGroup>
```

with:

```xml
    <!-- §5. Contract versions this core understands; newer RID packages may declare items differently. -->
    <ItemGroup>
      <_TypstiumUnknownContract Include="@(_TypstiumKnown)" Condition="'%(_TypstiumKnown.ContractVersion)' != '1'" />
      <_TypstiumKnown Remove="@(_TypstiumUnknownContract)" />
    </ItemGroup>
    <Warning Code="TYPSTIUM0005" Condition="'@(_TypstiumUnknownContract)' != ''"
             Text="The Typst runtime for %(_TypstiumUnknownContract.Identity) declares ContractVersion '%(_TypstiumUnknownContract.ContractVersion)', which this version of Typstium doesn't understand, so it is ignored. Update the Typstium package." />

    <PropertyGroup>
      <_TypstiumRid>$(RuntimeIdentifier.ToLowerInvariant())</_TypstiumRid>
      <_TypstiumRidArch Condition="'$(_TypstiumRid)' != ''">$([System.Text.RegularExpressions.Regex]::Match('$(_TypstiumRid)', '[^-]+$').Value)</_TypstiumRidArch>
      <_TypstiumRidOs Condition="'$(_TypstiumRid)' != '' and $([System.Text.RegularExpressions.Regex]::IsMatch('$(_TypstiumRid)', '^win'))">win</_TypstiumRidOs>
      <_TypstiumRidOs Condition="'$(_TypstiumRid)' != '' and $([System.Text.RegularExpressions.Regex]::IsMatch('$(_TypstiumRid)', '^(osx|macos)'))">osx</_TypstiumRidOs>
      <_TypstiumRidOs Condition="'$(_TypstiumRid)' != '' and '$(_TypstiumRidOs)' == '' and !$([System.Text.RegularExpressions.Regex]::IsMatch('$(_TypstiumRid)', '^(freebsd|illumos|solaris|haiku|android|ios|tvos|maccatalyst|browser|wasi)'))">linux</_TypstiumRidOs>
      <_TypstiumMappedRid Condition="'$(_TypstiumRidOs)' != ''">$(_TypstiumRidOs)-$(_TypstiumRidArch)</_TypstiumMappedRid>
    </PropertyGroup>

    <!-- 1. An explicit list wins. -->
    <ItemGroup Condition="'$(TypstiumRuntimeIdentifiers)' != ''">
      <_TypstiumRequested Include="$(TypstiumRuntimeIdentifiers.ToLowerInvariant())" />
      <_TypstiumNotRequested Include="@(_TypstiumKnown)" Exclude="@(_TypstiumRequested)" />
      <_TypstiumSelected Include="@(_TypstiumKnown)" Exclude="@(_TypstiumNotRequested)" />
      <_TypstiumUnavailable Include="@(_TypstiumRequested)" Exclude="@(_TypstiumKnown)" />
    </ItemGroup>
    <Warning Code="TYPSTIUM0004" Condition="'@(_TypstiumUnavailable)' != ''"
             Text="TypstiumRuntimeIdentifiers lists '%(_TypstiumUnavailable.Identity)', but no referenced Typstium.Runtime package provides it. Reference Typstium.Runtime.%(_TypstiumUnavailable.Identity), or remove it from the list." />

    <!-- 2. A RID: its shipped runtime; a 32-bit Windows app may run on x64 or Arm64 Windows, so it gets both (D12). -->
    <ItemGroup Condition="'$(TypstiumRuntimeIdentifiers)' == '' and '$(_TypstiumMappedRid)' == 'win-x86'">
      <_TypstiumSelected Include="@(_TypstiumKnown)" Condition="$([System.String]::Copy('%(Identity)').StartsWith('win-'))" />
    </ItemGroup>
    <ItemGroup Condition="'$(TypstiumRuntimeIdentifiers)' == '' and '$(_TypstiumMappedRid)' != '' and '$(_TypstiumMappedRid)' != 'win-x86'">
      <_TypstiumSelected Include="@(_TypstiumKnown)" Condition="'%(Identity)' == '$(_TypstiumMappedRid)'" />
    </ItemGroup>

    <!-- 3. No RID: everything referenced; the locator picks at runtime. -->
    <ItemGroup Condition="'$(TypstiumRuntimeIdentifiers)' == '' and '$(_TypstiumRid)' == ''">
      <_TypstiumSelected Include="@(_TypstiumKnown)" />
    </ItemGroup>

    <Warning Code="TYPSTIUM0001" Condition="'$(TypstiumRuntimeIdentifiers)' == '' and '$(_TypstiumRid)' != '' and '@(_TypstiumKnown)' != '' and '@(_TypstiumSelected)' == ''"
             Text="No referenced Typstium.Runtime package matches the runtime identifier '$(RuntimeIdentifier)' (referenced: @(_TypstiumKnown, ', ')), so no Typst binary is copied. Reference the matching Typstium.Runtime package, or ignore this if Typst is installed where the app runs." />
```

If `Exclude` inside a target turns out not to match itemspecs case-insensitively, the
`Runtime_identifiers_are_matched_ignoring_case` test fails. The fix is to lowercase `_TypstiumKnown`'s identity. Package
`Include` values are already lowercase, so only the requested list needs `ToLowerInvariant()`, which it already has.

- [ ] **Step 4: Run all runtime tests**

Run: `dotnet test tests/Typstium.Runtime.Tests`
Expected: PASS, including `CopyTests` (no RID → everything).

- [ ] **Step 5: Write refinement 4 into the spec**

In §10 "Targets tests", add: "Distro and versioned RIDs (`ubuntu.22.04-x64`, `win10-x64`, `osx.13-arm64`) are built with
`UseRidGraph=true` and `UseAppHost=false`, because .NET 8+ rejects non-portable RIDs otherwise and no apphost exists for them."

- [ ] **Step 6: Commit**

```bash
git add src/Typstium/buildTransitive/Typstium.targets tests/Typstium.Runtime.Tests docs/specs/runtime-packages.md
git commit -m "Select runtimes by runtime identifier, with an explicit list and warnings"
```

---

### Task 8: Directory patterns and publish shapes

**Files:**
- Modify: `src/Typstium/buildTransitive/Typstium.targets`
- Create: `tests/Typstium.Runtime.Tests/DirectoryTests.cs`, `tests/Typstium.Runtime.Tests/PublishShapeTests.cs`

**Interfaces:**
- Consumes: Tasks 6–7.
- Produces: `TYPSTIUM0002` and `TYPSTIUM0003` errors.

- [ ] **Step 1: Write the failing tests**

`DirectoryTests.cs`:

```csharp
using Typstium.Runtime.Testing;

namespace Typstium.Runtime.Tests;

public class DirectoryTests
{
    private static async Task<(SampleProject Project, DotNetRun Run)> BuildAsync(string directory, params string[] runtimes)
    {
        var project = await SampleProject.CreateAsync(runtimes: runtimes);
        project.Properties["TypstiumRuntimeDirectory"] = directory;
        project.Write();
        return (project, await project.RunAsync("build", "-c", "Release", "-o", "out"));
    }

    [Test]
    [Property(Scenario.Key, "RS31")]
    public async Task A_custom_directory_is_used_and_described_in_the_manifest()
    {
        var (project, run) = await BuildAsync("tools/typst-{rid}", "linux-x64", "win-x64");
        using (project)
        {
            await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
            await Assert.That(project.TypstFiles("out")).IsEquivalentTo(new[] { "tools/typst-linux-x64/typst", "tools/typst-win-x64/typst.exe", "typstium.runtime.json" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(project.ReadText("out/typstium.runtime.json")).Contains("\"win-x64\": \"tools/typst-win-x64/typst.exe\"");
        }
    }

    [Test]
    [Property(Scenario.Key, "RS32")]
    public async Task A_pattern_without_rid_gives_a_flat_layout_for_one_runtime()
    {
        var (project, run) = await BuildAsync("tools", "linux-x64");
        using (project)
        {
            await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
            await Assert.That(project.TypstFiles("out")).IsEquivalentTo(new[] { "tools/typst", "typstium.runtime.json" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }
    }

    [Test]
    [Property(Scenario.Key, "RS32")]
    public async Task A_pattern_without_rid_and_two_runtimes_is_an_error()
    {
        var (project, run) = await BuildAsync("tools", "linux-x64", "osx-x64");
        using (project)
        {
            await Assert.That(run.ExitCode).IsNotEqualTo(0);
            await Assert.That(run.DiagnosticCodes).Contains("TYPSTIUM0002");
        }
    }

    [Test]
    [Property(Scenario.Key, "RS31")]
    [Arguments("/opt/typst/{rid}")]
    [Arguments("C:/typst/{rid}")]
    [Arguments("C:typst/{rid}")]
    [Arguments("../typst/{rid}")]
    [Arguments("tools/../../{rid}")]
    [Arguments("tools/\"{rid}")]
    public async Task Directories_outside_the_output_folder_are_an_error(string directory)
    {
        var (project, run) = await BuildAsync(directory, "linux-x64");
        using (project)
        {
            await Assert.That(run.ExitCode).IsNotEqualTo(0);
            await Assert.That(run.DiagnosticCodes).Contains("TYPSTIUM0003");
        }
    }

    [Test]
    public async Task A_directory_with_backslashes_is_written_with_forward_slashes()
    {
        var (project, run) = await BuildAsync(@"tools\typst\{rid}\", "win-x64");
        using (project)
        {
            await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
            await Assert.That(project.ReadText("out/typstium.runtime.json")).Contains("\"win-x64\": \"tools/typst/win-x64/typst.exe\"");
            await Assert.That(project.TypstFiles("out")).Contains("tools/typst/win-x64/typst.exe");
        }
    }
}
```

`PublishShapeTests.cs`:

```csharp
using System.Runtime.InteropServices;
using Typstium.Runtime.Testing;

namespace Typstium.Runtime.Tests;

public class PublishShapeTests
{
    // The machine's own RID, for publishes that need its runtime pack or apphost.
    private static string HostRid =>
        (OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux") + "-" +
        RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();

    [Test]
    [Property(Scenario.Key, "RS4")]
    public async Task A_single_file_publish_keeps_the_binary_and_the_manifest_next_to_the_app()
    {
        using var project = await SampleProject.CreateAsync(runtimes: [HostRid]);
        project.Properties["UseAppHost"] = "true";
        project.Write();
        var run = await project.RunAsync("publish", "-c", "Release", "-r", HostRid, "--self-contained", "false", "-p:PublishSingleFile=true", "-o", "pub");

        var executable = PackageFeed.ExecutableName(HostRid);
        await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
        await Assert.That(project.TypstFiles("pub")).IsEquivalentTo(new[] { $"typst/{HostRid}/{executable}", "typstium.runtime.json" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    [Property(Scenario.Key, "RS5")]
    public async Task A_trimmed_self_contained_publish_keeps_the_binary()
    {
        using var project = await SampleProject.CreateAsync(runtimes: [HostRid]);
        project.Properties["UseAppHost"] = "true";
        project.Write();
        var run = await project.RunAsync("publish", "-c", "Release", "-r", HostRid, "--self-contained", "true", "-p:PublishTrimmed=true", "-o", "pub");

        await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
        await Assert.That(project.TypstFiles("pub")).Contains($"typst/{HostRid}/{PackageFeed.ExecutableName(HostRid)}");
    }

    [Test]
    [Property(Scenario.Key, "RS6")]
    public async Task Each_target_framework_gets_its_own_copy()
    {
        using var project = await SampleProject.CreateAsync(runtimes: ["linux-x64"]);
        project.TargetFrameworks = "net10.0;net48";
        project.Write();
        var run = await project.RunAsync("build", "-c", "Release");

        await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
        await Assert.That(project.TypstFiles("bin/Release/net10.0")).Contains("typst/linux-x64/typst");
        await Assert.That(project.TypstFiles("bin/Release/net48")).Contains("typst/linux-x64/typst");
    }
}
```

(For RS6 on Linux and macOS, `net48` builds against `Microsoft.NETFramework.ReferenceAssemblies`, which the SDK adds
automatically. With `UseAppHost=false` no apphost is needed.)

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Typstium.Runtime.Tests -- --treenode-filter "/*/*/DirectoryTests/*"`
Expected: FAIL. No errors are emitted (`TYPSTIUM0002`/`0003` don't exist yet). The flat layout and custom directory tests
may already pass. The `PublishShapeTests` should pass already: they pin behavior that standard MSBuild gives us through
`ExcludeFromSingleFile`.

- [ ] **Step 3: Write the implementation**

In `_TypstiumAddRuntimeFiles`, after the `_TypstiumDirectory` property group, add:

```xml
    <Error Code="TYPSTIUM0003" Condition="'@(_TypstiumSelected)' != '' and ('$(_TypstiumDirectory)' == '' or $([System.IO.Path]::IsPathRooted('$(_TypstiumDirectory)')) or $(_TypstiumDirectory.StartsWith('/')) or $(_TypstiumDirectory.Contains(':')) or $(_TypstiumDirectory.Contains('&quot;')) or $([System.Text.RegularExpressions.Regex]::IsMatch('$(_TypstiumDirectory)', '(^|/)\.\.(/|$)')))"
           Text="TypstiumRuntimeDirectory '$(TypstiumRuntimeDirectory)' must be a relative folder inside the output folder, without '..', ':' or quotes, e.g. 'typst/{rid}'." />
    <Error Code="TYPSTIUM0002" Condition="!$(_TypstiumDirectory.Contains('{rid}')) and '@(_TypstiumSelected->Count())' &gt; '1'"
           Text="TypstiumRuntimeDirectory '$(TypstiumRuntimeDirectory)' has no {rid}, but @(_TypstiumSelected->Count()) runtimes are selected (@(_TypstiumSelected, ', ')), so they would overwrite each other. Add {rid}, or select one runtime with RuntimeIdentifier or TypstiumRuntimeIdentifiers." />
```

- [ ] **Step 4: Run all runtime tests**

Run: `dotnet test tests/Typstium.Runtime.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Typstium/buildTransitive/Typstium.targets tests/Typstium.Runtime.Tests
git commit -m "Validate the runtime directory pattern and pin publish shapes"
```

---

### Task 9: Unix execute bit, unsupported platforms, classic projects

**Files:**
- Modify: `src/Typstium/buildTransitive/Typstium.targets`
- Create: `tests/Typstium.Runtime.Tests/PlatformTests.cs`, `tests/Typstium.Runtime.Tests/ClassicProjectTests.cs`

**Interfaces:**
- Consumes: Tasks 6–8.
- Produces: `TYPSTIUM0006`; `chmod` after both copies; the WebDeploy hook.

- [ ] **Step 1: Write the failing tests**

`PlatformTests.cs`:

```csharp
using Typstium.Runtime.Testing;

namespace Typstium.Runtime.Tests;

public class PlatformTests
{
    [Test]
    [Property(Scenario.Key, "RS35")]
    public async Task Copied_binaries_are_executable_on_unix()
    {
        if (OperatingSystem.IsWindows())
        {
            Skip.Test("Unix file modes don't exist on Windows.");
            return;
        }

        using var project = await SampleProject.CreateAsync(runtimes: ["linux-x64"]);
        project.Write();
        var build = await project.RunAsync("build", "-c", "Release", "-o", "out");
        var publish = await project.RunAsync("publish", "-c", "Release", "-o", "pub");

        await Assert.That(build.ExitCode).IsEqualTo(0).Because(build.Output);
        await Assert.That(publish.ExitCode).IsEqualTo(0).Because(publish.Output);
        foreach (var folder in new[] { "out", "pub" })
        {
            var mode = File.GetUnixFileMode(Path.Combine(project.Directory, folder, "typst", "linux-x64", "typst"));
            await Assert.That(mode.HasFlag(UnixFileMode.UserExecute)).IsTrue();
        }
    }

    [Test]
    [Property(Scenario.Key, "RS24")]
    public async Task Platforms_that_cant_start_processes_copy_nothing_and_say_so()
    {
        using var project = await SampleProject.CreateAsync(runtimes: ["linux-x64"]);
        project.Properties["RuntimeIdentifier"] = "browser-wasm";
        project.Properties["SelfContained"] = "false";
        project.Write();
        var run = await project.RunAsync("build", "-c", "Release", "-o", "out", "-v:n");

        await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
        await Assert.That(run.DiagnosticCodes).Contains("TYPSTIUM0006");
        await Assert.That(project.TypstFiles("out")).IsEmpty();
    }

    [Test]
    public async Task Projects_in_folders_with_spaces_and_non_ascii_characters_copy_and_describe_their_runtimes()
    {
        using var project = await SampleProject.CreateAsync(folder: "My App ü", runtimes: ["linux-x64"]);
        project.Properties["TypstiumRuntimeDirectory"] = "Typst Runtime/{rid}";
        project.Write();
        var run = await project.RunAsync("build", "-c", "Release", "-o", "out dir");

        await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
        await Assert.That(project.TypstFiles("out dir")).IsEquivalentTo(new[] { "Typst Runtime/linux-x64/typst", "typstium.runtime.json" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        using var manifest = System.Text.Json.JsonDocument.Parse(project.ReadText("out dir/typstium.runtime.json"));
        await Assert.That(manifest.RootElement.GetProperty("runtimes").GetProperty("linux-x64").GetString()).IsEqualTo("Typst Runtime/linux-x64/typst");
    }

    [Test]
    [Property(Scenario.Key, "RS20")]
    public async Task A_windows_forms_app_gets_its_runtime()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test("Windows Forms builds only on Windows.");
            return;
        }

        using var project = await SampleProject.CreateAsync(runtimes: ["win-x64"]);
        project.TargetFrameworks = "net10.0-windows";
        project.OutputType = "WinExe";
        project.Properties["UseWindowsForms"] = "true";
        project.Write();
        var run = await project.RunAsync("build", "-c", "Release", "-o", "out");

        await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
        await Assert.That(project.TypstFiles("out")).Contains("typst/win-x64/typst.exe");
    }
}
```

If the SDK rejects `browser-wasm` for a plain console project, replace the RS24 project with a minimal Blazor WebAssembly
one: `Sdk="Microsoft.NET.Sdk.BlazorWebAssembly"`, a `PackageReference` to `Microsoft.AspNetCore.Components.WebAssembly`
`10.0.*`, and `Program.cs` set to
`await Microsoft.AspNetCore.Components.WebAssembly.Hosting.WebAssemblyHostBuilder.CreateDefault(args).Build().RunAsync();`.
Assert the same things against `bin/Release/net10.0/wwwroot/..`.

`ClassicProjectTests.cs` runs `msbuild.exe` on old-style projects. It needs Windows with Visual Studio or Build Tools:

```csharp
using Typstium.Runtime.Testing;
using Typstium.Testing.Fixtures;

namespace Typstium.Runtime.Tests;

public class ClassicProjectTests
{
    // vswhere ships with every Visual Studio and Build Tools install since 2017.
    private static string? FindMSBuild()
    {
        var vswhere = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft Visual Studio", "Installer", "vswhere.exe");
        if (!File.Exists(vswhere))
        {
            return null;
        }

        var run = DotNet.RunToolAsync(vswhere, Path.GetTempPath(), new Dictionary<string, string?>(), "-latest", "-requires", "Microsoft.Component.MSBuild", "-find", @"MSBuild\**\Bin\MSBuild.exe").GetAwaiter().GetResult();
        return run.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault(File.Exists);
    }

    private static async Task<string> RequireMSBuildAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test("Classic projects build with msbuild.exe on Windows.");
        }

        var msbuild = FindMSBuild();
        if (msbuild is null)
        {
            Skip.Test("Visual Studio or Build Tools isn't installed.");
        }

        return await Task.FromResult(msbuild!);
    }

    [Test]
    [Property(Scenario.Key, "RS18")]
    public async Task A_packages_config_project_with_direct_references_gets_its_runtime()
    {
        var msbuild = await RequireMSBuildAsync();
        var feed = await PackageFeed.GetFixtureAsync();
        using var temp = new TemporaryDirectory("classic");
        var v = feed.Version;
        temp.CreateFile("nuget.config", $"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <config><add key="repositoryPath" value="packages" /></config>
              <packageSources><clear /><add key="feed" value="{feed.Directory}" /><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources>
            </configuration>
            """);
        temp.CreateFile("App/packages.config", $"""
            <?xml version="1.0" encoding="utf-8"?>
            <packages>
              <package id="Typstium" version="{v}" targetFramework="net472" />
              <package id="Typstium.Runtime.win-x64" version="{v}" targetFramework="net472" />
            </packages>
            """);
        // The Import lines NuGet adds when a package is installed: props at the top, targets at the bottom.
        temp.CreateFile("App/App.csproj", $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <Import Project="..\packages\Typstium.Runtime.win-x64.{v}\build\Typstium.Runtime.win-x64.props" Condition="Exists('..\packages\Typstium.Runtime.win-x64.{v}\build\Typstium.Runtime.win-x64.props')" />
              <Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props" />
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFrameworkVersion>v4.7.2</TargetFrameworkVersion>
                <OutputPath>bin\</OutputPath>
                <AssemblyName>App</AssemblyName>
              </PropertyGroup>
              <ItemGroup>
                <Reference Include="System" />
                <Compile Include="Program.cs" />
                <None Include="packages.config" />
              </ItemGroup>
              <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
              <Import Project="..\packages\Typstium.{v}\build\Typstium.targets" Condition="Exists('..\packages\Typstium.{v}\build\Typstium.targets')" />
            </Project>
            """);
        temp.CreateFile("App/Program.cs", "static class Program { static void Main() { } }\n");
        var environment = feed.Environment;

        var restore = await DotNet.RunToolAsync(msbuild, temp.Path, environment, @"App\App.csproj", "-t:restore", "-p:RestorePackagesConfig=true", "-nodeReuse:false");
        var build = await DotNet.RunToolAsync(msbuild, temp.Path, environment, @"App\App.csproj", "-p:Configuration=Release", "-nodeReuse:false");

        await Assert.That(restore.ExitCode).IsEqualTo(0).Because(restore.Output);
        await Assert.That(build.ExitCode).IsEqualTo(0).Because(build.Output);
        await Assert.That(File.Exists(temp.Combine("App", "bin", "typst", "win-x64", "typst.exe"))).IsTrue();
        await Assert.That(File.Exists(temp.Combine("App", "bin", "typstium.runtime.json"))).IsTrue();
    }

    [Test]
    [Property(Scenario.Key, "RS17")]
    public async Task A_classic_web_application_publishes_its_runtime_under_bin()
    {
        var msbuild = await RequireMSBuildAsync();
        var webTargets = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(msbuild)!)!)!, "Microsoft", "VisualStudio", "v17.0", "WebApplications", "Microsoft.WebApplication.targets");
        if (!File.Exists(webTargets))
        {
            Skip.Test("The ASP.NET web development workload isn't installed.");
        }

        var feed = await PackageFeed.GetFixtureAsync();
        using var temp = new TemporaryDirectory("classic-web");
        var v = feed.Version;
        temp.CreateFile("nuget.config", $"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources><clear /><add key="feed" value="{feed.Directory}" /><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources>
            </configuration>
            """);
        temp.CreateFile("Web/Web.config", "<configuration />\n");
        temp.CreateFile("Web/Web.csproj", $$"""
            <?xml version="1.0" encoding="utf-8"?>
            <Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props" />
              <PropertyGroup>
                <ProjectTypeGuids>{349c5851-65df-11da-9384-00065b846f21};{fae04ec0-301f-11d3-bf4b-00c04f79efbc}</ProjectTypeGuids>
                <OutputType>Library</OutputType>
                <TargetFrameworkVersion>v4.7.2</TargetFrameworkVersion>
                <OutputPath>bin\</OutputPath>
                <AssemblyName>Web</AssemblyName>
                <VisualStudioVersion>17.0</VisualStudioVersion>
                <VSToolsPath>$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)</VSToolsPath>
              </PropertyGroup>
              <ItemGroup>
                <Reference Include="System" />
                <Reference Include="System.Web" />
                <Content Include="Web.config" />
                <PackageReference Include="Typstium" Version="{{v}}" />
                <PackageReference Include="Typstium.Runtime.win-x64" Version="{{v}}" />
              </ItemGroup>
              <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
              <Import Project="$(VSToolsPath)\WebApplications\Microsoft.WebApplication.targets" />
            </Project>
            """);
        var site = temp.Combine("site");
        var environment = feed.Environment;

        var restore = await DotNet.RunToolAsync(msbuild, temp.Path, environment, @"Web\Web.csproj", "-t:restore", "-nodeReuse:false");
        var publish = await DotNet.RunToolAsync(msbuild, temp.Path, environment, @"Web\Web.csproj", "-p:Configuration=Release",
            "-p:DeployOnBuild=true", "-p:WebPublishMethod=FileSystem", "-p:PublishUrl=" + site, "-nodeReuse:false");

        await Assert.That(restore.ExitCode).IsEqualTo(0).Because(restore.Output);
        await Assert.That(publish.ExitCode).IsEqualTo(0).Because(publish.Output);
        await Assert.That(File.Exists(Path.Combine(site, "bin", "typst", "win-x64", "typst.exe"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(site, "bin", "typstium.runtime.json"))).IsTrue();
    }
}
```

(The project text uses `$$"""` so the GUIDs' single braces stay literal and `{{v}}` interpolates.)

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Typstium.Runtime.Tests -- --treenode-filter "/*/*/PlatformTests/*"` (and `ClassicProjectTests` on Windows)
Expected:
- `Copied_binaries_are_executable_on_unix` fails on Linux and macOS.
- The RS24 test fails, because `TYPSTIUM0006` doesn't exist yet.
- The classic web test fails if WebDeploy doesn't pick up the binaries. If it passes without the hook, drop the hook in
  Step 3, and say in the commit message that WPP already collects `ContentWithTargetPath` items.

- [ ] **Step 3: Write the implementation**

Add a target that classifies the platform, make `_TypstiumAddRuntimeFiles` depend on it
(`DependsOnTargets="_TypstiumCheckPlatform"`), and make the first statement in `_TypstiumAddRuntimeFiles`, before
`RemoveDuplicates`, drop every runtime on an unsupported platform:

```xml
    <ItemGroup Condition="'$(_TypstiumUnsupportedPlatform)' == 'true'">
      <TypstiumRuntime Remove="@(TypstiumRuntime)" />
    </ItemGroup>
```

A target's `Condition` is evaluated before its dependencies run, so the check can't go in the condition; with the
items removed, `_TypstiumKnown` and `_TypstiumSelected` stay empty and every later target skips itself. The
classifying target:

```xml
  <Target Name="_TypstiumCheckPlatform">
    <PropertyGroup>
      <_TypstiumPlatform>$(TargetPlatformIdentifier.ToLowerInvariant())</_TypstiumPlatform>
      <_TypstiumUnsupportedPlatform Condition="'$(_TypstiumPlatform)' == 'android' or '$(_TypstiumPlatform)' == 'ios' or '$(_TypstiumPlatform)' == 'maccatalyst' or '$(_TypstiumPlatform)' == 'tvos' or '$(_TypstiumPlatform)' == 'browser' or $([System.Text.RegularExpressions.Regex]::IsMatch('$(RuntimeIdentifier.ToLowerInvariant())', '^(browser|wasi)-'))">true</_TypstiumUnsupportedPlatform>
    </PropertyGroup>
    <Message Importance="normal" Condition="'$(_TypstiumUnsupportedPlatform)' == 'true' and '@(TypstiumRuntime)' != ''"
             Text="TYPSTIUM0006: Typst can't run as a process on this platform ($(TargetFramework) $(RuntimeIdentifier)), so no Typst binary is copied." />
  </Target>
```

Add the `chmod` targets:

```xml
  <!-- NuGet drops the execute bit when it extracts a package; Copy keeps the source's mode (docs/specs/runtime-packages.md §6). -->
  <Target Name="_TypstiumChmodOutput" AfterTargets="CopyFilesToOutputDirectory"
          Condition="'$(OS)' != 'Windows_NT' and '@(_TypstiumSelected)' != '' and '$(TypstiumCopyRuntimeOnBuild)' == 'true'">
    <Exec Command="chmod +x &quot;$(OutDir)%(_TypstiumSelected.TypstiumTargetPath)&quot;" />
  </Target>

  <Target Name="_TypstiumChmodPublish" AfterTargets="CopyFilesToPublishDirectory"
          Condition="'$(OS)' != 'Windows_NT' and '@(_TypstiumSelected)' != '' and '$(TypstiumCopyRuntimeOnPublish)' == 'true'">
    <Exec Command="chmod +x &quot;$(PublishDir)%(_TypstiumSelected.TypstiumTargetPath)&quot;" />
  </Target>
```

Add the classic web publish hook:

```xml
  <!-- Classic ASP.NET web projects publish through the Web Publishing Pipeline, which only packs files it knows about. -->
  <Target Name="_TypstiumAddWebDeployFiles" BeforeTargets="CopyAllFilesToSingleFolderForPackage;CopyAllFilesToSingleFolderForMsdeploy"
          Condition="'@(_TypstiumSelected)' != '' and '$(TypstiumCopyRuntimeOnPublish)' == 'true'">
    <ItemGroup>
      <FilesForPackagingFromProject Include="@(_TypstiumSelected->'%(Executable)')">
        <DestinationRelativePath>bin\%(TypstiumTargetPath)</DestinationRelativePath>
      </FilesForPackagingFromProject>
      <FilesForPackagingFromProject Include="$(OutDir)typstium.runtime.json" Condition="Exists('$(OutDir)typstium.runtime.json')">
        <DestinationRelativePath>bin\typstium.runtime.json</DestinationRelativePath>
      </FilesForPackagingFromProject>
    </ItemGroup>
  </Target>
```

- [ ] **Step 4: Run all runtime tests on Windows and in WSL**

Run: `dotnet test tests/Typstium.Runtime.Tests`, then `wsl -d Ubuntu -- bash -lc "cd /mnt/c/Dev/GitHub/mgnslndh/Typstium && dotnet test tests/Typstium.Runtime.Tests"`
Expected: PASS on both. Skips appear only for platform-gated tests.

- [ ] **Step 5: Commit**

```bash
git add src/Typstium/buildTransitive/Typstium.targets tests/Typstium.Runtime.Tests
git commit -m "Restore the execute bit, skip platforms without processes, and publish classic web apps"
```

---

### Task 10: Pinned assets and release digests (build library)

**Files:**
- Create: `build/Build.Cake.Extensions/Downloads/PinnedAssets.cs`
- Create: `build/Build.Cake.Extensions/GitHub/GitHubReleaseDigests.cs`
- Test: `build/Build.Cake.Extensions.Tests/Downloads/PinnedAssetsTests.cs`, `build/Build.Cake.Extensions.Tests/GitHub/GitHubReleaseDigestsTests.cs`
- Modify: `docs/specs/runtime-packages.md` §8 (refinement 2)

**Interfaces:**
- Produces:

```csharp
namespace Build.Cake.Extensions.Downloads;
public sealed record PinnedAsset(string Name, string Sha256);
public sealed record PinnedAssets(string Version, IReadOnlyDictionary<string, PinnedAsset> Assets)
{
    public static PinnedAssets Parse(string json);   // throws InvalidDataException naming the problem
    public string ToJson();                           // keys sorted ordinally, indented 2, '\n', trailing newline
}

namespace Build.Cake.Extensions.GitHub;
public static class GitHubReleaseDigests
{
    public static IReadOnlyDictionary<string, string> Parse(string releaseJson);          // asset name → lowercase hex, assets with a sha256 digest only
    public static IReadOnlyDictionary<string, string> Read(ICakeContext context, string repository, string tag); // gh api repos/<repository>/releases/tags/<tag>
}
```

- [ ] **Step 1: Write the failing tests**

```csharp
using Build.Cake.Extensions.Downloads;

namespace Build.Cake.Extensions.Tests.Downloads;

public class PinnedAssetsTests
{
    private const string Hash = "a6d077d0a95eed5a2eba715b2dae06be954f624ccbf85758a03f389ded33118c";

    [Test]
    public async Task Round_trips_with_keys_in_ordinal_order()
    {
        var assets = new PinnedAssets("0.15.1", new Dictionary<string, PinnedAsset>
        {
            ["win-x64"] = new("typst-x86_64-pc-windows-msvc.zip", Hash),
            ["linux-x64"] = new("typst-x86_64-unknown-linux-musl.tar.xz", Hash),
        });

        var json = assets.ToJson();

        await Assert.That(json).IsEqualTo($$"""
            {
              "version": "0.15.1",
              "assets": {
                "linux-x64": {
                  "name": "typst-x86_64-unknown-linux-musl.tar.xz",
                  "sha256": "{{Hash}}"
                },
                "win-x64": {
                  "name": "typst-x86_64-pc-windows-msvc.zip",
                  "sha256": "{{Hash}}"
                }
              }
            }

            """);
        var parsed = PinnedAssets.Parse(json);
        await Assert.That(parsed.Version).IsEqualTo("0.15.1");
        await Assert.That(parsed.Assets["win-x64"]).IsEqualTo(new PinnedAsset("typst-x86_64-pc-windows-msvc.zip", Hash));
    }

    [Test]
    [Arguments("""{ "assets": {} }""", "\"version\"")]
    [Arguments("""{ "version": "1", "assets": { "a": { "name": "x.zip", "sha256": "ABC" } } }""", "64 lowercase hex")]
    [Arguments("""{ "version": "1", "assets": { "a": { "name": "dir/x.zip", "sha256": "0000000000000000000000000000000000000000000000000000000000000000" } } }""", "file name")]
    [Arguments("""{ "version": "1" }""", "\"assets\"")]
    public async Task Rejects_lock_files_it_cant_trust(string json, string mention)
    {
        var exception = Assert.Throws<InvalidDataException>(() => PinnedAssets.Parse(json));

        await Assert.That(exception.Message).Contains(mention);
    }
}
```

```csharp
using Build.Cake.Extensions.GitHub;

namespace Build.Cake.Extensions.Tests.GitHub;

public class GitHubReleaseDigestsTests
{
    [Test]
    public async Task Reads_sha256_digests_by_asset_name_and_skips_assets_without_one()
    {
        const string release = """
            { "tag_name": "v0.15.1", "assets": [
              { "name": "typst-x86_64-pc-windows-msvc.zip", "digest": "sha256:19CE3551153C2FE7EE9FA2F95208310C8F4D3209FEDB699E0333FAF8913F6736" },
              { "name": "old.zip", "digest": null },
              { "name": "md5.zip", "digest": "md5:abc" }
            ] }
            """;

        var digests = GitHubReleaseDigests.Parse(release);

        await Assert.That(digests.Count).IsEqualTo(1);
        await Assert.That(digests["typst-x86_64-pc-windows-msvc.zip"]).IsEqualTo("19ce3551153c2fe7ee9fa2f95208310c8f4d3209fedb699e0333faf8913f6736");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test build/Build.Cake.Extensions.Tests -- --treenode-filter "/*/*/PinnedAssetsTests/*"`
Expected: build error, the types don't exist.

- [ ] **Step 3: Write the implementation**

```csharp
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Build.Cake.Extensions.Downloads;

// One file to download, pinned by its SHA-256 (lowercase hex).
public sealed record PinnedAsset(string Name, string Sha256);

// A committed lock file: the assets a build downloads, by key (for example a runtime identifier), with the hashes that
// were reviewed. Only an update task rewrites it; downloads verify against it.
public sealed partial record PinnedAssets(string Version, IReadOnlyDictionary<string, PinnedAsset> Assets)
{
    public static PinnedAssets Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var version = root.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } text
            ? text
            : throw new InvalidDataException("The lock file needs a non-empty \"version\" string.");
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("The lock file needs an \"assets\" object.");
        }

        var result = new SortedDictionary<string, PinnedAsset>(StringComparer.Ordinal);
        foreach (var asset in assets.EnumerateObject())
        {
            var name = asset.Value.GetProperty("name").GetString() ?? "";
            var sha256 = asset.Value.GetProperty("sha256").GetString() ?? "";
            if (name.Length == 0 || name.IndexOfAny(['/', '\\']) >= 0)
            {
                throw new InvalidDataException($"Asset '{asset.Name}' needs a plain file name, not '{name}'.");
            }

            if (!Sha256Pattern().IsMatch(sha256))
            {
                throw new InvalidDataException($"Asset '{asset.Name}' needs a sha256 of 64 lowercase hex digits, not '{sha256}'.");
            }

            result.Add(asset.Name, new PinnedAsset(name, sha256));
        }

        return new PinnedAssets(version, result);
    }

    public string ToJson()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, IndentSize = 2, NewLine = "\n" }))
        {
            writer.WriteStartObject();
            writer.WriteString("version", Version);
            writer.WriteStartObject("assets");
            foreach (var (key, asset) in Assets.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                writer.WriteStartObject(key);
                writer.WriteString("name", asset.Name);
                writer.WriteString("sha256", asset.Sha256);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex Sha256Pattern();
}
```

```csharp
using System.Text.Json;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Cake.Extensions.GitHub;

// The SHA-256 GitHub records for each release asset (the release API's "digest", "sha256:<hex>").
public static class GitHubReleaseDigests
{
    public static IReadOnlyDictionary<string, string> Parse(string releaseJson)
    {
        using var document = JsonDocument.Parse(releaseJson);
        var digests = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var asset in document.RootElement.GetProperty("assets").EnumerateArray())
        {
            if (asset.TryGetProperty("digest", out var digest) && digest.ValueKind == JsonValueKind.String
                && digest.GetString() is { } value && value.StartsWith("sha256:", StringComparison.Ordinal))
            {
                digests[asset.GetProperty("name").GetString()!] = value["sha256:".Length..].ToLowerInvariant();
            }
        }

        return digests;
    }

    public static IReadOnlyDictionary<string, string> Read(ICakeContext context, string repository, string tag) =>
        Parse(new GitHubCli(context).Gh(new ProcessArgumentBuilder().Append("api").Append($"repos/{repository}/releases/tags/{tag}")));
}
```

(If `JsonWriterOptions.IndentSize` or `NewLine` doesn't exist in .NET 10's System.Text.Json, write the JSON with a
`StringBuilder` in the same shape. The round-trip test pins that shape.)

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test build/Build.Cake.Extensions.Tests`
Expected: PASS.

- [ ] **Step 5: Write refinement 2 into the spec**

In §8, replace the lock file example's keys with the generic ones: `"version"` instead of `"typst"`, `"assets"` instead of
`"runtimes"`, and `"name"` instead of `"asset"`. Add the sentence "The keys are generic so the lock model can live in the
Typstium-agnostic build library."

- [ ] **Step 6: Commit**

```bash
git add build/Build.Cake.Extensions build/Build.Cake.Extensions.Tests docs/specs/runtime-packages.md
git commit -m "Read and write pinned asset lock files and GitHub release digests"
```

---

### Task 11: `VerifiedDownloader` (build library)

**Files:**
- Create: `build/Build.Cake.Extensions/Downloads/VerifiedDownloader.cs`
- Create: `build/Build.Cake.Extensions.Tests/TempFolder.cs`
- Test: `build/Build.Cake.Extensions.Tests/Downloads/VerifiedDownloaderTests.cs`

**Interfaces:**
- Produces:

```csharp
public sealed class VerifiedDownloader : IDisposable
{
    public VerifiedDownloader(HttpMessageHandler handler, Func<TimeSpan, Task>? delay = null);   // the handler must not follow redirects
    public static VerifiedDownloader CreateDefault();                                            // SocketsHttpHandler { AllowAutoRedirect = false }
    public Task DownloadAsync(Uri url, string destination, string expectedSha256, CancellationToken cancellationToken = default);
}
```

Behavior (ideas from Cake.Download.Module's `HttpDownloader`, same author, MIT):

- **Cache:** if `destination` exists and its SHA-256 matches, return without any request.
- **Download:** stream to `destination + ".partial"` while hashing, then compare. On a match, move it over `destination`.
  On a mismatch, delete the partial file and throw `InvalidDataException` with the message
  `"{url} has SHA-256 {actual}, but {expected} is pinned. Find out why it changed before re-pinning."`
- **Retries:** HTTP 408, 429 and 5xx, `HttpRequestException`, `IOException` and stalls. Three attempts, with delays of
  1 s and then 2 s through `delay` (tests pass a no-op).
- **Stalls:** abort an attempt when no bytes arrive for 30 seconds.
- **Redirects:** followed by hand, up to 5, https only. A non-https redirect target throws `InvalidOperationException`.
- **Headers:** a `User-Agent: Typstium-build` header, and no credentials.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Build.Cake.Extensions.Downloads;

namespace Build.Cake.Extensions.Tests.Downloads;

public class VerifiedDownloaderTests
{
    private static readonly byte[] Content = Encoding.UTF8.GetBytes("typst release asset");

    private static readonly string Hash = Convert.ToHexStringLower(SHA256.HashData(Content));

    private static readonly Uri Url = new("https://github.com/o/r/releases/download/v1/asset.zip");

    // Answers each request with the next response; records the requests.
    private sealed class StubHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] responses) : HttpMessageHandler
    {
        private int _next;

        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(responses[Math.Min(_next++, responses.Length - 1)](request));
        }
    }

    private static HttpResponseMessage Ok() => new(HttpStatusCode.OK) { Content = new ByteArrayContent(Content) };

    private static VerifiedDownloader Downloader(StubHandler handler) => new(handler, _ => Task.CompletedTask);

    [Test]
    public async Task Downloads_and_verifies_the_file()
    {
        using var temp = new TempFolder();
        var handler = new StubHandler(_ => Ok());

        await Downloader(handler).DownloadAsync(Url, temp.Combine("asset.zip"), Hash);

        await Assert.That(File.ReadAllBytes(temp.Combine("asset.zip"))).IsEquivalentTo(Content);
        await Assert.That(File.Exists(temp.Combine("asset.zip.partial"))).IsFalse();
    }

    [Test]
    public async Task A_cached_file_with_the_pinned_hash_needs_no_request()
    {
        using var temp = new TempFolder();
        File.WriteAllBytes(temp.Combine("asset.zip"), Content);
        var handler = new StubHandler(_ => throw new InvalidOperationException("no request expected"));

        await Downloader(handler).DownloadAsync(Url, temp.Combine("asset.zip"), Hash);

        await Assert.That(handler.Requests).IsEmpty();
    }

    [Test]
    public async Task A_hash_mismatch_fails_names_both_hashes_and_leaves_nothing_behind()
    {
        using var temp = new TempFolder();
        var pinned = new string('0', 64);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => Downloader(new StubHandler(_ => Ok())).DownloadAsync(Url, temp.Combine("asset.zip"), pinned));

        await Assert.That(exception!.Message).Contains(Hash);
        await Assert.That(exception.Message).Contains(pinned);
        await Assert.That(Directory.GetFiles(temp.Path)).IsEmpty();
    }

    [Test]
    public async Task Server_errors_are_retried()
    {
        using var temp = new TempFolder();
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway), _ => Ok());

        await Downloader(handler).DownloadAsync(Url, temp.Combine("asset.zip"), Hash);

        await Assert.That(handler.Requests.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Https_redirects_are_followed()
    {
        using var temp = new TempFolder();
        var target = new Uri("https://objects.githubusercontent.com/asset");
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = target } }, _ => Ok());

        await Downloader(handler).DownloadAsync(Url, temp.Combine("asset.zip"), Hash);

        await Assert.That(handler.Requests).IsEquivalentTo(new[] { Url, target }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Redirects_to_plain_http_are_refused()
    {
        using var temp = new TempFolder();
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = new Uri("http://example.com/asset") } });

        await Assert.ThrowsAsync<InvalidOperationException>(() => Downloader(handler).DownloadAsync(Url, temp.Combine("asset.zip"), Hash));
    }
}
```

`TempFolder` is a small helper of the build tests (they stay independent of the product's test library), in
`build/Build.Cake.Extensions.Tests/TempFolder.cs`:

```csharp
namespace Build.Cake.Extensions.Tests;

// A unique folder under the system temp path, deleted on dispose.
internal sealed class TempFolder : IDisposable
{
    public TempFolder()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "typstium-build-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string Combine(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test build/Build.Cake.Extensions.Tests -- --treenode-filter "/*/*/VerifiedDownloaderTests/*"`
Expected: build error, `VerifiedDownloader` doesn't exist.

- [ ] **Step 3: Write the implementation**

```csharp
using System.Net;
using System.Security.Cryptography;

namespace Build.Cake.Extensions.Downloads;

// Downloads a file whose SHA-256 is pinned, over https only, with retries and a stall timeout. A file already at the
// destination with the pinned hash is reused. Ideas from Cake.Download.Module's HttpDownloader (same author, MIT).
public sealed class VerifiedDownloader : IDisposable
{
    private const int MaxAttempts = 3;
    private const int MaxRedirects = 5;
    private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(30);

    private readonly HttpClient _client;
    private readonly Func<TimeSpan, Task> _delay;

    public VerifiedDownloader(HttpMessageHandler handler, Func<TimeSpan, Task>? delay = null)
    {
        _client = new HttpClient(handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
        _delay = delay ?? (wait => Task.Delay(wait));
    }

    public static VerifiedDownloader CreateDefault() => new(new SocketsHttpHandler { AllowAutoRedirect = false });

    public async Task DownloadAsync(Uri url, string destination, string expectedSha256, CancellationToken cancellationToken = default)
    {
        if (File.Exists(destination) && await HashFileAsync(destination, cancellationToken) == expectedSha256)
        {
            return;
        }

        var partial = destination + ".partial";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
        try
        {
            var actual = await DownloadWithRetriesAsync(url, partial, cancellationToken);
            if (actual != expectedSha256)
            {
                throw new InvalidDataException($"{url} has SHA-256 {actual}, but {expectedSha256} is pinned. Find out why it changed before re-pinning.");
            }

            File.Move(partial, destination, overwrite: true);
        }
        finally
        {
            File.Delete(partial);
        }
    }

    public void Dispose() => _client.Dispose();

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
    }

    private async Task<string> DownloadWithRetriesAsync(Uri url, string path, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await DownloadOnceAsync(url, path, cancellationToken);
            }
            catch (Exception exception) when (attempt < MaxAttempts && exception is HttpRequestException or IOException or TimeoutException)
            {
                await _delay(TimeSpan.FromSeconds(attempt));
            }
        }
    }

    private async Task<string> DownloadOnceAsync(Uri url, string path, CancellationToken cancellationToken)
    {
        using var stall = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var response = await SendFollowingRedirectsAsync(url, stall);
        var status = (int)response.StatusCode;
        if (status is 408 or 429 or >= 500)
        {
            throw new HttpRequestException($"{url}: HTTP {status} ({response.ReasonPhrase}).");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Downloading {url} failed: HTTP {status} ({response.ReasonPhrase}).");
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        await using var source = await response.Content.ReadAsStreamAsync(stall.Token);
        await using var target = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        while (true)
        {
            stall.CancelAfter(StallTimeout);
            int read;
            try
            {
                read = await source.ReadAsync(buffer, stall.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"{url}: no data for {StallTimeout.TotalSeconds} s.");
            }

            if (read == 0)
            {
                return Convert.ToHexStringLower(hash.GetHashAndReset());
            }

            hash.AppendData(buffer, 0, read);
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    private async Task<HttpResponseMessage> SendFollowingRedirectsAsync(Uri url, CancellationTokenSource stall)
    {
        var current = url;
        for (var redirects = 0; ; redirects++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            request.Headers.UserAgent.ParseAdd("Typstium-build");
            stall.CancelAfter(StallTimeout);
            var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, stall.Token);
            if ((int)response.StatusCode is not (301 or 302 or 303 or 307 or 308))
            {
                return response;
            }

            var location = response.Headers.Location;
            response.Dispose();
            var next = location is null ? throw new InvalidOperationException($"{current} redirected without a Location header.")
                : location.IsAbsoluteUri ? location : new Uri(current, location);
            if (next.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidOperationException($"{current} redirected to {next}, which isn't https.");
            }

            if (redirects + 1 > MaxRedirects)
            {
                throw new InvalidOperationException($"{url} redirected more than {MaxRedirects} times.");
            }

            current = next;
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test build/Build.Cake.Extensions.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add build/Build.Cake.Extensions build/Build.Cake.Extensions.Tests
git commit -m "Download pinned files over https with retries and verification"
```

---

### Task 12: `ArchiveFiles` (build library)

**Files:**
- Create: `build/Build.Cake.Extensions/Downloads/ArchiveFiles.cs`
- Modify: `build/Build.Cake.Extensions/Build.Cake.Extensions.csproj` (SharpCompress), `Directory.Packages.props`
  (`<PackageVersion Include="SharpCompress" Version="1.0.0" />`, under the build comment)
- Create: `build/Build.Cake.Extensions.Tests/Fixtures/Archives/make-fixtures.py`, `release.tar.xz`, `deep.tar.xz`
- Test: `build/Build.Cake.Extensions.Tests/Downloads/ArchiveFilesTests.cs`

**Interfaces:**
- Produces: `public static class ArchiveFiles { public static void Extract(string archivePath, IReadOnlyDictionary<string, string> destinationsByFileName); }`
  - Works on `.zip` and `.tar.xz`. Any other extension throws `NotSupportedException`.
  - Each file name must match exactly one regular-file entry at the archive root or one folder down. None →
    `InvalidDataException("<archive> has no entry named <name> at its root or one folder down.")`. Two or more →
    `InvalidDataException("<archive> has more than one entry named <name>: a, b.")`.
  - Destinations come from the caller. Nothing in the archive chooses a path.

- [ ] **Step 1: Create the xz fixtures**

`make-fixtures.py` (Python's standard library writes xz; .NET can't):

```python
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
```

Run `python make-fixtures.py` in `build/Build.Cake.Extensions.Tests/Fixtures/Archives/` and commit both `.tar.xz` files.
`Fixtures/**` is already copied to the test output.

- [ ] **Step 2: Write the failing tests**

```csharp
using System.IO.Compression;
using Build.Cake.Extensions.Downloads;

namespace Build.Cake.Extensions.Tests.Downloads;

public class ArchiveFilesTests
{
    private static string Zip(TempFolder temp, params (string Path, string Content)[] entries)
    {
        var path = temp.Combine("asset.zip");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (entryPath, content) in entries)
        {
            using var writer = new StreamWriter(archive.CreateEntry(entryPath).Open());
            writer.Write(content);
        }

        return path;
    }

    [Test]
    public async Task Extracts_named_files_from_one_folder_down_in_a_tar_xz()
    {
        using var temp = new TempFolder();
        var destinations = new Dictionary<string, string> { ["typst"] = temp.Combine("out", "typst"), ["LICENSE"] = temp.Combine("out", "LICENSE") };

        ArchiveFiles.Extract(Fixture.Path("Archives", "release.tar.xz"), destinations);

        await Assert.That(File.ReadAllText(temp.Combine("out", "typst"))).IsEqualTo("#!/bin/sh\necho typst\n");
        await Assert.That(File.ReadAllText(temp.Combine("out", "LICENSE"))).IsEqualTo("license\n");
        await Assert.That(File.Exists(temp.Combine("out", "README.md"))).IsFalse();
    }

    [Test]
    public async Task Extracts_named_files_from_the_root_of_a_zip()
    {
        using var temp = new TempFolder();
        var zip = Zip(temp, ("typst.exe", "exe"), ("LICENSE", "license"));

        ArchiveFiles.Extract(zip, new Dictionary<string, string> { ["typst.exe"] = temp.Combine("typst.exe") });

        await Assert.That(File.ReadAllText(temp.Combine("typst.exe"))).IsEqualTo("exe");
    }

    [Test]
    public async Task Entries_deeper_than_one_folder_are_not_matched()
    {
        using var temp = new TempFolder();

        var exception = Assert.Throws<InvalidDataException>(() =>
            ArchiveFiles.Extract(Fixture.Path("Archives", "deep.tar.xz"), new Dictionary<string, string> { ["typst"] = temp.Combine("typst") }));

        await Assert.That(exception.Message).Contains("no entry named typst");
    }

    [Test]
    public async Task Two_matching_entries_are_an_error()
    {
        using var temp = new TempFolder();
        var zip = Zip(temp, ("a/typst.exe", "1"), ("b/typst.exe", "2"));

        var exception = Assert.Throws<InvalidDataException>(() => ArchiveFiles.Extract(zip, new Dictionary<string, string> { ["typst.exe"] = temp.Combine("typst.exe") }));

        await Assert.That(exception.Message).Contains("more than one entry named typst.exe");
    }

    [Test]
    public async Task Other_formats_are_not_supported()
    {
        using var temp = new TempFolder();
        File.WriteAllText(temp.Combine("asset.7z"), "");

        await Assert.That(() => ArchiveFiles.Extract(temp.Combine("asset.7z"), new Dictionary<string, string>())).Throws<NotSupportedException>();
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test build/Build.Cake.Extensions.Tests -- --treenode-filter "/*/*/ArchiveFilesTests/*"`
Expected: build error, `ArchiveFiles` doesn't exist.

- [ ] **Step 4: Write the implementation**

```csharp
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
            var segments = entryPath.TrimStart('.', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
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
```

`Build.Cake.Extensions.csproj` gains `<PackageReference Include="SharpCompress" />`.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test build/Build.Cake.Extensions.Tests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add Directory.Packages.props build/Build.Cake.Extensions build/Build.Cake.Extensions.Tests
git commit -m "Extract named files from zip and tar.xz release archives"
```

---

### Task 13: The Cake tasks and the lock file

**Files:**
- Create: `build/Build/TypstRuntimes.cs`, `build/Build/Tasks/RuntimeTasks.cs`, `build/typst-runtimes.json`
- Modify: `build/Build/Program.cs` (`RegisterRuntimeTasks();`, and the header comment's list of targets)
- Modify: `build/Build/Build.csproj` (`<Using Include="Build.Cake.Extensions.Downloads" />`)

**Interfaces:**
- Consumes: `PinnedAssets`, `GitHubReleaseDigests` (Task 10), `VerifiedDownloader` (Task 11), `ArchiveFiles` (Task 12),
  the template projects (Task 5).
- Produces:
  - Cake targets `UpdateTypstRuntimes`, `DownloadTypstRuntimes`, `PackTypstRuntimes`, `RuntimeTests`, `RuntimeEndToEndTests`
  - `artifacts/runtimes/<version>/<rid>/{typst[.exe],LICENSE}`
  - `artifacts/packages/Typstium.Runtime.*.nupkg`

- [ ] **Step 1: Write the Typstium values**

```csharp
using System.Runtime.InteropServices;

// The Typst release assets packed into Typstium.Runtime.<rid> (docs/specs/runtime-packages.md §8): which RIDs, which
// Rust target each one's asset is built for, and where the assets live.
public static class TypstRuntimes
{
    public const string LockFile = "build/typst-runtimes.json";

    public const string Repository = "typst/typst";

    public static IReadOnlyDictionary<string, string> RustTargets { get; } = new SortedDictionary<string, string>(StringComparer.Ordinal)
    {
        ["linux-arm"] = "armv7-unknown-linux-musleabi",
        ["linux-arm64"] = "aarch64-unknown-linux-musl",
        ["linux-x64"] = "x86_64-unknown-linux-musl",
        ["osx-arm64"] = "aarch64-apple-darwin",
        ["osx-x64"] = "x86_64-apple-darwin",
        ["win-arm64"] = "aarch64-pc-windows-msvc",
        ["win-x64"] = "x86_64-pc-windows-msvc",
    };

    public static bool IsWindows(string rid) => rid.StartsWith("win-", StringComparison.Ordinal);

    public static string AssetName(string rid) => $"typst-{RustTargets[rid]}.{(IsWindows(rid) ? "zip" : "tar.xz")}";

    public static string ExecutableName(string rid) => IsWindows(rid) ? "typst.exe" : "typst";

    public static Uri AssetUrl(string version, string assetName) => new($"https://github.com/{Repository}/releases/download/v{version}/{assetName}");

    // The machine running the build, named like the packages (the OS architecture, as the locator uses).
    public static string HostRuntimeIdentifier =>
        (OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux") + "-" +
        RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();
}
```

- [ ] **Step 2: Write the tasks**

```csharp
using Cake.Common.Tools.DotNet.MSBuild;

public static partial class Program
{
    // docs/specs/runtime-packages.md §8. Not part of Default: they download about 120 MB.
    static void RegisterRuntimeTasks()
    {
        Task("UpdateTypstRuntimes")
            .Does(() =>
            {
                var version = Argument<string>("typst-version");
                var digests = GitHubReleaseDigests.Read(Context, TypstRuntimes.Repository, "v" + version);
                var assets = new Dictionary<string, PinnedAsset>(StringComparer.Ordinal);
                foreach (var rid in TypstRuntimes.RustTargets.Keys)
                {
                    var name = TypstRuntimes.AssetName(rid);
                    assets[rid] = digests.TryGetValue(name, out var sha256)
                        ? new PinnedAsset(name, sha256)
                        : throw new CakeException($"Typst {version} has no {name} with a SHA-256 digest; {rid} can't be packed.");
                }

                System.IO.File.WriteAllText(TypstRuntimes.LockFile, new PinnedAssets(version, assets).ToJson());
                Information("Pinned Typst {0} in {1}. Review the diff before committing.", version, TypstRuntimes.LockFile);
            });

        Task("DownloadTypstRuntimes")
            .Does(() =>
            {
                var pinned = PinnedAssets.Parse(System.IO.File.ReadAllText(TypstRuntimes.LockFile));
                var missing = TypstRuntimes.RustTargets.Keys.Except(pinned.Assets.Keys).ToArray();
                if (missing.Length > 0)
                {
                    throw new CakeException($"{TypstRuntimes.LockFile} doesn't pin {string.Join(", ", missing)}. Run --target=UpdateTypstRuntimes.");
                }

                using var downloader = VerifiedDownloader.CreateDefault();
                foreach (var (rid, asset) in pinned.Assets)
                {
                    var archive = $"artifacts/cache/typst/{pinned.Version}/{asset.Name}";
                    downloader.DownloadAsync(TypstRuntimes.AssetUrl(pinned.Version, asset.Name), archive, asset.Sha256).GetAwaiter().GetResult();
                    var folder = RuntimeFolder(pinned.Version, rid);
                    var executable = TypstRuntimes.ExecutableName(rid);
                    ArchiveFiles.Extract(archive, new Dictionary<string, string>
                    {
                        [executable] = System.IO.Path.Combine(folder, executable),
                        ["LICENSE"] = System.IO.Path.Combine(folder, "LICENSE"),
                    });
                    Information("{0}: {1} verified and extracted.", rid, asset.Name);
                }

                CheckHostBinary(pinned.Version);
            });

        Task("PackTypstRuntimes")
            .IsDependentOn("DownloadTypstRuntimes")
            .Does(() =>
            {
                var version = PinnedAssets.Parse(System.IO.File.ReadAllText(TypstRuntimes.LockFile)).Version;
                var packages = MakeAbsolute(Directory("artifacts/packages")).FullPath;
                // Restoring Typstium.Runtime.All pulls the fresh packages into a cache of their own, not the user's.
                var environment = new Dictionary<string, string> { ["NUGET_PACKAGES"] = MakeAbsolute(Directory("artifacts/cache/nuget")).FullPath };
                foreach (var rid in TypstRuntimes.RustTargets.Keys)
                {
                    DotNetPack("src/Typstium.Runtime/Typstium.Runtime.csproj", new DotNetPackSettings
                    {
                        Configuration = "Release",
                        OutputDirectory = packages,
                        EnvironmentVariables = environment,
                        MSBuildSettings = new DotNetMSBuildSettings()
                            .WithProperty("TypstiumRuntimeIdentifier", rid)
                            .WithProperty("TypstiumRuntimeSource", MakeAbsolute(Directory(RuntimeFolder(version, rid))).FullPath)
                            .WithProperty("TypstVersion", version),
                    });
                }

                DotNetPack("src/Typstium.Runtime.All/Typstium.Runtime.All.csproj", new DotNetPackSettings
                {
                    Configuration = "Release",
                    OutputDirectory = packages,
                    EnvironmentVariables = environment,
                    MSBuildSettings = new DotNetMSBuildSettings()
                        .WithProperty("TypstVersion", version)
                        .WithProperty("RestoreAdditionalProjectSources", packages),
                });
            });

        // The targets tests with fixture packages (stub binaries); minutes, not seconds.
        Task("RuntimeTests")
            .Does<BuildParameters>(parameters =>
                DotNetTest("tests/Typstium.Runtime.Tests", new DotNetTestSettings { PathType = DotNetTestPathType.Project, Configuration = parameters.Configuration }));

        // Against the real packages in artifacts/packages (run PackTypstRuntimes first, or download CI's artifact there).
        Task("RuntimeEndToEndTests")
            .Does<BuildParameters>(parameters =>
                DotNetTest("tests/Typstium.Runtime.EndToEndTests", new DotNetTestSettings
                {
                    PathType = DotNetTestPathType.Project,
                    Configuration = parameters.Configuration,
                    EnvironmentVariables = { ["TYPSTIUM_RUNTIME_PACKAGES"] = MakeAbsolute(Directory("artifacts/packages")).FullPath },
                }));
    }

    static string RuntimeFolder(string version, string rid) => $"artifacts/runtimes/{version}/{rid}";

    // A lock file that names the wrong release would pack the wrong binary: run this machine's and compare versions.
    static void CheckHostBinary(string version)
    {
        var rid = TypstRuntimes.HostRuntimeIdentifier;
        if (!TypstRuntimes.RustTargets.ContainsKey(rid))
        {
            Warning("No Typst runtime for {0}; skipping the version check.", rid);
            return;
        }

        var executable = System.IO.Path.GetFullPath(System.IO.Path.Combine(RuntimeFolder(version, rid), TypstRuntimes.ExecutableName(rid)));
        if (!OperatingSystem.IsWindows())
        {
            System.IO.File.SetUnixFileMode(executable, System.IO.File.GetUnixFileMode(executable) | UnixFileMode.UserExecute);
        }

        StartProcess(executable, new ProcessSettings { Arguments = "--version", RedirectStandardOutput = true }, out var output);
        var line = output.FirstOrDefault() ?? "";
        if (!line.StartsWith($"typst {version}", StringComparison.Ordinal))
        {
            throw new CakeException($"{executable} --version printed '{line}', but {TypstRuntimes.LockFile} pins {version}.");
        }
    }
}
```

In `Program.cs`, add `RegisterRuntimeTasks();` after `RegisterCoverageTasks();`. Extend the header comment with:
"Runtime packages (not in Default): --target=UpdateTypstRuntimes --typst-version=<v>, DownloadTypstRuntimes,
PackTypstRuntimes, RuntimeTests, RuntimeEndToEndTests."

- [ ] **Step 3: Pin 0.15.1 and check the digests**

Run: `./build.ps1 --target=UpdateTypstRuntimes --typst-version=0.15.1`
Expected: `build/typst-runtimes.json` is written. Check it against these digests (from `gh api repos/typst/typst/releases/tags/v0.15.1`
on Oct 10, 2026). Any difference means stop and ask the user:

| RID | Asset | SHA-256 |
|---|---|---|
| linux-arm | typst-armv7-unknown-linux-musleabi.tar.xz | 44986312e557b9ac0f2c71d5d5156c0ad93b2da374d54c859d6c0c7c0b73709f |
| linux-arm64 | typst-aarch64-unknown-linux-musl.tar.xz | 5aa8d74a3d906e60ea12a66ac2f37f8eef1b14cbad7182a745e393a10c23dcee |
| linux-x64 | typst-x86_64-unknown-linux-musl.tar.xz | a6d077d0a95eed5a2eba715b2dae06be954f624ccbf85758a03f389ded33118c |
| osx-arm64 | typst-aarch64-apple-darwin.tar.xz | 48f62ed034aa3a7978309579ac6ca00045e2ef0da73114e8af27cfd8e74dc05a |
| osx-x64 | typst-x86_64-apple-darwin.tar.xz | 7f9fdd9584866245de9a79e0add8f9236fae6f40a8a45e2c4771ccc14db4e0fa |
| win-arm64 | typst-aarch64-pc-windows-msvc.zip | 4ab28e1b71ec3184d38d580ab797f499b6770d952b6b19167be5cea5c2662e14 |
| win-x64 | typst-x86_64-pc-windows-msvc.zip | 19ce3551153c2fe7ee9fa2f95208310c8f4d3209fedb699e0333faf8913f6736 |

- [ ] **Step 4: Pack and inspect**

Run: `./build.ps1 --target=PackTypstRuntimes`
Expected:
- Eight packages in `artifacts/packages/`: seven of about 14–22 MB, and `Typstium.Runtime.All.0.15.1.nupkg` with no content.
- The log shows the version check passing for `win-x64`.

Then run the same target in WSL (`./build.sh --target=PackTypstRuntimes`) to check the `linux-x64` binary and the
`chmod` path. Open one `.nupkg` (it's a zip) and confirm `tools/typst/linux-x64/typst` is the real binary (~50 MB unpacked).

- [ ] **Step 5: Run the build's own tests and the default build**

Run: `./build.ps1`
Expected: green. The default target is unchanged and doesn't pack runtimes.

- [ ] **Step 6: Commit**

```bash
git add build/Build build/typst-runtimes.json
git commit -m "Pin, download and pack the Typst runtimes with Cake"
```

---

### Task 14: End-to-end tests with the real packages

**Files:**
- Create: `tests/Typstium.Runtime.EndToEndTests/Typstium.Runtime.EndToEndTests.csproj`, `RealPackages.cs`, `EndToEndTests.cs`,
  `ContainerTests.cs`
- Modify: `Typstium.slnx`

**Interfaces:**
- Consumes:
  - `DotNet`, `Scenario` (Task 5)
  - real packages in `$TYPSTIUM_RUNTIME_PACKAGES`
  - `TYPSTIUM_REQUIRE_RUNTIME_PACKAGES=1`, which turns skips into failures (in the style of `TYPSTIUM_REQUIRE_TYPST`)
- Produces: tests only.

`RealPackages` works like `PackageFeed` with two differences:
- It packs only the core package, with a unique test version, into `artifacts/runtime-tests/e2e-<version>/`. Its
  `nuget.config` lists that folder, `$TYPSTIUM_RUNTIME_PACKAGES` and nuget.org.
- `NUGET_PACKAGES` is a **fresh** folder for each session, under `artifacts/runtime-tests/e2e-cache-<version>`.

The runtime package version is read from `$TYPSTIUM_RUNTIME_PACKAGES/Typstium.Runtime.All.*.nupkg`'s file name.

Every test runs the same app (`ProgramSource` in `RealPackages.cs` below), which prints what the locator chose and
whether the result is a PDF.

- [ ] **Step 1: Write the tests**

```csharp
using System.Runtime.InteropServices;
using Typstium.Runtime.Testing;

namespace Typstium.Runtime.EndToEndTests;

public class EndToEndTests
{
    private static string HostRid =>
        (OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux") + "-" + RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();

    [Test]
    [Property(Scenario.Key, "RS1")]
    [Property(Scenario.Key, "RS27")]
    public async Task An_app_referencing_every_runtime_compiles_with_the_native_binary()
    {
        using var app = await RealPackages.CreateAppAsync(referenceAll: true);
        var run = await app.RunAppAsync(publish: null);

        await Assert.That(run.Output).Contains("found-by=BundledRuntime");
        await Assert.That(run.Output).Contains($"typst{Path.DirectorySeparatorChar}{HostRid}{Path.DirectorySeparatorChar}");
        await Assert.That(run.Output).Contains("pdf=True");
    }

    [Test]
    [Property(Scenario.Key, "RS4")]
    public async Task A_single_file_app_compiles_with_the_binary_next_to_it()
    {
        using var app = await RealPackages.CreateAppAsync(runtimes: [HostRid]);
        var run = await app.RunAppAsync(publish: ["-r", HostRid, "--self-contained", "false", "-p:PublishSingleFile=true"]);

        await Assert.That(run.Output).Contains("found-by=BundledRuntime");
        await Assert.That(run.Output).Contains("pdf=True");
    }

    [Test]
    [Property(Scenario.Key, "RS31")]
    public async Task A_custom_directory_is_found_through_the_manifest_from_another_working_directory()
    {
        using var app = await RealPackages.CreateAppAsync(runtimes: [HostRid], properties: new() { ["TypstiumRuntimeDirectory"] = "tools/typst-{rid}" });
        var run = await app.RunAppAsync(publish: null, workingDirectory: Path.GetTempPath());

        await Assert.That(run.Output).Contains("found-by=BundledRuntime");
        await Assert.That(run.Output).Contains($"tools{Path.DirectorySeparatorChar}typst-{HostRid}");
    }

    [Test]
    [Property(Scenario.Key, "RS19")]
    public async Task A_32_bit_net_framework_app_runs_the_x64_binary()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test(".NET Framework runs on Windows only.");
            return;
        }

        using var app = await RealPackages.CreateAppAsync(runtimes: ["win-x64"], targetFramework: "net48",
            properties: new() { ["PlatformTarget"] = "AnyCPU", ["Prefer32Bit"] = "true", ["UseAppHost"] = "true" });
        var run = await app.RunAppAsync(publish: null);

        await Assert.That(run.Output).Contains("process64=False");
        await Assert.That(run.Output).Contains($"typst{Path.DirectorySeparatorChar}win-x64");
        await Assert.That(run.Output).Contains("pdf=True");
    }

    [Test]
    [Property(Scenario.Key, "RS41")]
    public async Task An_arm_machine_runs_the_x64_binary_under_emulation_when_only_it_is_referenced()
    {
        var emulated = HostRid switch { "osx-arm64" => "osx-x64", "win-arm64" => "win-x64", _ => null };
        if (emulated is null)
        {
            Skip.Test("Needs an Arm64 Mac (Rosetta) or Arm64 Windows.");
            return;
        }

        using var app = await RealPackages.CreateAppAsync(runtimes: [emulated]);
        var run = await app.RunAppAsync(publish: null);

        await Assert.That(run.Output).Contains($"typst{Path.DirectorySeparatorChar}{emulated}");
        await Assert.That(run.Output).Contains("pdf=True");
    }
}
```

`ContainerTests.cs` (Linux with Docker only; skip otherwise, or fail under `TYPSTIUM_REQUIRE_RUNTIME_PACKAGES=1` when
Docker is missing on Linux):

```csharp
using Typstium.Runtime.Testing;

namespace Typstium.Runtime.EndToEndTests;

public class ContainerTests
{
    [Test]
    [Property(Scenario.Key, "RS9")]
    [Property(Scenario.Key, "RS11")]
    [Arguments("linux-musl-x64", "mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine")]
    [Arguments("linux-x64", "mcr.microsoft.com/dotnet/runtime-deps:10.0-noble")]
    public async Task A_self_contained_linux_app_compiles_in_a_container(string rid, string image)
    {
        await RealPackages.RequireDockerAsync();
        using var app = await RealPackages.CreateAppAsync(runtimes: ["linux-x64"]);
        await app.PublishAsync("-r", rid, "--self-contained", "true", "-p:UseAppHost=true");

        var run = await DotNet.RunToolAsync("docker", app.Directory, new Dictionary<string, string?>(),
            "run", "--rm", "-v", $"{Path.Combine(app.Directory, "pub")}:/app", image, "/app/App");

        await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
        await Assert.That(run.Output).Contains("found-by=BundledRuntime");
        await Assert.That(run.Output).Contains("pdf=True");
    }

    [Test]
    [Property(Scenario.Key, "RS10")]
    public async Task An_sdk_container_image_compiles()
    {
        await RealPackages.RequireDockerAsync();
        using var app = await RealPackages.CreateAppAsync(runtimes: ["linux-x64"]);
        var archive = Path.Combine(app.Directory, "image.tar.gz");
        await app.PublishAsync("-r", "linux-x64", "-t:PublishContainer", "-p:ContainerRepository=typstium-e2e", "-p:ContainerImageTag=test", $"-p:ContainerArchiveOutputPath={archive}");

        var load = await DotNet.RunToolAsync("docker", app.Directory, new Dictionary<string, string?>(), "load", "-i", archive);
        var run = await DotNet.RunToolAsync("docker", app.Directory, new Dictionary<string, string?>(), "run", "--rm", "typstium-e2e:test");

        await Assert.That(load.ExitCode).IsEqualTo(0).Because(load.Output);
        await Assert.That(run.Output).Contains("found-by=BundledRuntime");
        await Assert.That(run.Output).Contains("pdf=True");
    }
}
```

`RealPackages.cs`:

```csharp
using System.Globalization;
using Typstium.Runtime.Testing;
using Typstium.Testing;
using Typstium.Testing.Fixtures;

namespace Typstium.Runtime.EndToEndTests;

/// <summary>
/// Apps that reference a freshly packed Typstium and the real runtime packages in $TYPSTIUM_RUNTIME_PACKAGES, restored
/// into a fresh cache per session (the real packages keep their version between repacks).
/// </summary>
internal static class RealPackages
{
    private static readonly Lazy<Task<Session>> Current = new(CreateSessionAsync);

    private static bool Required => Environment.GetEnvironmentVariable("TYPSTIUM_REQUIRE_RUNTIME_PACKAGES") == "1";

    public static async Task<App> CreateAppAsync(string[]? runtimes = null, bool referenceAll = false, string targetFramework = "net10.0", Dictionary<string, string>? properties = null)
    {
        var session = await Current.Value;
        var ids = (runtimes ?? []).Select(rid => "Typstium.Runtime." + rid).Concat(referenceAll ? ["Typstium.Runtime.All"] : []);
        var references = string.Concat(ids.Select(id => $"    <PackageReference Include=\"{id}\" Version=\"{session.RuntimeVersion}\" />\n"));
        var allProperties = new Dictionary<string, string>(properties ?? []) { ["OutputType"] = "Exe", ["TargetFramework"] = targetFramework, ["LangVersion"] = "latest", ["ImplicitUsings"] = "enable" };
        var propertyLines = string.Concat(allProperties.Select(property => $"    <{property.Key}>{property.Value}</{property.Key}>\n"));

        var app = new App(session.Environment);
        app.Write("App.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n" + propertyLines + "  </PropertyGroup>\n  <ItemGroup>\n" +
            $"    <PackageReference Include=\"Typstium\" Version=\"{session.Version}\" />\n" + references + "  </ItemGroup>\n</Project>\n");
        app.Write("Program.cs", ProgramSource);
        app.Write("nuget.config", $"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="typstium" value="{session.Feed}" />
                <add key="runtimes" value="{session.RuntimePackages}" />
                <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
              </packageSources>
            </configuration>
            """);
        File.Copy(Path.Combine(RepositoryPaths.Root, "global.json"), Path.Combine(app.Directory, "global.json"));
        return app;
    }

    public static async Task RequireDockerAsync()
    {
        await Current.Value;
        if (!OperatingSystem.IsLinux() || await TryRunAsync("docker", "info") != 0)
        {
            if (Required && OperatingSystem.IsLinux())
            {
                throw new InvalidOperationException("TYPSTIUM_REQUIRE_RUNTIME_PACKAGES=1, but Docker isn't available.");
            }

            Skip.Test("Container tests need Docker on Linux.");
        }
    }

    private static async Task<int> TryRunAsync(string tool, params string[] arguments)
    {
        try
        {
            return (await DotNet.RunToolAsync(tool, Path.GetTempPath(), new Dictionary<string, string?>(), arguments)).ExitCode;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return -1;
        }
    }

    private static async Task<Session> CreateSessionAsync()
    {
        var packages = Environment.GetEnvironmentVariable("TYPSTIUM_RUNTIME_PACKAGES");
        var all = packages is not null && Directory.Exists(packages)
            ? Directory.EnumerateFiles(packages, "Typstium.Runtime.All.*.nupkg").SingleOrDefault()
            : null;
        if (all is null)
        {
            if (Required)
            {
                throw new InvalidOperationException($"TYPSTIUM_REQUIRE_RUNTIME_PACKAGES=1, but '{packages}' holds no Typstium.Runtime.All package.");
            }

            Skip.Test("Set TYPSTIUM_RUNTIME_PACKAGES to a folder with the packed runtime packages (./build.ps1 --target=PackTypstRuntimes).");
        }

        var runtimeVersion = Path.GetFileNameWithoutExtension(all!)["Typstium.Runtime.All.".Length..];
        var version = "0.0.0-e" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
        var feed = Path.Combine(PackageFeed.ArtifactsRoot, "e2e-" + version);
        var environment = new Dictionary<string, string?> { ["NUGET_PACKAGES"] = Path.Combine(PackageFeed.ArtifactsRoot, "e2e-cache-" + version) };
        var pack = await DotNet.RunAsync(RepositoryPaths.Root, environment,
            "pack", Path.Combine("src", "Typstium", "Typstium.csproj"), "-c", "Release", "-o", feed, "-p:Version=" + version, "-nodeReuse:false");
        if (pack.ExitCode != 0)
        {
            throw new InvalidOperationException("dotnet pack src/Typstium failed:\n" + pack.Output);
        }

        return new Session(feed, version, packages!, runtimeVersion, environment);
    }

    private sealed record Session(string Feed, string Version, string RuntimePackages, string RuntimeVersion, IReadOnlyDictionary<string, string?> Environment);

    private const string ProgramSource = """
        using Typstium;

        var compiler = TypstCompiler.CreateDefault();
        var result = (await compiler.CompileAsync(TypstSource.Markup("= Hello from Typstium"), TypstOutput.Pdf())).EnsureSuccess();
        var pdf = ((TypstDocument.SingleFile)result.Document!).Content;
        Console.WriteLine($"found-by={result.Invocation.ExecutableFoundBy}");
        Console.WriteLine($"path={result.Invocation.ExecutablePath}");
        Console.WriteLine($"pdf={(pdf.Length > 4 && pdf.Span[0] == (byte)'%' && pdf.Span[1] == (byte)'P')}");
        Console.WriteLine($"process64={Environment.Is64BitProcess}");

        """;
}

/// <summary>One generated app in a temp folder.</summary>
internal sealed class App(IReadOnlyDictionary<string, string?> environment) : IDisposable
{
    private readonly TemporaryDirectory _temp = new("e2e");

    public string Directory => _temp.Path;

    public void Write(string relativePath, string content) => _temp.CreateFile(relativePath, content);

    public async Task PublishAsync(params string[] arguments)
    {
        var run = await DotNet.RunAsync(Directory, environment, ["publish", "-c", "Release", "-o", "pub", "-nodeReuse:false", .. arguments]);
        if (run.ExitCode != 0)
        {
            throw new InvalidOperationException("dotnet publish failed:\n" + run.Output);
        }
    }

    /// <summary>Builds (or publishes with <paramref name="publish"/>) and runs the app; throws on a non-zero exit.</summary>
    public async Task<DotNetRun> RunAppAsync(string[]? publish, string? workingDirectory = null)
    {
        string folder;
        if (publish is null)
        {
            var build = await DotNet.RunAsync(Directory, environment, "build", "-c", "Release", "-o", "out", "-nodeReuse:false");
            if (build.ExitCode != 0)
            {
                throw new InvalidOperationException("dotnet build failed:\n" + build.Output);
            }

            folder = Path.Combine(Directory, "out");
        }
        else
        {
            await PublishAsync(publish);
            folder = Path.Combine(Directory, "pub");
        }

        // The apphost where there is one (net48, single-file, UseAppHost), otherwise dotnet App.dll.
        var apphost = Path.Combine(folder, OperatingSystem.IsWindows() ? "App.exe" : "App");
        var run = File.Exists(apphost)
            ? await DotNet.RunToolAsync(apphost, workingDirectory ?? folder, new Dictionary<string, string?>())
            : await DotNet.RunAsync(workingDirectory ?? folder, new Dictionary<string, string?>(), Path.Combine(folder, "App.dll"));
        if (run.ExitCode != 0)
        {
            throw new InvalidOperationException("The app failed:\n" + run.Output);
        }

        return run;
    }

    public void Dispose() => _temp.Dispose();
}
```

`Typstium.Runtime.EndToEndTests.csproj` is the same as `Typstium.Runtime.Tests.csproj` (Task 5). Its own
`AssemblyInfo.cs` limits parallelism to two (`[assembly: ParallelLimiter<TwoAtATime>]` with `Limit => 2`), because
each test restores real packages of about 20 MB.

- [ ] **Step 2: Run against the packages from Task 13**

Run: `./build.ps1 --target=RuntimeEndToEndTests`, and in WSL `./build.sh --target=RuntimeEndToEndTests`
Expected:
- Windows: RS1, RS4, RS31 and RS19 pass. Containers and RS41 skip.
- WSL with Docker: the container tests also pass.

- [ ] **Step 3: Commit**

```bash
git add tests/Typstium.Runtime.EndToEndTests Typstium.slnx
git commit -m "Compile with the real runtime packages end to end"
```

---

### Task 15: CI, the catalog check and the docs

**Files:**
- Modify: `.github/workflows/ci.yml`
- Create: `tests/Typstium.Runtime.Tests/ScenarioCatalogTests.cs`
- Create: `docs/runtime-packages-checklist.md`
- Modify: `docs/specs/runtime-packages.md` (status → "Implemented"; refinement 5 in §10), `docs/design.md` (status column)

**Interfaces:**
- Consumes: everything above.

- [ ] **Step 1: Write the catalog test** (fails until every scenario verified by U, T or E has a tagged test)

```csharp
using System.Text.RegularExpressions;
using Typstium.Testing;

namespace Typstium.Runtime.Tests;

public partial class ScenarioCatalogTests
{
    // §9's rows: | RSn | scenario | expected | tier | verified |
    [GeneratedRegex(@"^\| (RS\d+) \|.*\| ([^|]+) \|\s*$", RegexOptions.Multiline)]
    private static partial Regex Row();

    [GeneratedRegex(@"\[Property\((?:Scenario\.Key|""Scenario""), ""(RS\d+)""\)\]")]
    private static partial Regex Tag();

    [Test]
    public async Task Every_scenario_verified_by_tests_has_a_tagged_test()
    {
        var spec = File.ReadAllText(Path.Combine(RepositoryPaths.Root, "docs", "specs", "runtime-packages.md"));
        var needed = Row().Matches(spec)
            .Where(match => match.Groups[2].Value.Split(',', StringSplitOptions.TrimEntries).Any(kind => kind is "U" or "T" or "E"))
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
        var tagged = Directory.EnumerateFiles(Path.Combine(RepositoryPaths.Root, "tests"), "*.cs", SearchOption.AllDirectories)
            .SelectMany(file => Tag().Matches(File.ReadAllText(file)).Select(match => match.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);

        await Assert.That(needed.Except(tagged).Order(StringComparer.Ordinal).ToArray()).IsEmpty();
        await Assert.That(needed.Count).IsGreaterThan(20);
    }
}
```

Run it: `dotnet test tests/Typstium.Runtime.Tests -- --treenode-filter "/*/*/ScenarioCatalogTests/*"`.
For each missing ID, either add the tag to the test that covers it, or, if no test does, correct the spec row's
"Verified" column. Never add an empty test.

- [ ] **Step 2: Add the CI jobs** (append to `.github/workflows/ci.yml`, reusing the pinned action SHAs already in the file)

```yaml
  # The runtime package targets against fixture packages (stub binaries), on every OS the targets run on.
  runtime-targets:
    name: Runtime targets / ${{ matrix.os }}
    strategy:
      fail-fast: false
      matrix:
        os: [windows-latest, ubuntu-latest, macos-latest]
    runs-on: ${{ matrix.os }}
    env:
      DOTNET_NOLOGO: 'true'
    steps:
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
      - uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0
        with:
          global-json-file: global.json
      - if: runner.os == 'Windows'
        run: ./build.ps1 --target=RuntimeTests
        shell: pwsh
      - if: runner.os != 'Windows'
        run: ./build.sh --target=RuntimeTests
        shell: bash

  # Downloads, verifies and packs the real runtime packages once; the end-to-end jobs use them.
  pack:
    name: Pack runtime packages
    runs-on: ubuntu-latest
    env:
      DOTNET_NOLOGO: 'true'
    steps:
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
      - uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0
        with:
          global-json-file: global.json
      - uses: actions/cache@<pin the current v4/v5 SHA, with the version in a comment, as for the other actions>
        with:
          path: artifacts/cache/typst
          key: typst-assets-${{ hashFiles('build/typst-runtimes.json') }}
      - run: ./build.sh --target=PackTypstRuntimes
        shell: bash
      - uses: actions/upload-artifact@cf430e030ddbb5b0abf93d22962f4752f3646cd9 # v7.0.2
        with:
          name: runtime-packages
          path: artifacts/packages/Typstium.Runtime.*.nupkg
          if-no-files-found: error
          retention-days: 7

  runtime-e2e:
    name: Runtime end to end / ${{ matrix.os }}
    needs: pack
    strategy:
      fail-fast: false
      matrix:
        os: [windows-latest, ubuntu-latest, macos-latest]
    runs-on: ${{ matrix.os }}
    env:
      DOTNET_NOLOGO: 'true'
      TYPSTIUM_REQUIRE_RUNTIME_PACKAGES: '1'
    steps:
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
      - uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0
        with:
          global-json-file: global.json
      - uses: actions/download-artifact@9000827ccba6bdab643e8b6fd33ac0654aef8333 # v8.0.2
        with:
          name: runtime-packages
          path: artifacts/packages
      - if: runner.os == 'Windows'
        run: ./build.ps1 --target=RuntimeEndToEndTests
        shell: pwsh
      - if: runner.os != 'Windows'
        run: ./build.sh --target=RuntimeEndToEndTests
        shell: bash

  # Windows on Arm runners are free for public repositories only (spec runtime-packages §10).
  runtime-e2e-windows-arm:
    name: Runtime end to end / windows-11-arm
    if: github.event.repository.private == false
    needs: pack
    runs-on: windows-11-arm
    env:
      DOTNET_NOLOGO: 'true'
      TYPSTIUM_REQUIRE_RUNTIME_PACKAGES: '1'
    steps:
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
      - uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0
        with:
          global-json-file: global.json
      - uses: actions/download-artifact@9000827ccba6bdab643e8b6fd33ac0654aef8333 # v8.0.2
        with:
          name: runtime-packages
          path: artifacts/packages
      - run: ./build.ps1 --target=RuntimeEndToEndTests
        shell: pwsh
```

The `actions/cache` SHA: look it up with `gh api repos/actions/cache/releases/latest --jq .tag_name`, then
`gh api repos/actions/cache/git/ref/tags/<tag> --jq .object.sha`. Pin it with `# vX.Y.Z`. Dependabot's
`github-actions` group keeps it current.

- [ ] **Step 3: Write the manual checklist**

`docs/runtime-packages-checklist.md`:

```markdown
# Runtime packages: manual checks

The scenarios in [the runtime packages spec](specs/runtime-packages.md#9-scenario-catalog) that CI can't check yet.
Record each run with a date, the Typst and Typstium versions, and the result.

| ID | Check | How | Last run | Result |
|---|---|---|---|---|
| RS12 | Built on Windows without a RID, copied into a Linux image with a read-only root filesystem | `docker run --read-only`; expect a clear "permission denied" start failure, then fix with `chmod` in the Dockerfile | — | — |
| RS14 | Azure App Service (Windows and Linux) and Azure Functions (isolated worker) | Deploy the end-to-end sample app; call an endpoint that compiles | — | — |
| RS15 | ASP.NET Core on modern .NET under IIS (in-process and out-of-process) | Publish to a local IIS site | — | — |
| RS16 | ASP.NET Core 2.x on .NET Framework under IIS | SDK-style net48 web app with Microsoft.AspNetCore 2.3 packages; found through bin/ | — | — |
| RS17 | Classic WebDeploy package (msdeploy zip), beyond the FileSystem publish CI tests | `msbuild -t:Package`; check bin\typst\ in the zip; deploy to IIS | — | — |
| RS36 | macOS Gatekeeper | Download a published app as a zip through a browser (quarantine flag), unzip, run | — | — |
| RS41 (Windows) | Windows on Arm, while the repository is private | `./build.ps1 --target=RuntimeEndToEndTests` on an Arm64 Windows machine | — | — |
```

- [ ] **Step 4: Update the spec and the design overview**

- Spec status line → `**Status:** Implemented · <date> · Magnus Lindhe`. Add refinement 5 to §10's end-to-end list:
  "`windows-11-arm` runs only while the repository is public; until then the Windows half of RS41 is in the manual
  checklist." Link the checklist from §10's "Manual checklist" heading.
- In `docs/design.md`'s spec table, change Runtime packages' status from "Proposed" to "Implemented".

- [ ] **Step 5: Run everything locally**

Run: `./build.ps1`, `./build.ps1 --target=RuntimeTests`, `./build.ps1 --target=RuntimeEndToEndTests`, and the same three in
WSL with `./build.sh`.
Expected: all green, with skips only where the platform gates them.

- [ ] **Step 6: Commit**

```bash
git add .github/workflows/ci.yml tests/Typstium.Runtime.Tests docs
git commit -m "Run the runtime package tests in CI and check the scenario catalog"
```

---

### Task 16: Verify in CI with a pull request

- [ ] **Step 1: Ask the user before pushing.** With approval, push `feature/runtime-packages` and open a PR with a
  summary of what changed and a pointer to the spec. End the description with
  `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.
- [ ] **Step 2: Watch the run.** All jobs must be green:
  - `build` (2), `coverage`, `latest-typst` (advisory)
  - `runtime-targets` (3), `pack`, `runtime-e2e` (3)
  - `runtime-e2e-windows-arm` is skipped while the repository is private
- [ ] **Step 3: Fix and report.** Fix any failure on the branch with a focused commit. Report the outcome to the user,
  including any test that was skipped on a runner where it was expected to run.
