# Cake: the `Cake.Typstium` addin

**Status:** Draft · Oct 10, 2026 · Magnus Lindhe
**Part of:** [Typstium design overview](../design.md)
**Builds on:** [Foundation](foundation.md) (the core API and the locator chain),
[Runtime packages](runtime-packages.md) (Typst through NuGet, `tools/typst/<rid>/`)

## 1. Purpose and scope

A Cake build should turn a `.typ` file into a PDF (or PNG or SVG pages) with one alias call, using the core
`Typstium` API, with Typst coming from a `#tool` runtime package or from anywhere else the core's locator
looks. This is use case A of the design overview, the build pipeline.

**In scope**

- The `Cake.Typstium` package: aliases for Cake script, Frosting and Cake.Sdk.
- Plain `.typ` sources compiled in place (`TypstSource.File`).
- Finding Typst in Cake's tools folder, in front of the rest of the core's locator chain.
- Unit, integration and end-to-end tests, and a user guide.

**Out of scope**

- **Markdown through `@preview/cmarker`.** It comes later, once every integration exists, and is made to work for
  all of them at once (the planned Markdown spec; see the [design overview](../design.md)). Nothing here prevents a script from compiling a
  `.typ` layout that uses cmarker today; v1 just doesn't test, document or vendor it.
- Template and markup sources, virtual files and batches through aliases. `TypstCompiler()` hands out the core
  compiler for those.
- Installing Typst. The addin finds Typst; how it gets onto the machine is the script's or the CI's choice (section
  6).
- A classic `Tool<TSettings>` addin over the `typst` CLI (`Cake.Typst`), deferred (D3).
- Publishing to nuget.org, with the other packages.

**Success:** a cross-platform Cake script compiles a `.typ` file to a PDF in one alias call. Typst comes from
`#tool`, a failed compile fails the task with canonical diagnostics in the log, and the run needs no installed Typst
and no network.

## 2. Decisions

| # | Decision | Choice | Rejected | Why |
|---|---|---|---|---|
| D1 | Alias surface | Cake-style aliases (`TypstCompile` with `FilePath`s and a settings class) plus `TypstCompiler()` returning a core `ITypstCompiler` | Only `TypstCompiler()`; a `Tool<TSettings>` over Cake's process runner | Idiomatic in scripts, while the escape hatch reaches the whole core API. A `Tool<TSettings>` would bypass the core (containment, diagnostics, limits, version profiles), against the design's rule that integrations sit on the core API |
| D2 | First generation's sources | Plain `.typ` files only | Markdown through cmarker in v1 | Markdown support is made to work for all integrations together, later |
| D3 | `Cake.Typst`, a thin `Tool<TSettings>` wrapper of the `typst` CLI | Deferred, and outside this repository if it's ever built | Build it now, here | Loaded with Cake.Typstium, both would define `TypstCompile` and Cake fails on duplicate aliases. The name reads like the Typst team's own addin (the naming rule and trademark question in the design overview). It would have no core and follow Typst's release cadence. Its escape hatches already exist here: `ExtraArguments` and `ArgumentCustomization` |
| D4 | Several platforms in one script | One `#tool` line per RID the script runs on; the addin picks the host's | `#tool nuget:?package=Typstium.Runtime.All`; an install alias | `#tool` installs no dependencies, so the meta package brings nothing. Every Cake tool with per-platform binaries has this problem, and the locator chain offers other ways to provide Typst (section 6) |
| D5 | Where the Cake tools folder sits in the chain | `ToolPath` → `TYPST_PATH` → Cake tools → bundled runtime → WinGet → well-known locations → `PATH` | Cake tools before `TYPST_PATH`; Cake tools only | `TYPST_PATH` stays the operator's override everywhere in Typstium. A `#tool` package plays the role a runtime package plays in an app, so it takes the bundled runtime's place |
| D6 | Finding the binary in the tools folder | Own locator: `Typstium.Runtime.*/tools/typst/<rid>/typst[.exe]` for the host RID and its emulation fallback | `context.Tools.Resolve("typst")` | With linux-x64 and linux-arm64 both installed, both files are named `typst` and Cake's registry would pick one arbitrarily |
| D7 | Host RID logic | Duplicated in the addin (about 20 lines) | Public `RuntimeIdentifierCandidates` in the core; `InternalsVisibleTo` | A public type would be permanent core API for one consumer. `InternalsVisibleTo` across separately versioned packages breaks on version skew. Duplication is cheaper than the wrong abstraction (foundation principle 11) |
| D8 | Sync or async | Both: `TypstCompile` blocks on `TypstCompileAsync` | Async only; sync only | Most Cake aliases are sync, and `.Does(() => TypstCompileAsync(...))` drops the task silently in a script. Async allows parallel compiles. Blocking is the addin's single exception to foundation principle 9; it is safe because Cake runs with no synchronization context |
| D9 | Loading the core assembly | A normal NuGet dependency on `Typstium`; scripts use `&loaddependencies=true` | Bundle `Typstium.dll` in the addin's `lib/`; both | One copy of the core per process. Frosting and Cake.Sdk projects get the core's `buildTransitive` targets and may reference `Typstium` themselves without a duplicate. The core's `net8.0` build has no runtime dependencies, so `loaddependencies` adds exactly one assembly. A missing directive can't be caught by the addin (section 4.4), so the docs lead with it |
| D10 | Failure | `Failed`, `TimedOut` and `OutputLimitExceeded` throw `CakeException` after every diagnostic is logged; core exceptions pass through | Return the result and let the script check it | A failed document fails the task the Cake way, and `ContinueOnError`/`OnError` work as usual. The locator's "searched these places" message reaches the log intact |
| D11 | Return value | `TypstCompileResult` from both the sync and the async aliases | `void` | Scripts can read `Invocation` (Typst version, duration) or count warnings; most ignore it |
| D12 | Warnings | Logged; `TreatWarningsAsErrors` (default `false`) makes them fail the task | Always fail; never fail | The MSBuild convention |
| D13 | CI annotations (GitHub Actions, Azure Pipelines) | Not in v1 | Emit them through Cake's build system APIs | Canonical log lines already work with problem matchers; can be added without breaking anything |
| D14 | Settings type | Mutable `TypstCompileSettings` with fluent `With…` extensions, mapped to the core's immutable request and options | Derive from Cake's `ToolSettings`; expose the core records directly | The Cake idiom. Most of `ToolSettings` would not apply (`HandleExitCode`, `Silent`, `SetupProcessSettings`, a `ProcessArgumentBuilder` customization), and everything inherited would be supported forever |
| D15 | Defaults | Exactly `TypstCompileOptions.Default`: no system fonts, no network, 60 s | Build-friendly defaults (system fonts on, network allowed) | The defaults are about reproducibility, not trust, and CI is where identical PDFs across machines matter. Missing fonts and packages already get hints that name the fix |
| D16 | Compiler lifetime | A new `CliTypstCompiler` per alias call | A cache per context or per process | No static state (foundation principle 2). The cost is a resolver scan and a `typst info` probe per call, tens of milliseconds. Batches keep one compiler from `TypstCompiler()` |
| D17 | Target framework | `net8.0` | `net8.0;net9.0;net10.0` | Cake.Core 6.3.0 ships all three; a `net8.0` addin runs on each, and the core has no `net9.0`/`net10.0` build to match |
| D18 | Core types in scripts | `[CakeNamespaceImport("Typstium")]` | Re-declare settings types for every core concept | `PdfStandard`, `TypstPageSelection` and `TypstCompileResult` are usable without a `using`, and stay one concept with one name |

## 3. Package

`Cake.Typstium`:

- Targets `net8.0` (D17).
- Depends on `Typstium` at a version range starting at the core version it was built with (`net8.0` build).
- References `Cake.Core` 6.0.0, the first Cake 6, with `PrivateAssets="all"`, the standard addin pattern: Cake provides
  it at run time, and Cake warns about addins built against a newer Cake.Core than the one running.
- Package tags include `cake-addin` (how Cake's addin discovery finds addins), an icon and a README.
- Tracks its public API with `PublicApiAnalyzers`, like the core.

Public types keep the core's naming rule: every public type carries the `Typst` prefix, and only the namespace
(`Cake.Typstium`) and the package ID carry the brand.

### Repository layout

```
src/Cake.Typstium/
  TypstAliases.cs                      [CakeMethodAlias] methods, each a thin call into the runner
  TypstCompileSettings.cs
  TypstCompileSettingsExtensions.cs    fluent With… methods
  Compilation/                         internal: settings → request mapping, the compile-and-report runner
  Executable/CakeToolsLocator.cs       internal ITypstExecutableLocator, plus the host RID logic (D7)
tests/Cake.Typstium.UnitTests/
tests/Cake.Typstium.IntegrationTests/  real Typst; skip or fail per TYPSTIUM_REQUIRE_TYPST
tests/Cake.Typstium.EndToEndTests/     real Cake runs against packed packages
docs/cake.md                           user guide
```

## 4. Public API

### 4.1 Aliases

`TypstAliases`, a static class with `[CakeAliasCategory("Typst")]`. Overloads follow foundation D36: no optional
parameters, and each async shape is a pair, without and with a `CancellationToken`.

```csharp
[CakeNamespaceImport("Typstium")]
public static class TypstAliases
{
    // The output defaults to the input's path with a .pdf extension, as `typst compile in.typ` does.
    public static TypstCompileResult TypstCompile(this ICakeContext context, FilePath input);
    public static TypstCompileResult TypstCompile(this ICakeContext context, FilePath input, FilePath output);
    public static TypstCompileResult TypstCompile(this ICakeContext context, FilePath input, FilePath output,
                                                  TypstCompileSettings settings);

    public static Task<TypstCompileResult> TypstCompileAsync(this ICakeContext context, FilePath input);
    public static Task<TypstCompileResult> TypstCompileAsync(this ICakeContext context, FilePath input,
                                                             CancellationToken cancellationToken);
    // … the same pair for (input, output) and (input, output, settings)

    // A compiler wired to the Cake resolution chain (section 5), for markup and template sources, virtual files,
    // PNG pages in memory, and batches that share one compiler's MaxConcurrency and cached profile.
    public static ITypstCompiler TypstCompiler(this ICakeContext context);
    public static ITypstCompiler TypstCompiler(this ICakeContext context, FilePath toolPath);
}
```

### 4.2 Settings

```csharp
public sealed class TypstCompileSettings
{
    public FilePath? ToolPath { get; set; }                         // explicit executable; first in the chain
    public DirectoryPath? Root { get; set; }                        // null = the input file's folder
    public TypstOutputFormat? Format { get; set; }                  // null = inferred from the output extension
    public IList<PdfStandard> PdfStandards { get; }                 // empty = plain PDF
    public int? Ppi { get; set; }                                   // PNG; null = 144
    public TypstPageSelection? Pages { get; set; }                  // PNG and SVG; null = all pages
    public IDictionary<string, string> Inputs { get; }              // --input key=value (sys.inputs)
    public IList<DirectoryPath> FontPaths { get; }
    public bool IncludeSystemFonts { get; set; }                    // default false
    public DirectoryPath? PackagePath { get; set; }                 // vendored packages
    public DirectoryPath? PackageCachePath { get; set; }
    public bool AllowNetwork { get; set; }                          // default false
    public DateTimeOffset? CreationTimestamp { get; set; }          // null = inherit SOURCE_DATE_EPOCH
    public TimeSpan? Timeout { get; set; }                          // null = the core default (60 s)
    public bool TreatWarningsAsErrors { get; set; }                 // default false
    public TypstArguments ExtraArguments { get; set; }              // after the generated arguments
    public Func<TypstArguments, TypstArguments>? ArgumentCustomization { get; set; }
}
```

`TypstCompileSettingsExtensions` has one fluent method per setting, each returning the settings: `WithToolPath`,
`WithRoot`, `WithFormat`, `WithPdfStandard` (adds), `WithPpi`, `WithPages`, `WithInput(key, value)` (adds or
replaces), `WithFontPath` (adds), `WithSystemFonts`, `WithPackagePath`, `WithPackageCachePath`, `WithNetwork`,
`WithCreationTimestamp`, `WithTimeout`, `WithWarningsAsErrors`, `WithExtraArguments`, `WithArgumentCustomization`.

**Mapping** onto the core, starting from `TypstCompileOptions.Default` (D15):

| Setting | Core |
|---|---|
| `Root` | `TypstSource.File(input, root)` |
| `Format`, `PdfStandards`, `Ppi`, `Pages` | `TypstOutput.Pdf(...)`, `Png(ppi, pages)`, `Svg(pages)`, `Html()` |
| `Inputs` | `TypstCompileRequest.Inputs` (redacted in logs, as always) |
| `FontPaths`, `IncludeSystemFonts` | `Fonts.FontPaths`, `Fonts.IncludeSystemFonts` |
| `PackagePath`, `PackageCachePath`, `AllowNetwork` | `Packages` (`NetworkAccess.Allowed` when true) |
| `CreationTimestamp` | `TypstCreationTimestamp.Fixed(...)`, or `Inherit` when null |
| `Timeout` | `Timeout` |
| `ExtraArguments`, `ArgumentCustomization` | the same-named options |

Every relative `FilePath` and `DirectoryPath` is made absolute against Cake's working directory
(`MakeAbsolute(context.Environment)`), as other Cake aliases do.

Deliberately not exposed (reachable through `TypstCompiler()`): `Jobs`, process priority, the memory limit,
`MaxOutputBytes`, embedded-font switching and `ExtraGlobalArguments`.

### 4.3 Behavior of one call

An internal `TypstCompileRunner`, constructed with the context and a factory for the compiler (the seam for unit
tests):

1. **Validate before any I/O.**
   - `input` and `output` are made absolute.
   - The format comes from `Format`, or from the output's extension: `.pdf`, `.png`, `.svg`, `.html`
     (case-insensitive). An unknown extension with no `Format` throws `ArgumentException`.
2. **Map** the settings to a `TypstCompileRequest` (4.2).
3. **Compile** with a new compiler (D16).
4. **Report.** Each diagnostic goes to `Log.Error` or `Log.Warning` as its `ToString()`, the canonical
   `path(line,col): error TYPST1001: message`. Hints and trace frames follow at `Verbose`, indented.
5. **Decide.**
   - Any status other than `Succeeded` → `CakeException`: `Typst failed to compile <input>: <status>, <n> errors.`
   - `Succeeded` with warnings and `TreatWarningsAsErrors` → `CakeException` naming the warning count.
6. **Save.** The output folder is created if needed.
   - A single-file document is written to `output`.
   - A paged document is saved with `output` as the page pattern (`{p}`, `{0p}`, `{t}`). A paged document with more
     than one page and no `{p}` or `{0p}` in the path throws `ArgumentException` naming the page count; nothing is
     written.
7. **Log** one `Information` line: the output path, the page count for paged output, and the duration. **Return**
   the result.

`TypstException`s (for example `TypstExecutableNotFoundException`), `ArgumentException` and
`OperationCanceledException` pass through unwrapped (D10).

The sync alias calls the async path with `GetAwaiter().GetResult()` (D8).

### 4.4 A missing `loaddependencies`

A script loaded with `#addin nuget:?package=Cake.Typstium` but without `&loaddependencies=true` has no
`Typstium.dll`. The aliases' public signatures name core types (`TypstCompileResult`, `ITypstCompiler`), so Cake's
compilation of the script fails (CS0012, the type "is defined in an assembly that is not referenced", naming
`Typstium`) before any alias runs. The addin can't intercept that, so:

- The package README and the user guide show the directive with `&loaddependencies=true` in every script example,
  and mention Cake's global switch (`NuGet_LoadDependencies=true` in `cake.config`, or the
  `CAKE_NUGET_LOADDEPENDENCIES=true` environment variable).
- CK3 pins that the failure names the `Typstium` assembly, so a user searching for the error finds the fix.

## 5. Finding Typst

The aliases build a `TypstExecutableResolver` from the core's public locator factories, plus one locator of their
own (D5):

| # | Locator | Source |
|---|---|---|
| 1 | Explicit path | `ToolPath` (settings, or `TypstCompiler(toolPath)`), when set |
| 2 | `TYPST_PATH` | `TypstExecutableLocators.EnvironmentVariable()` |
| 3 | **Cake tools** | `CakeToolsLocator` (below) |
| 4 | Bundled runtime | `TypstExecutableLocators.BundledRuntime()`; finds runtime packages referenced by a Cake.Sdk or Frosting project |
| 5 | WinGet | `TypstExecutableLocators.WinGet()` |
| 6 | Well-known locations | `TypstExecutableLocators.WellKnownLocations()` |
| 7 | `PATH` | `TypstExecutableLocators.SearchPath()` |

**`CakeToolsLocator`** (`Name` = `"CakeTools"`, in the style of the core's `BundledRuntime` and `WinGet`):

- The tools folder is the one `#tool` installs into: Cake's `Paths_Tools` configuration value, default `./tools`
  under the working directory.
- **Candidate RIDs:** the host RID from the OS and architecture, then its emulation fallback, as the core's
  bundled-runtime locator does (`win-arm64` → `win-x64`, `osx-arm64` → `osx-x64`). Implemented in the addin (D7).
- **Search:** for each candidate RID, folders matching `Typstium.Runtime.<rid>` or `Typstium.Runtime.<rid>.<version>`
  (`UseVersionInToolsFolder` decides which Cake writes), each with `tools/typst/<rid>/typst[.exe]`. When several
  versions are installed, the highest version wins (a folder without a version counts as lowest).
- **Execute bit:** on Unix the found file gets its execute bits set if they're missing, because NuGet extraction
  drops them.
- **Not found** lists the tools folder and each pattern tried, so `TypstExecutableNotFoundException` shows them
  alongside the other locators' searches.

## 6. Ways to provide Typst

The user guide (`docs/cake.md`) documents these, in this order:

| Way | How | Found by |
|---|---|---|
| `#tool`, one platform | `#tool nuget:?package=Typstium.Runtime.linux-x64&version=0.15.1` | Cake tools |
| `#tool`, several platforms | One line per RID the script runs on; the addin picks the host's (D4) | Cake tools |
| Cake.Sdk or Frosting | `<PackageReference Include="Typstium.Runtime.<rid>" />` in the build project; the core's `buildTransitive` targets copy Typst into the build's output | Bundled runtime |
| Installed on the runner | winget, Homebrew, cargo, a CI setup step, or a container image with Typst | WinGet, well-known locations or `PATH` |
| An exact binary | `TYPST_PATH` on the runner, or `ToolPath` in the script | `TYPST_PATH` / explicit path |

## 7. Scenarios

Each scenario has a stable ID. End-to-end tests carry `[Property("Scenario", "CKn")]`, as the runtime scenarios do.

| ID | Scenario | Expected | Verified by |
|---|---|---|---|
| CK1 | `.cake` script with `#addin …&loaddependencies=true` and `#tool` for the host RID | PDF produced; the result's `ExecutableFoundBy` is `CakeTools` | End to end |
| CK2 | As CK1, with `#tool` lines for several RIDs | The host's binary is chosen | End to end; locator unit tests |
| CK3 | `#addin` without `loaddependencies` | Cake fails to compile the script, and the error names the `Typstium` assembly (4.4) | End to end |
| CK4 | Cake.Sdk project referencing `Cake.Typstium` and `Typstium.Runtime.<rid>`, no `#tool` | PDF produced; found by the bundled runtime | End to end |
| CK5 | `TYPST_PATH` set and a `#tool` installed | `TYPST_PATH` wins | End to end; resolver unit tests |
| CK6 | A `.typ` with an error | Cake exits non-zero; the log has the canonical `file(line,col): error TYPST…` line | End to end; integration |

## 8. Testing

**Unit tests** (`tests/Cake.Typstium.UnitTests`, internals visible, no Typst, `Cake.Testing` fakes for the context,
environment and log):

- Settings mapping: each row of 4.2; defaults equal `TypstCompileOptions.Default`; relative paths resolve against
  the working directory.
- Format inference and the unknown-extension error; the default output path; the page-pattern rule.
- The runner through a fake `ITypstCompiler`: each status → `CakeException` or a returned result;
  `TreatWarningsAsErrors`; log lines and levels; core exceptions passing through; the sync and async aliases
  agreeing.
- `CakeToolsLocator` against temporary folders: host RID, emulation fallback, folders with and without a version,
  highest version wins, a configured `Paths_Tools`, the not-found search list, the execute bit on Unix.
- Resolver order: `ToolPath` → `TYPST_PATH` → Cake tools → the rest.

**Integration tests** (`tests/Cake.Typstium.IntegrationTests`, public API only, real Typst; skip, or fail when
`TYPSTIUM_REQUIRE_TYPST=1`, like the core's):

- A plain `.typ` fixture to PDF, to multi-page PNG with `{0p}`, and to SVG.
- `Inputs` reach `sys.inputs`; a bundled font with system fonts off.
- A syntax error → `CakeException` and the canonical error line in the log.
- A fixed `CreationTimestamp` → byte-identical PDFs from two runs.

**End-to-end tests** (`tests/Cake.Typstium.EndToEndTests`, modeled on `Typstium.Runtime.EndToEndTests`):

- The fixture packs `Typstium` and `Cake.Typstium` into a session feed with a unique version, next to the real
  runtime packages from the `pack` job (or a local `PackTypstRuntimes`).
- Each scenario of section 7 runs real Cake in a temporary folder: Cake.Tool 6.3.0 from a temporary tool manifest
  for `.cake` scripts, `dotnet run` for the Cake.Sdk project.

**CI:** the unit and integration tests join `Typstium.slnx` and the existing test tasks, so the `build` jobs run
them. The end-to-end tests join the runtime end-to-end jobs on Windows, Linux and macOS, which already download the
packed runtime packages.

## 9. To verify while planning

1. `#tool` installs no package dependencies (the basis of D4), and the folder layout it writes, with and without
   `UseVersionInToolsFolder`.
2. How an addin reads the tools folder: the `Paths_Tools` configuration key and the API that resolves it against
   the working directory.
3. That `[CakeNamespaceImport]` reaches Cake.Sdk scripts and Frosting, not only `.cake` scripts.
4. ~~That a missing `loaddependencies` can be caught at run time.~~ It can't: the script fails to compile (4.4).
5. Cake.Tool 6.3.0 runs `.cake` scripts on the .NET 10 SDK, and the addin's `net8.0` build loads in it.
6. `Cake.Testing` 6.3.0 fakes cover what the unit tests need (`FakeEnvironment`, `FakeLog`, `ICakeConfiguration`).
7. ~~The core's `TypstDocument.Paged.SaveAsync` handles the pattern rules of 4.3.~~ It throws `ArgumentException` for
   more than one page without `{p}` or `{0p}`, before writing; the runner checks first only to name the page count.

## 10. Related work

- **Markdown** through cmarker, for every integration together ([design overview](../design.md)).
- **`Cake.Typst`**, deferred (D3).
- **CI annotations**, deferred (D13).
