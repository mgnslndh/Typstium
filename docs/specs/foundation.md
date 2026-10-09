# Foundation: core API and CLI backend

**Status:** Implemented (foundation) · Oct 8, 2026 · Magnus Lindhe
**Part of:** [Typstium design overview](../design.md)
**Based on:** [Spike 0: CLI latency, Markdown, offline packages, diagnostics](../spikes/2026-10-07-cli-and-markdown.md)

## 1. Purpose and scope

The foundation is the core `Typstium` package and the repository it lives in. Every other package
(`Typstium.Build`, `Cake.Typstium`, `Typstium.AspNetCore`, the source generator) builds on it and on
nothing else.

**In scope**

- Repository scaffolding: SDK pinning, build properties, Central Package Management, solution, test
  projects, CI.
- The `Typstium` package (`netstandard2.0` + `net8.0`):
  - the request/result API, builders and convenience extensions
  - the CLI backend (`CliTypstCompiler`)
  - locating the Typst executable
  - capability detection
  - sandboxed workspaces
  - process execution with limits
  - diagnostics
  - the template provider abstraction, with directory and embedded-resource implementations
- Unit and integration tests against a locally installed Typst.

**Out of scope (separate specs)**

- Runtime NuGet packages and packaging CI (`runtime-packages.md`)
- `Typstium.Build` (`build.md`)
- `Cake.Typstium` (`cake.md`)
- `Typstium.AspNetCore` (`aspnetcore.md`)
- Markdown and Scriban input (`markdown.md`)
- Source generators (`source-generators.md`)
- The server backend (second generation)

The foundation must not need API changes to accommodate any of these. The bundled-runtime locator
already exists, so the runtime packages plug in later.

## 2. Design principles

Ordered by priority. SOLID sits below these, not above them.

1. **Make change local.** A change in one part should touch as little unrelated code as possible.
2. **Explicit dependencies.** Use constructor injection. No service locators, statics or ambient state.
   A class is understood from its constructor and public API.
3. **Infrastructure behind seams.** Processes, the file system and the environment sit behind internal
   seams where a seam pays for itself (testing, platform variation).
4. **Cohesive components, not class confetti.** Group by capability. Add an abstraction only for a real
   seam.
5. **Invalid states are hard to represent.** Use closed hierarchies, value objects, immutable records and
   nullable reference types.
6. **Immutability by default.** Mutable state is local (builders, the process runner).
7. **Orchestration is separate from implementation.** The compiler orchestrates a pipeline of small
   components.
8. **Misuse-resistant APIs.** No ambiguous `string` overloads, no Boolean parameter soup, no nullable
   parameters with unclear meaning.
9. **Deliberate async.** Use async for real I/O and pass `CancellationToken` everywhere. Never block on
   `.Result` or `.Wait()`.
10. **Errors are part of the design.** Expected outcomes are results. Exceptional failures are
    meaningful exception types that keep their context.
11. **Optimize for change, not abstraction.** Duplication is cheaper than the wrong abstraction.

Library-specific additions:

12. **The public API is forever.** Keep it small and `internal` by default. Track it with
    `PublicApiAnalyzers`.
13. **Honest wrapper.** Use Typst's names for Typst concepts. A documented escape hatch for CLI arguments
    means users never wait on us for a new Typst flag.
14. **Safe and reproducible by default.** Unsafe or non-reproducible behavior must be opted into.
15. **Minimal core dependencies.** No `Microsoft.Extensions.*` in the core.
16. **Explicit dependencies, convenient defaults.** Constructor injection, plus one obvious default
    setup (`TypstCompiler.CreateDefault()`) for scripts.

**Naming rule:** every public type carries the `Typst` prefix. Only namespaces and package IDs carry the
brand, so renaming the project (Typstium vs TypstSharpie is still open) doesn't change the API. Helper
folders are named `Utilities`, never `Helpers` or `Misc`.

## 3. Repository layout

```
Typstium/
├─ global.json                  .NET 10 SDK pin; "test": { "runner": "Microsoft.Testing.Platform" }
├─ Directory.Build.props        LangVersion=latest, Nullable=enable, TreatWarningsAsErrors=true,
│                               Deterministic, ContinuousIntegrationBuild (CI), SourceLink,
│                               ImplicitUsings=enable, PolyArgumentExceptions=true, analyzers
├─ Directory.Packages.props     Central Package Management
├─ .editorconfig
├─ .gitattributes               text=auto; *.sh eol=lf; binary fonts/images
├─ Typstium.slnx
├─ src/
│  └─ Typstium/
├─ tests/
│  ├─ Typstium.Testing/         shared test infrastructure (class library, not a test project)
│  ├─ Typstium.UnitTests/
│  └─ Typstium.IntegrationTests/
├─ samples/                     sample templates (also test fixtures)
├─ spike/                       throwaway spike code (not built)
├─ docs/
│  ├─ design.md                 family overview
│  ├─ specs/                    one living spec per major feature
│  ├─ plans/                    implementation plans
│  └─ spikes/
└─ .github/workflows/ci.yml
```

### Source layout of `src/Typstium`

The public API is in one namespace, `Typstium`. Internals are grouped by capability.

| Folder | Namespace | Contents |
| --- | --- | --- |
| `/` (root) | `Typstium` | `ITypstCompiler`, `TypstCompiler`, requests, results, options, exceptions |
| `Compilation/` | `Typstium.Compilation` | `CliTypstCompiler` pipeline orchestration, outcome mapping (internal) |
| `Sources/` | `Typstium` | `TypstSource`, `TypstVirtualFile`, `TypstPath` (public) |
| `Output/` | `Typstium` | `TypstOutput`, `TypstDocument`, `PdfStandard`, `TypstPageSelection` (public) |
| `Templates/` | `Typstium` | `ITemplateProvider`, `TypstTemplate`, directory and embedded providers (public) |
| `Executable/` | `Typstium.Executable` + public types in `Typstium` | Resolver and locators |
| `Capabilities/` | `Typstium.Capabilities` | `TypstCapabilities`, `TypstCliProfile`, `TypstCliProfileFactory`: the only version-aware code (internal, see 5.7) |
| `Workspace/` | `Typstium.Workspace` | In-place and sandbox workspaces (internal) |
| `CommandLine/` | `Typstium.CommandLine` | `TypstArguments` (public, root namespace), argument builder, Windows escaper (internal) |
| `Processes/` | `Typstium.Processes` | Process runner, outcomes, containment (internal) |
| `Diagnostics/` | `Typstium.Diagnostics` + public types in `Typstium` | Human-format parser, classifier, path mapping |
| `Interop/Windows/` | `Typstium.Interop.Windows` | `Kernel32.cs`, `SafeJobObjectHandle.cs` (internal) |
| `Utilities/` | `Typstium.Utilities` | Small shared internals, only when genuinely shared |

### Dependencies of the `Typstium` package

| Dependency | Targets | Why |
| --- | --- | --- |
| System.Text.Json | `netstandard2.0` (in-box on `net8.0`) | JSON virtual files and inputs |
| System.Collections.Immutable | `netstandard2.0` | Immutable request types |
| System.Diagnostics.DiagnosticSource | `netstandard2.0` | `ActivitySource` tracing |
| Polyfill | all (private, source-only) | Compiler-support types and newer BCL APIs on `netstandard2.0` |

No `Microsoft.Extensions.*` and no `ILogger`. `Typstium.AspNetCore` adds logging on top.

**Polyfill over PolySharp:** PolySharp generates compiler-support types only. Polyfill does that and
also adds newer BCL APIs as extension methods, which the core needs on `netstandard2.0`. Never reference
both.

**Why `net8.0` in addition to `netstandard2.0`:**
- real `Process.WaitForExitAsync` and `Kill(entireProcessTree: true)`
- `ProcessStartInfo.ArgumentList` (no hand-written escaping)
- span APIs
- trimming and AOT annotations: the `net8.0` build sets `IsAotCompatible=true`

.NET Framework consumers use the `netstandard2.0` build.

## 4. Public API

### 4.1 Compiler

```csharp
public interface ITypstCompiler
{
    Task<TypstCompileResult> CompileAsync(TypstCompileRequest request, CancellationToken cancellationToken);
}

public static class TypstCompiler
{
    public static ITypstCompiler CreateDefault();                                   // default resolver + settings
    public static ITypstCompiler CreateDefault(CliTypstCompilerSettings settings);
}

public sealed class CliTypstCompiler : ITypstCompiler
{
    public CliTypstCompiler(ITypstExecutableResolver resolver, CliTypstCompilerSettings settings);
}

public sealed record CliTypstCompilerSettings
{
    public int MaxConcurrency { get; init; } = Environment.ProcessorCount;     // concurrent typst processes
    public string TempRoot { get; init; } = Path.Combine(Path.GetTempPath(), "typstium");
    public TimeSpan OrphanedWorkspaceAge { get; init; } = TimeSpan.FromHours(1);
    public TypstProcessPriority ProcessPriority { get; init; } = TypstProcessPriority.Normal; // D39
}
```

`ITypstCompiler` has exactly one method. Everything else is an extension method that builds a request
(4.6). That keeps every backend (CLI now, server later, test fakes) trivial to implement. The precedents
are `HttpClient.SendAsync` and `ILogger.Log`.

### 4.2 Request

```csharp
public sealed record TypstCompileRequest(TypstSource Source)
{
    public TypstInputs Inputs { get; init; } = TypstInputs.Empty;
    public TypstOutput Output { get; init; } = TypstOutput.Pdf();
    public TypstCompileOptions Options { get; init; } = TypstCompileOptions.Default;

    public static TypstCompileRequestBuilder ForTemplate(ITemplateProvider provider, string templateName);
    public static TypstCompileRequestBuilder ForTemplate(ITemplateProvider provider, TypstTemplateName templateName);
    public static TypstCompileRequestBuilder ForMarkup(string markup);
    public static TypstFileCompileRequestBuilder ForFile(string path);
}
```

**Split by lifetime:**
- The **request** says *what* to compile, per call: source, inputs and output.
- The **options** say *how* and *under which policy*. They're configured once and shared.

Output format is on the request, because one app may produce PDFs from one endpoint and PNG previews from
another.

The request is plain immutable data. It can be built directly, through the builder, or loaded from
storage, and it's the only thing backends receive.

### 4.3 Sources

```csharp
public abstract record TypstSource                       // closed hierarchy: private constructor
{
    public static TypstSource File(string path);                                     // root = file's folder
    public static TypstSource File(string path, string root);
    public static TypstSource Template(ITemplateProvider provider, TypstTemplateName name);
    public static TypstSource Template(ITemplateProvider provider, TypstTemplateName name,
                                       ImmutableArray<TypstVirtualFile> files);
    public static TypstSource Markup(string markup);
    public static TypstSource Markup(string markup, ImmutableArray<TypstVirtualFile> files);

    public sealed record FileSource ...;       // Path, Root
    public sealed record TemplateSource ...;   // Provider, Name, Files
    public sealed record MarkupSource ...;     // Markup, Files
}
```

**The source kind decides where `--root` points:**

| Source | Root | Virtual files | Typical user |
| --- | --- | --- | --- |
| `File` | The given root, or the file's folder (Typst's own default). Compiled **in place**, nothing copied | **Not representable**: `FileSource` has no files | MSBuild, Cake, scripts |
| `Template` | A fresh **sandbox** folder holding the template bundle and the virtual files | Yes | Web apps (use case B) |
| `Markup` | A fresh **sandbox** folder holding the virtual files; markup sent over stdin | Yes | Quick or one-off use |

File sources get data through `Inputs`, including JSON (`TypstCompileRequestBuilder.WithJsonInput`,
read in Typst with `json(bytes(sys.inputs.data))`). Inputs are modelled as concepts, not strings (D38):
`TypstInputKey` (non-blank, no `=`, ordinal), `TypstInput` (key plus string value, with `FromText` and
`FromJson` factories) and `TypstInputs` (an immutable set with unique keys that enumerates in ordinal key
order, so the command line is deterministic). Every input value is redacted in logs and telemetry, and
`ToString` shows only keys. Windows limits command lines to about 32,000
characters, so the command-line builder checks the total length and throws `ArgumentException` that
recommends a template or markup source with a virtual file when it's too long.

### 4.4 Virtual files and value objects

```csharp
public sealed record TypstVirtualFile
{
    public TypstPath Path { get; }
    public ReadOnlyMemory<byte> Content { get; }

    public static TypstVirtualFile FromBytes(TypstPath path, ReadOnlyMemory<byte> content);
    public static TypstVirtualFile FromText(TypstPath path, string text);                 // UTF-8, no BOM
    public static TypstVirtualFile FromJson<T>(TypstPath path, T value);
    public static TypstVirtualFile FromJson<T>(TypstPath path, T value, JsonSerializerOptions options);
    public static TypstVirtualFile FromJson<T>(TypstPath path, T value, JsonTypeInfo<T> typeInfo); // AOT
    public static Task<TypstVirtualFile> FromStreamAsync(TypstPath path, Stream stream, CancellationToken ct);
}
```

- **Content is held in memory, so requests can be replayed.** The server backend's "retry on a fresh
  worker" relies on this, and requests can be stored. `FromStreamAsync` reads the stream once, up front.
- **JSON is serialized when the file is created**, so the request stays immutable. The reflection-based
  overloads carry `RequiresUnreferencedCode` / `RequiresDynamicCode` on `net8.0`.

**Value objects**

| Type | Kind | Rules |
| --- | --- | --- |
| `TypstPath` | sealed class | Root-relative and normalized to `/`. A leading `/` is optional and stripped. Rejects absolute paths, drive letters, `..` segments, empty segments and invalid characters. `Parse`, `TryParse`, `ToString()` gives `/logo.png` (Typst's root-relative form) |
| `TypstTemplateName` | sealed class | `[A-Za-z0-9._-]` plus `/` for nesting; no all-dot segments, and portable segments (below). `Parse`, `TryParse` |
| `TypstDiagnosticId` | sealed class | `TYPST` + four digits |
| `TypstPosition` | readonly record struct | 1-based `Line`, `Column` (MSBuild convention); `default` is never produced by the parser. Typst 0.15 reports 1-based lines but **0-based character columns**; the 0.15 parser profile converts them (verified while planning) |
| `TypstVersion` | readonly record struct | `Major.Minor.Patch`, comparable |
| `TypstTemplateVersion` | sealed class | Opaque non-empty string (content hash or provider ETag); compared by ordinal value |

Plain enums: `TypstCompileStatus`, `TypstOutputFormat` (`Pdf`, `Png`, `Svg`, `Html`),
`TypstDiagnosticSeverity`, `TypstNetworkAccess`.

The rule: a value type that needs validation is a sealed class, because a struct's `default` would
bypass validation. Plain coordinates and versions are structs.

Builders and extension methods accept `string` and parse it, so callers rarely see these types.

**Portable file names (D43).** Files Typstium writes into the sandbox, and template names (which become folder
paths), must have names Windows stores as written, on every platform, so nothing that works on Linux can fail
or alias on Windows. A segment may not end with `.` or a space (Windows strips them, so `data.json.` is
`data.json`), be a device name (`CON`, `PRN`, `AUX`, `NUL`, `COM0`–`COM9`, `LPT0`–`LPT9`, also before the first
dot: `nul.json`), or contain control characters. Virtual files are checked with the duplicates (at `Build()` or
`TypstSource.Template`/`Markup`), template bundles in the `TypstTemplate` constructor. `TypstPath` itself stays
permissive, because it also maps paths Typst prints back to the caller's files, which on Linux may legitimately
be named `aux.typ`. As a safety net for aliases no rule lists (NTFS 8.3 short names such as `DATAFI~1.JSO`), the
sandbox writes every file with `FileMode.CreateNew`: an existing file means two paths reached the same file,
which fails with `ArgumentException` instead of overwriting. Only a virtual file replacing a template file at the
same path overwrites, by design.

### 4.5 Output and result

```csharp
public abstract record TypstOutput                   // closed hierarchy
{
    public static TypstOutput Pdf();
    public static TypstOutput Pdf(params PdfStandard[] standards);
    public static TypstOutput Pdf(TypstPdfOptions options);              // standards, Tagged (default true), Pages
    public static TypstOutput Png();                                     // 144 ppi, all pages
    public static TypstOutput Png(int ppi);
    public static TypstOutput Png(int ppi, TypstPageSelection pages);
    public static TypstOutput Svg();
    public static TypstOutput Svg(TypstPageSelection pages);
    public static TypstOutput Html();                                    // experimental; needs the html feature
}

public sealed class PdfStandard          // known values as static instances; the CLI value is the identity
{
    public static PdfStandard Pdf17 { get; } ... A2b, A2u, A3b, A4, UA1 ...
    public static PdfStandard FromCliValue(string value);              // escape hatch for new standards
}

public sealed record TypstPageSelection  // All, or ranges: Pages(1), Range(2, 5), From(8)

public sealed class TypstCompileResult
{
    public TypstCompileStatus Status { get; }                 // Succeeded | Failed | TimedOut | OutputLimitExceeded
    public bool IsSuccess { get; }                            // Status == Succeeded
    public TypstDocument? Document { get; }                   // non-null exactly when IsSuccess
    public ImmutableArray<TypstDiagnostic> Diagnostics { get; }
    public TypstInvocation Invocation { get; }
    public TypstCompileResult EnsureSuccess();                // throws TypstCompileException
}

public abstract record TypstDocument                          // closed hierarchy; the format decides the shape
{
    public sealed record SingleFile(TypstOutputFormat Format, ReadOnlyMemory<byte> Content);   // PDF, HTML
    public sealed record Paged(TypstOutputFormat Format, ImmutableArray<ReadOnlyMemory<byte>> Pages); // PNG, SVG
    // SingleFile: WriteToAsync(Stream, ct), SaveAsync(string path, ct)
    // Paged:      SaveAsync(string pathPattern, ct)  with {p}, {0p}, {t} as in Typst
}

public sealed record TypstInvocation
{
    public string ExecutablePath { get; }
    public string ExecutableFoundBy { get; }                  // locator name, e.g. "WinGet"
    public TypstVersion TypstVersion { get; }
    public string CommandLine { get; }                        // redacted rendering
    public TimeSpan ProcessDuration { get; }                  // the typst process only
    public TimeSpan TotalDuration { get; }                    // the whole CompileAsync call
}
```

PNG and SVG are always `Paged`, even for a single page, so the result's shape depends only on the format.
`Status`, `IsSuccess` and `Document` are set together by one internal factory per status, so they can't
disagree.

### 4.6 Builders and convenience extensions

There are two sealed, mutable builders. `ForFile` returns a builder that **has no file methods**, so the
"file source + virtual files" combination can't be written:

```csharp
public sealed class TypstCompileRequestBuilder              // template and markup sources
{
    public TypstCompileRequestBuilder WithFile(string path, ReadOnlyMemory<byte> content);
    public TypstCompileRequestBuilder WithFile(TypstVirtualFile file);
    public TypstCompileRequestBuilder WithText(string path, string text);
    public TypstCompileRequestBuilder WithJson<T>(string path, T value);            // + options / JsonTypeInfo overloads
    public TypstCompileRequestBuilder WithInput(string key, string value);          // setting a key again replaces it
    public TypstCompileRequestBuilder WithInput(TypstInput input);
    public TypstCompileRequestBuilder WithJsonInput<T>(string key, T value);         // + overloads
    public TypstCompileRequestBuilder WithOutput(TypstOutput output);
    public TypstCompileRequestBuilder ToPdf(params PdfStandard[] standards);
    public TypstCompileRequestBuilder ToPng(int ppi = 144);
    public TypstCompileRequestBuilder ToSvg();
    public TypstCompileRequestBuilder WithOptions(TypstCompileOptions options);
    public TypstCompileRequestBuilder ConfigureOptions(Func<TypstCompileOptions, TypstCompileOptions> configure);
    public TypstCompileRequest Build();
}

public sealed class TypstFileCompileRequestBuilder          // file source: the same, minus WithFile/WithText/WithJson
```

Both builders forward to one internal core, so the duplication is a few one-line methods. That's cheaper
than a self-referencing generic base in the public API.

**Convenience extensions on `ITypstCompiler`** (static class `TypstCompilerExtensions`). Each one builds a
request and calls `CompileAsync`. Strings are never ambiguous, because the method name says what the
string is:

```csharp
CompileAsync(this ITypstCompiler, TypstCompileRequestBuilder builder)
CompileAsync(this ITypstCompiler, TypstFileCompileRequestBuilder builder)
CompileAsync(this ITypstCompiler, TypstSource source)
CompileAsync(this ITypstCompiler, TypstSource source, TypstOutput output)
CompileAsync(this ITypstCompiler, TypstSource source, TypstOutput output, TypstCompileOptions options)
CompileFileAsync(this ITypstCompiler, string path)
CompileFileAsync(this ITypstCompiler, string path, TypstOutput output)
CompileFileAsync(this ITypstCompiler, string path, TypstOutput output, TypstCompileOptions options)
CompileMarkupAsync(this ITypstCompiler, string markup, ...same overload pattern...)
CompileTemplateAsync(this ITypstCompiler, ITemplateProvider provider, string name, ...same overload pattern...)
// Each of the above also has an overload ending in a required `CancellationToken cancellationToken`.
```

Overload families never use optional parameters (analyzers RS0026/RS0027 stay enabled): a default value
is compiled into the caller, so adding overloads in later versions would be a source and binary
compatibility hazard. Each shape is a pair instead, as in the BCL (`Stream.ReadAsync`,
`HttpClient.GetAsync`): one overload without a token that passes `CancellationToken.None`, and one with a
required token. The single interface method `ITypstCompiler.CompileAsync` keeps `= default`, because it is
not an overload family.

**Open question before v1:** trim the `(x, output, options)` shapes and leave options to the builders,
which would roughly halve the extension surface.

Example:

```csharp
var compiler = TypstCompiler.CreateDefault();

// One line for scripts
var pdf = (await compiler.CompileFileAsync("docs/spec.typ", ct)).EnsureSuccess();

// Builder for richer requests
var result = await compiler.CompileAsync(
    TypstCompileRequest.ForTemplate(templates, "license")
        .WithJson("data.json", licenseViewModel)
        .WithFile("logo.png", logoBytes)
        .ToPdf(PdfStandard.A2b)
        .WithOptions(TypstCompileOptions.Untrusted),
    ct);
```

### 4.7 Options

```csharp
public sealed record TypstCompileOptions
{
    public static TypstCompileOptions Default { get; }
    public static TypstCompileOptions Untrusted { get; }

    public TypstFontOptions Fonts { get; init; }
    public TypstPackageOptions Packages { get; init; }
    public TimeSpan Timeout { get; init; }                               // Timeout.InfiniteTimeSpan to disable
    public long MaxOutputBytes { get; init; }
    public long? MemoryLimitBytes { get; init; }                         // Windows job object only
    public TypstCreationTimestamp CreationTimestamp { get; init; }       // Inherit | Now | Fixed(DateTimeOffset)
    public int? Jobs { get; init; }                                      // --jobs; null = Typst default
    public TypstProcessPriority? ProcessPriority { get; init; }         // null = the compiler's setting (D39)
    public TypstArguments ExtraGlobalArguments { get; init; }            // before the subcommand
    public TypstArguments ExtraArguments { get; init; }                  // after the generated arguments
    public Func<TypstArguments, TypstArguments>? ArgumentCustomization { get; init; } // full escape hatch
}

public sealed record TypstFontOptions
{
    public ImmutableArray<string> FontPaths { get; init; }
    public bool IncludeSystemFonts { get; init; }                        // default false
    public bool IncludeEmbeddedFonts { get; init; }                      // default true
}

public sealed record TypstPackageOptions
{
    public string? PackagePath { get; init; }                            // null = Typst's default location
    public string? PackageCachePath { get; init; }                       // null = Typst's default location
    public TypstNetworkAccess NetworkAccess { get; init; }               // Blocked (default) | Allowed
}
```

| Setting | `Default` | `Untrusted` |
| --- | --- | --- |
| `Timeout` | 60 s | 10 s |
| `MaxOutputBytes` | 100 MB | 20 MB |
| `MemoryLimitBytes` | null (off) | 1 GB |
| `Fonts.IncludeSystemFonts` | false | false |
| `Fonts.IncludeEmbeddedFonts` | true | true |
| `Packages.NetworkAccess` | Blocked | Blocked |
| `CreationTimestamp` | Inherit | Inherit |
| `ArgumentCustomization` | null | null |

`CreationTimestamp.Inherit` passes the host's `SOURCE_DATE_EPOCH` through when it's set (the CI
convention for reproducible builds). Otherwise Typst uses the current time.

### 4.8 Command-line customization

```csharp
public sealed class TypstArguments : IReadOnlyList<TypstArgument>     // immutable
{
    public static TypstArguments Empty { get; }
    public TypstArguments Add(string value);
    public TypstArguments AddSecret(string value);
    public TypstArguments AddSwitch(string name, string value);             // "--name" "value" as two items
    public TypstArguments AddSwitchSecret(string name, string value);
    public TypstArguments Insert(int index, TypstArgument argument);
    public TypstArguments RemoveAt(int index);
    public string Render();                                                  // for diagnostics only
    public string RenderSafe();                                              // secrets → [REDACTED]
}

public sealed record TypstArgument(string Value, bool IsSecret);
```

This is inspired by Cake's `ProcessArgumentBuilder` (typed arguments, `Render`/`RenderSafe`, the
`ArgumentCustomization` hook). It's much smaller, because arguments are a **list** rather than a string:
on `net8.0` they map to `ProcessStartInfo.ArgumentList`, and on `netstandard2.0` an internal Windows-rules
escaper (adapted from CliWrap, MIT, with attribution) renders them. So no "quoted" variants are needed.

The generated `--input` values are always marked secret, because they usually carry customer data.

`ArgumentCustomization` receives the complete generated list (global arguments, subcommand, arguments)
and can change anything, including `--root` and the font flags. That undoes guarantees by design, and
the documentation says so.

### 4.9 Template providers

```csharp
public interface ITemplateProvider
{
    ValueTask<TypstTemplate?> GetTemplateAsync(TypstTemplateName name, CancellationToken cancellationToken);
}

public sealed class TypstTemplate
{
    public TypstTemplate(TypstTemplateName name, TypstPath mainFile, TypstTemplateVersion version,
                         IReadOnlyList<TypstTemplateFile> files);   // validates: mainFile ∈ files, unique paths
    public TypstTemplateName Name { get; }
    public TypstPath MainFile { get; }
    public TypstTemplateVersion Version { get; }                    // content hash or provider ETag
    public IReadOnlyList<TypstTemplateFile> Files { get; }
}

public abstract class TypstTemplateFile
{
    public TypstPath Path { get; }
    public abstract ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken);
}
```

- A template is a **bundle**: a main file plus every file it may reference.
- `Version` costs almost nothing now. It's what sandbox caching and the later draft/published lifecycle
  need.
- `GetTemplateAsync` returns `null` for an unknown name. The compiler turns that into
  `ArgumentException`, since asking for a template that doesn't exist is a caller error.

**`DirectoryTemplateProvider(string rootDirectory)`**
- Each subfolder of the root is a template, and the name is the folder path relative to the root.
- The main file is `<last-name-segment>.typ` if it exists, otherwise `main.typ`. If neither exists, the
  template is reported as invalid with an `InvalidOperationException` naming both candidates.
- All files under the template folder belong to the bundle.
- `Version` is `sha256:` plus 64 hex digits over an unambiguous framing, so no two bundles share one:
  the tag `typstium-template-v2\0`, the file count, then per file in ordinal path order the UTF-8 path
  length, the path and the SHA-256 of its content (lengths are 8-byte little-endian). Content is hashed per
  file because a custom `TypstTemplateFile` stream may have no known length. Custom providers can use any
  version that changes whenever a file does, such as an ETag.

**`EmbeddedResourceTemplateProvider(Assembly assembly, string resourcePrefix)`**
- Manifest resource names turn folders into dots, which makes `logo.svg` in a folder ambiguous. So the
  provider requires resources embedded with a `LogicalName` that keeps the `/` separators, documented as:

  ```xml
  <EmbeddedResource Include="Templates\**" LogicalName="Templates/%(RecursiveDir)%(Filename)%(Extension)" />
  ```

- The provider normalizes `\` to `/` and groups resources by template folder under the prefix. Same
  main-file rule and versioning as the directory provider.
- A later `Typstium.Build` can add the item automatically.

Adapters for `Microsoft.Extensions.FileProviders` belong in a future `Typstium.Extensions.FileProviders`
package, not in the core.

### 4.10 Locating the Typst executable

```csharp
public interface ITypstExecutableResolver
{
    ValueTask<TypstExecutable> ResolveAsync(CancellationToken cancellationToken);   // throws TypstExecutableNotFoundException
}

public interface ITypstExecutableLocator
{
    string Name { get; }                                                              // "TYPST_PATH", "WinGet", …
    ValueTask<TypstLocatorResult> LocateAsync(CancellationToken cancellationToken);  // Found(path) | NotFound(searched[])
}

public sealed class TypstExecutableResolver : ITypstExecutableResolver
{
    public TypstExecutableResolver(IEnumerable<ITypstExecutableLocator> locators);   // ordered; caches the first hit
    public static TypstExecutableResolver CreateDefault();
    public static TypstExecutableResolver CreateDefault(string explicitPath);
}

public static class TypstExecutableLocators                     // factories; implementations are internal
{
    public static ITypstExecutableLocator ExplicitPath(string path);
    public static ITypstExecutableLocator EnvironmentVariable();                 // TYPST_PATH
    public static ITypstExecutableLocator EnvironmentVariable(string name);
    public static ITypstExecutableLocator BundledRuntime();
    public static ITypstExecutableLocator WinGet();
    public static ITypstExecutableLocator WellKnownLocations();
    public static ITypstExecutableLocator SearchPath();                          // PATH
}

public sealed record TypstExecutable(string Path, string FoundBy);
```

**Default order:** explicit path (when given) → `TYPST_PATH` → bundled runtime → WinGet → well-known
locations → `PATH`.

| Locator | Looks in |
| --- | --- |
| Bundled runtime | `AppContext.BaseDirectory/typst/<rid>/typst[.exe]` and, for classic ASP.NET on IIS, `AppContext.BaseDirectory/bin/typst/<rid>/`. The RID is computed from `RuntimeInformation` OS and architecture. On Unix it makes sure the execute bit is set |
| WinGet | `%LOCALAPPDATA%\Microsoft\WinGet\Links\typst.exe`, `%ProgramFiles%\WinGet\Links\typst.exe`, then `%LOCALAPPDATA%\Microsoft\WinGet\Packages\Typst.Typst_*\typst-*\typst.exe` (newest first). Windows only |
| Well-known locations | `~/.cargo/bin`, `/opt/homebrew/bin`, `/usr/local/bin`, `%USERPROFILE%\scoop\shims` |
| `PATH` | Each `PATH` entry, plus `PATHEXT` on Windows. Never the current directory |

- The resolver always returns a **fully qualified path**, so Windows' "current directory first" search
  rule is never involved.
- `TypstExecutableNotFoundException` lists, per locator, every place it searched.
- Locators get the environment and file existence through internal seams, so they're unit-testable.

### 4.11 Exceptions

```
TypstException                                   base, never thrown directly
├─ TypstExecutableNotFoundException              SearchedLocations (per locator)
├─ TypstFeatureNotSupportedException             RequiredVersion / Feature, InstalledVersion
├─ TypstInvocationException                      exit code 2: Arguments (redacted), ExitCode, Stderr
├─ TypstCompileException                         only from EnsureSuccess(): Result
└─ TypstProcessException (abstract)
   ├─ TypstProcessStartException                 Executable, InnerException (e.g. Win32Exception)
   ├─ TypstProcessCrashedException               ExitCode, Stderr
   └─ TypstProcessOutputException                Description of what was unreadable
```

**Who is at fault decides the mechanism:**

| At fault | Mechanism |
| --- | --- |
| The template | `TypstCompileResult` with a status and diagnostics |
| The environment or Typstium | `TypstException` subtype |
| The caller | `ArgumentException` (and subclasses) |
| Caller cancellation | `OperationCanceledException` (never wrapped) |

The process failures are separate subtypes because they carry different data and call for different
reactions:
- **Start failure:** never retry; fix the configuration.
- **Crash:** may retry (e.g. an out-of-memory kill).
- **Unreadable output:** a version mismatch bug.

### 4.12 Diagnostics

```csharp
public sealed record TypstDiagnostic
{
    public TypstDiagnosticId Id { get; }
    public TypstDiagnosticSeverity Severity { get; }            // Error | Warning
    public string Message { get; }
    public TypstLocation? Location { get; }                     // null when Typst reports no position
    public ImmutableArray<string> Hints { get; }
    public ImmutableArray<TypstTraceFrame> Trace { get; }       // innermost first
    public override string ToString();                          // "path(line,col): error TYPST1001: message"
}

public sealed record TypstLocation(TypstSourceFile File, TypstPosition Start, TypstPosition? End);
public sealed record TypstTraceFrame(string Message, TypstLocation? Location);

public abstract record TypstSourceFile                         // closed hierarchy
{
    public sealed record Project(TypstPath Path, string? FullPath);   // file-source root or template bundle
    public sealed record Virtual(TypstPath Path);                      // a TypstVirtualFile
    public sealed record Package(string Spec, TypstPath Path);         // "@preview/cmarker:0.1.10", "lib.typ"
    public sealed record Markup;                                       // inline source
}
```

`FullPath` is set only for files that exist on disk outside a sandbox (file sources). That's what
`Typstium.Build` needs for clickable MSBuild errors. **Sandbox paths never appear in a diagnostic.**

**Diagnostic IDs**

| Range | Meaning |
| --- | --- |
| `TYPST0001` / `TYPST0002` | Generic error / warning: the fallback for anything not classified |
| `TYPST1xxx` | Classified errors |
| `TYPST2xxx` | Classified warnings |
| `TYPST9xxx` | Typstium's own diagnostics, added to results |

The initial classification table, matched on message prefixes and pinned by tests:

| ID | Message starts with |
| --- | --- |
| `TYPST1001` | `unknown variable:` |
| `TYPST1002` | `file not found` |
| `TYPST1003` | `path ` … `would escape the project root` |
| `TYPST1004` | `package not found` |
| `TYPST1005` | `failed to download package` |
| `TYPST1006` | `failed to decode image` |
| `TYPST1007` | `unclosed delimiter` / `expected ` (syntax) |
| `TYPST1008` | `maximum function call depth exceeded`, `loop seems to be infinite` |
| `TYPST2001` | `unknown font family:` |
| `TYPST9001` | Output limit exceeded (added by Typstium) |
| `TYPST9002` | Compilation timed out (added by Typstium) |

If Typst rewords a message, the diagnostic falls back to the generic ID. IDs are never reused.

**Hints added by Typstium** (appended to the matching diagnostic's `Hints`):
- `TYPST2001` when system fonts are disabled: *"system fonts are disabled by default; add a font folder
  to Fonts.FontPaths or set Fonts.IncludeSystemFonts."*
- `TYPST1005` when the network is blocked: *"network access is blocked; vendor the package into
  Packages.PackagePath or set Packages.NetworkAccess to Allowed."*

**Parser.** Typstium always runs Typst with `--diagnostic-format human` and `--color never`, and parses:

```
<severity>: <message>
  ┌─ <path>:<line>:<col>
  │
N │ <source line>
  │   ^^^^                         caret count → end column (single-line spans)
  │
  = hint: <text>                   zero or more
                                   blank line
  while calling `<fn>` at <path>:<line>:<col>     zero or more trace frames
    <snippet>
```

- **Linear time, no regular expressions (D42):** each line is matched by hand in one pass; locations are read
  from the end (`:<line>:<col>`), so paths may contain colons; a trace frame's message ends at the first
  ` at ` that leaves a valid location. Stderr is attacker-influenced (an `assert` message is printed
  verbatim), and the whole parse is linear in its size, which the runner caps at 1 MB.
- **Paths:** turn Windows' verbatim forms back into ordinary ones (`\\?\C:\x` → `C:\x`, and for a root on a
  network share `\\?\UNC\server\share\x` → `\\server\share\x`), handle the drive-letter colon, and map paths
  to `TypstSourceFile`. Scrubbing recognizes the sandbox root in all three forms:
  - sandbox → `Project` or `Virtual`
  - `@ns/name:ver/…` → `Package`
  - `<stdin>` → `Markup`
- **Printed path forms differ by platform.** On Windows Typst prints absolute `\\?\`-prefixed paths. On
  Linux and macOS it prints paths under its working directory *relative* to it (`sub/helper.typ:2:2`;
  verified with Typst 0.15.1 on Ubuntu). The runner always uses the project root as working directory,
  so a relative printed path is root-relative. Spike 0 only captured Windows output; the CI run on Ubuntu
  found this.
- **Multi-line spans** (different box drawing) give `End = null`.
- **Ignored:** package download progress lines.
- **Robustness rule:** a block the parser doesn't recognize becomes a generic diagnostic whose message is
  the block's full raw text. A Typst format change degrades quality but never loses an error.

## 5. CLI backend

### 5.1 Pipeline

`CliTypstCompiler.CompileAsync` orchestrates ten steps. Each step is an internal component with one job:

| # | Step | Component | Notes |
| --- | --- | --- | --- |
| 1 | Validate | request validation | Caller errors → `ArgumentException`, before any I/O |
| 2 | Resolve executable | `ITypstExecutableResolver` | Cached for the compiler's lifetime |
| 3 | Resolve profile | `TypstCliProfileFactory` | Runs `typst info --format json` once per executable, cached per (path, size, last-write time), and produces a `TypstCliProfile`: the `TypstCapabilities` plus the version-sensitive components for steps 5 and 8. Then validates the request against the capabilities (≥ 0.15.0, features such as `html`), otherwise `TypstFeatureNotSupportedException`. See 5.7 |
| 4 | Prepare workspace | `InPlaceWorkspace` / `SandboxWorkspace`, over a `ScratchDirectory` | Inside the `MaxConcurrency` slot, which is taken before this step and released after step 7, so queued requests hold no folders (D44). A `ScratchDirectory` is a fresh `TempRoot/ws-<guid>/` that Typstium owns, holds a lease on, and deletes on dispose. Sandbox: its `root/` (template + virtual files) and `out/` (paged output). In place: the caller's folder is only `--root` and is never written to; a scratch `out/` exists only for paged output |
| 5 | Build command line | `profile.ArgumentsBuilder` | See 5.2 |
| 6 | Build environment | `ProcessEnvironmentBuilder` | See 5.3 |
| 7 | Run process | `ITypstProcessRunner` | Still inside the slot taken before step 4; see 5.4 |
| 8 | Parse diagnostics | `profile.DiagnosticParser` + `profile.DiagnosticClassifier` + `SourcePathMapper` | Section 4.12 |
| 9 | Map outcome | `OutcomeMapper` | See 5.5 |
| 10 | Collect document | `DocumentCollector` | Stdout bytes, or `out/` pages in page order |

The template bundle is copied file by file into the sandbox on every compile. Caching prepared bundles by
`TypstTemplateVersion` is a later optimization, gated on measurements.

On startup (the first compile), the compiler deletes scratch folders in `TempRoot` older than
`OrphanedWorkspaceAge` whose owner is gone. These are left behind when a host is killed hard. It only deletes
folders named exactly `ws-` plus 32 lowercase hex digits, so a host folder that merely starts with `ws-` (for
example `ws-reports`, if `TempRoot` points at the host's own data) is never touched. Ownership is a **lease**
(D44): every scratch folder holds its `.lease` file open with `FileShare.None` while it's in use (a sharing
lock on Windows, an advisory `flock` honored by every .NET process on Unix), and the operating system releases
it when the owner dies, even on a hard kill. The cleaner skips any folder whose lease it can't open, so a live
folder of another compiler sharing `TempRoot` survives however old it is. Setting
`DOTNET_SYSTEM_IO_DISABLEFILELOCKING=1` turns the Unix lock off and with it this protection.

### 5.2 Command line

The arguments are generated in this order:

```
--color never                          global (always)
<ExtraGlobalArguments>
compile
--root <root>
--diagnostic-format human
--font-path <p>…   [--ignore-system-fonts]   [--ignore-embedded-fonts]
[--package-path <p>]   [--package-cache-path <p>]
[--jobs <n>]   [--creation-timestamp <unix>]
[--pdf-standard <a,b>]   [--no-pdf-tags]   [--pages <sel>]   [--ppi <n>]   [--features html]
--input <k=v>…                         (secret)
<input>                                main file path, or - (stdin) for markup sources
<output>                               - (stdout) for PDF and HTML; out/{0p}.png|svg for PNG and SVG
<ExtraArguments>
```

Then `ArgumentCustomization` (if set) receives the whole list.

Output targets:
- **PDF and HTML** go to stdout (`-`).
- **PNG and SVG** always go to `out/{0p}.png|svg`, even for one page. That keeps one code path and
  matches the `Paged` document shape.
- **File sources** have no sandbox, so their paged output goes to a temporary `out/` folder under
  `TempRoot`, which is deleted afterwards.

### 5.3 Environment

- Start from the current process environment.
- Remove every variable whose name starts with `TYPST_`. The host's `TYPST_FONT_PATHS`, `TYPST_ROOT`
  and so on must not silently change results; every setting is passed explicitly.
- `CreationTimestamp`:
  - `Inherit`: leave `SOURCE_DATE_EPOCH` as is
  - `Now`: remove it
  - `Fixed`: pass `--creation-timestamp`
- `NetworkAccess.Blocked`:
  - set `HTTPS_PROXY`, `HTTP_PROXY` and `ALL_PROXY` to `http://127.0.0.1:9` (a closed port)
  - remove `NO_PROXY`
  - so any package not found locally fails fast instead of downloading (verified in the spike)

### 5.4 Process runner and containment

```csharp
internal interface ITypstProcessRunner
{
    Task<ProcessOutcome> RunAsync(ProcessSpec spec, CancellationToken cancellationToken);
}

internal sealed record ProcessSpec(string Executable, TypstArguments Arguments,
    IReadOnlyDictionary<string, string> Environment, string WorkingDirectory,
    ReadOnlyMemory<byte> Stdin, TimeSpan Timeout, long MaxStdoutBytes, long MaxStderrBytes,
    long? MemoryLimitBytes, TypstProcessPriority Priority = TypstProcessPriority.Normal);

internal abstract record ProcessOutcome(TimeSpan Duration, string Stderr)   // closed hierarchy
{
    internal sealed record Exited(int ExitCode, ReadOnlyMemory<byte> Stdout, ...);
    internal sealed record TimedOut(...);                     // killed by our timeout
    internal sealed record OutputLimitExceeded(long LimitBytes, ...);  // killed by our stdout cap
}
```

| Field | Meaning |
| --- | --- |
| `Stdin` (input) | Bytes written to standard input: the main file for markup sources, otherwise empty |
| `ExitCode` | Typst's exit code, only for `Exited`: 0 success, 1 compile error, 2 bad arguments, other = crash (101 Rust panic, 137 SIGKILL on Linux) |
| `Stdout` | Bytes Typst wrote to standard output: the document for PDF and HTML. Reading stops and the process is killed once `MaxStdoutBytes` is exceeded |
| `Stderr` | Diagnostics text, decoded as UTF-8 and capped at `MaxStderrBytes` (1 MB) |
| `Duration` | Wall-clock time from process start to exit or kill |

Runner behavior:
- **Process start:** `UseShellExecute = false`, the fully resolved executable path, the working directory
  set to the workspace root, and stdin, stdout and stderr all redirected.
- **Priority:** unless it's `Normal`, `Process.PriorityClass` is lowered right after start, before the
  job object is attached (D39). Failing for any reason other than "already exited" kills the process
  and throws `TypstProcessStartException`.
- **Pipes:** stdout and stderr are read concurrently, and stdin is written and closed right after start,
  all on dedicated threads (D40). Process pipes are synchronous streams on every target framework, so
  `ReadAsync` would block a thread-pool thread per pipe for the whole compile.
- **Timeout:** a linked `CancellationTokenSource`. On timeout, the output cap or caller cancellation, the
  callback terminates the Windows job object, one system call that stops Typst at once. On `net8.0` the tree
  walk (`Kill(true)`) then runs on a dedicated thread: it's the kill where there's no job (non-Windows) and the
  backstop for children outside the job (started before it was attached). It kills the parent itself, since
  on Unix a child whose parent has died is re-parented and can't be found. `netstandard2.0` has no tree walk
  and calls `Process.Kill()`, so there a child started between `Start()` and the job attach can outlive the
  kill and hold the pipes until it exits. Typst starts no child processes, so this is accepted (follow-ups
  doc).
- **Caller cancellation** throws `OperationCanceledException` after the kill. It's never returned as an
  outcome.
- **Paged output limit:** for paged output, the total size of `out/` is checked after exit. If it's over
  the limit, the outcome becomes `OutputLimitExceeded`.

**Containment**

```
Processes/IProcessContainment.cs       internal: Attach(Process), IDisposable
Processes/WindowsJobObject.cs         kill-on-close; optional JOB_OBJECT_LIMIT_PROCESS_MEMORY
Processes/NoProcessContainment.cs    non-Windows
Interop/Windows/Kernel32.cs           CreateJobObjectW, SetInformationJobObject,
                                      AssignProcessToJobObject, CloseHandle, structs, constants
Interop/Windows/SafeJobObjectHandle.cs  SafeHandleZeroOrMinusOneIsInvalid → CloseHandle
```

- P/Invoke is grouped per native library in `Typstium.Interop.Windows`, all `internal`.
- Signatures are blittable (no string marshalling), so a single `DllImport` declaration works on
  `netstandard2.0` and stays AOT-compatible on `net8.0`.
- Handles live in `SafeHandle`s, so a job object is closed even when `Dispose` isn't called. Closing it
  kills the process, which covers IIS app pool recycles.

### 5.5 Outcome mapping

| Process outcome | Result |
| --- | --- |
| `Exited(0)` | `Succeeded`, document collected, warnings in diagnostics |
| `Exited(1)` | `Failed`, diagnostics parsed |
| `Exited(2)` | throw `TypstInvocationException` |
| `Exited(other)` | throw `TypstProcessCrashedException` |
| `TimedOut` | `TimedOut`, plus diagnostic `TYPST9002` and any partial diagnostics |
| `OutputLimitExceeded` | `OutputLimitExceeded`, plus diagnostic `TYPST9001` |
| Start failure | throw `TypstProcessStartException` |
| `Exited(0)` but output missing or unreadable | throw `TypstProcessOutputException` |

### 5.6 Observability

- `ActivitySource` named `Typstium`, with a `typst.compile` activity per compile.
- Tags: source kind, template name, output format, Typst version, status, process duration,
  diagnostic counts.
- No logging in the core. `TypstInvocation` and the diagnostics carry everything a host wants to log.

### 5.7 Typst version handling

Users can upgrade Typst independently of Typstium, so the CLI backend must handle differences between
Typst versions **without version checks spread through the pipeline**. The spike showed where versions
differ, and every case lands in one of three existing components:

| What changes between versions | Example | Lands in |
| --- | --- | --- |
| CLI flags | `--make-deps` → `--deps` + `--deps-format` (0.14 → 0.15) | Arguments builder (step 5) |
| Diagnostic text and layout | Message wording; human-format layout | Diagnostic parser and classifier (step 8) |
| `typst info` JSON shape | New or renamed fields | Profile factory (step 3) |
| Available features | `html` behind `--features`; new PDF standards | Capabilities → request validation (step 3) |

**Design: one place knows about versions, everyone else asks about features.**

```csharp
internal sealed record TypstCapabilities          // named facts, never raw version comparisons
{
    public TypstVersion Version { get; }           // for TypstInvocation and telemetry only
    public bool SupportsHtml { get; }
    public ImmutableHashSet<PdfStandard> PdfStandards { get; }
    // … extended with one named fact per real difference
}

internal sealed record TypstCliProfile            // chosen once per executable
{
    public TypstCapabilities Capabilities { get; }
    public ICompileArgumentsBuilder ArgumentsBuilder { get; }
    public IDiagnosticParser DiagnosticParser { get; }
    public IDiagnosticClassifier DiagnosticClassifier { get; }
}

internal sealed class TypstCliProfileFactory      // the ONLY code that compares Typst versions
{
    public ValueTask<TypstCliProfile> GetProfileAsync(TypstExecutable executable, CancellationToken ct);
}
```

**Rules**

1. **Only `TypstCliProfileFactory` compares version numbers.** It turns `typst info` into
   `TypstCapabilities` and picks the components for that version range. Pipeline components receive the
   profile and ask named questions (`Capabilities.SupportsHtml`). They never ask "is the version ≥ x?".
2. **A small difference becomes a capability fact** used by the one component that cares, e.g. a renamed
   flag becomes a `DepsFlag` fact read by the arguments builder.
3. **A component that changes as a whole gets a second implementation,** selected by the factory, e.g.
   `HumanDiagnosticParserV015` and a later `HumanDiagnosticParserV016`. The pipeline is unchanged.
   Supporting a new Typst version means adding a row to the factory's version table.
4. **Version policy:**
   - below 0.15.0 → `TypstFeatureNotSupportedException`
   - a known range → that range's profile
   - **newer than the newest known version → the newest profile, optimistically.** The version is
     recorded in `TypstInvocation`, so problems are traceable, and users can upgrade Typst before we
     release.
5. **Users' escape hatches** for differences we haven't handled yet: `ExtraArguments` and
   `ArgumentCustomization` for flags. For diagnostics, the parser's raw-text fallback (4.12) means quality
   degrades but no error is lost.

**Today there is exactly one profile, for 0.15.x.** The cost of the seam is the profile record, the
factory and three small internal interfaces, all internal, so they can be reshaped freely. It also gives
unit tests a single place to inject fake components.

**Detecting drift early:** the CI job that runs against the latest Typst release (7.4) is the early
warning, and the per-version diagnostic fixtures (7.1) pin each profile's parser behavior.

## 6. Security summary

| Threat | Measure |
| --- | --- |
| Template reads host files | Sandbox root per compile for template and markup sources; `TypstPath` validation; Typst confines reads to `--root` |
| Package downloads, or network use by templates | `NetworkAccess.Blocked` by default (dead proxy); packages come only from `PackagePath` or the cache |
| Denial of service | Timeout, stdout cap, stderr cap, paged-output cap, Windows memory cap via job object |
| Orphaned processes | Job object with kill-on-close on Windows; kill on timeout or cancel everywhere |
| Binary planting | Fully qualified executable path; `UseShellExecute = false`; no search of the current directory |
| Host settings leaking in | `TYPST_*` variables removed from the child environment |
| Customer data in logs | `--input` values and `AddSecret` arguments redacted in `Invocation.CommandLine` |
| Escape hatch misuse | `ArgumentCustomization` is opt-in and documented as able to undo guarantees |

**Known limitations**
- On Linux and macOS, if the host dies hard, an orphaned `typst` can keep running until it finishes.
  Mitigation: container cgroup limits.
- The memory cap is Windows-only.
- `ArgumentCustomization` can undo any guarantee.
- The rule that untrusted Markdown needs `raw-typst: false` belongs to the Markdown spec.

## 7. Testing

### 7.1 Projects

| Project | Kind | Targets | Sees internals | Needs Typst |
| --- | --- | --- | --- | --- |
| `tests/Typstium.Testing` | Class library (shared infrastructure) | `net10.0;net472` | No | No |
| `tests/Typstium.UnitTests` | TUnit test project | `net10.0` (+ `net472` on Windows) | Yes (`InternalsVisibleTo`) | No |
| `tests/Typstium.IntegrationTests` | TUnit test project | `net10.0` (+ `net472` on Windows) | **No**, public API only | Yes |
| `tests/Typstium.TimingTests` | TUnit test project, `[assembly: NotInParallel]` | `net10.0` (+ `net472` on Windows) | **No**, public API only | Yes |

`Typstium.TimingTests` holds the tests that assert wall-clock bounds (timeouts, cancellation, timeouts
with every concurrency slot busy). They need the machine to themselves, so they run one at a time and,
in `build.ps1` and CI, after the other projects (D41). Everything else runs in parallel.

Test namespaces mirror the source namespaces (`Typstium.UnitTests.Diagnostics`, …). Shared test
infrastructure lives in `Typstium.Testing`, organized by kind:

```
Typstium.Testing/
├─ Fixtures/        SampleTemplates (paths to samples/), DiagnosticFixtures (per Typst version, see below),
│                   TemporaryDirectory
├─ Fakes/           FakeTemplateProvider (public abstractions only)
├─ Assertions/      PDF assertions (starts with %PDF, page count via typst eval), diagnostic assertions
└─ Typst/           Locating Typst for integration tests; skip-or-fail policy
```

**Diagnostic fixtures are kept per Typst version**, as data files rather than code:

```
tests/fixtures/
└─ typst-0.15/
   └─ diagnostics/
      ├─ 01-syntax.typ               the template that produces the diagnostic
      ├─ 01-syntax.human.stderr      captured output (from spike 0)
      └─ …
```

`DiagnosticFixtures` enumerates every version folder, so a parser test runs once per (version, case) and
uses that version's profile. When a new Typst version arrives, its captures go into a new
`typst-<version>/` folder (regenerated by running the fixture templates), and both profiles stay pinned by
tests. The spike's `spike/diagnostics/` captures move here, because `spike/` is throwaway.

`Typstium.Testing` uses the **public API only** and never gets `InternalsVisibleTo`. Fakes for internal
seams (`FakeProcessRunner`, `FakeLocatorEnvironment`) live in `Typstium.UnitTests/Testing/` (namespace
`Typstium.UnitTests.Testing`), the only project that can see internals. `Typstium.Testing/Fakes/` holds
fakes of public abstractions only, such as `FakeTemplateProvider`.

`dotnet test` works with no extra configuration, through `global.json`'s Microsoft.Testing.Platform
runner setting.

### 7.2 Unit tests (no Typst, fast, every platform)

| Area | Covers |
| --- | --- |
| Value objects | `TypstPath` rejects `..`, absolute paths, drive letters and empty segments; normalization; `TypstTemplateName`; `TypstDiagnosticId` |
| Request and builders | Immutability; `ForFile` has no file methods; `Build()` output |
| Command line | Argument order (`--color` before `compile`), output targets per format, `--input` redaction, extra arguments, customization hook, the 32K check |
| Environment | `TYPST_*` removed; dead proxy when blocked; `SOURCE_DATE_EPOCH` per `CreationTimestamp` |
| Diagnostics parser | One test per (Typst version, fixture) from `tests/fixtures/typst-*/diagnostics/`; unknown block becomes a generic diagnostic with the raw text; `\\?\` stripping; sandbox path mapping |
| Profile factory | Version table: below minimum throws; 0.15.x → the 0.15 profile; a newer unknown version → the newest profile; `typst info` JSON → `TypstCapabilities` |
| Classifier | Each table row; fallback to the generic ID; added hints |
| Outcome mapping | Every `ProcessOutcome` → status or exception, via `FakeProcessRunner` |
| Locators | Default order; each locator's search places; the not-found message lists everything searched |
| Template providers | Main-file rule; `LogicalName` resources; version hash stability |

### 7.3 Integration tests (real Typst)

| Scenario | Checks |
| --- | --- |
| Compile the license and release-notes samples | `%PDF` header, expected page count, no warnings |
| Every spike diagnostic case end to end | Status, ID, location mapped to template paths, no sandbox path in any diagnostic |
| 300 pages with a 64 KB cap | `OutputLimitExceeded` |
| Missing package, network blocked | `Failed`, `TYPST1005` with the added hint |
| 50 parallel compiles | All succeed, no sandbox collisions, `MaxConcurrency` respected |
| Reproducibility | Same request twice with `CreationTimestamp.Fixed` → byte-identical PDFs |
| PNG multi-page | `Paged` document with the right page count and order |
| Markup source over stdin | Succeeds; diagnostics point at `Markup` |
| Windows: job-object memory cap | Memory-heavy template is killed and mapped to a crash or limit outcome |

**Timing tests** (`Typstium.TimingTests`, real Typst, run alone):

| Scenario | Checks |
| --- | --- |
| Nested loops with a 2 s timeout | `TimedOut`; the process ran at least 2 s and stopped within 5 s more; sandbox deleted |
| Twice `MaxConcurrency` timed-out compiles at once | Every process stops within 5 s of its timeout (guards D40) |
| Caller cancellation | `OperationCanceledException` within 5 s of the cancel; sandbox deleted |

Integration and timing tests **skip with a clear reason** when Typst can't be found. In CI,
`TYPSTIUM_REQUIRE_TYPST=1` makes a missing Typst fail the run instead.

### 7.4 CI

GitHub Actions on `windows-latest` and `ubuntu-latest`:
- restore, build (warnings as errors), unit tests, integration tests
- Typst installed with a pinned version (**0.15.1**, the declared minimum)
- a second job with the latest Typst release, allowed to fail, as an early warning for format changes
- every job runs `./build.ps1`, the same script used locally: build, unit tests, integration tests,
  then the timing tests alone (D41)
- the pinned-Typst jobs run `./build.ps1 -Coverage` (D45): coverage of the unit and integration tests,
  merged across test projects and target frameworks, printed as a table per namespace of each project
  under `src/` (line, branch, method; assembly subtotals and a total) and written to the job summary

## 8. Decisions

| # | Decision | Alternatives considered | Why |
| --- | --- | --- | --- |
| D1 | Foundation = scaffolding + core + template providers; runtime packages are a separate spec | Smaller (no providers); larger (with runtime packages) | Develop against an installed Typst first; the bundled-runtime locator keeps the door open |
| D2 | Immutable `TypstCompileRequest` as the backend contract, with a builder and extensions on top | Builder only; request only | Backends interchangeable and testable; requests can be stored and replayed |
| D3 | Names `TypstCompileRequest` / `TypstCompileResult` | `TypstCompilationRequest`, `TypstCompilerRequest` | One-to-one with `typst compile` and `CompileAsync`; extends to `TypstEvalRequest` |
| D4 | `Typst` prefix on all public types; brand only in namespaces and package IDs | `Typstium` prefix | Renaming the project doesn't touch the API |
| D5 | Template failures, timeouts and output limits are result statuses | Exceptions for timeouts and limits | "Who is at fault" rule; web endpoints handle all template failures in one place |
| D6 | Shallow exception hierarchy under `TypstException`; process failures as three subtypes | No base; one process exception with a `Kind` enum | One catch for library failures; the subtypes carry different data and need different reactions |
| D7 | Request (per call) vs options (shared) split by lifetime; output format on the request | One flat request; output in options | Options map to ASP.NET Core defaults, Cake settings and MSBuild metadata; one app may produce several formats |
| D8 | Template as a bundle with a version | Provider as a file system; `IFileProvider` in the core | The template boundary is a type, not a convention; versioning enables caching and the lifecycle; no `Microsoft.Extensions` in the core |
| D9 | The source kind decides the root: file sources in place, template and markup sources sandboxed; virtual files only on sandboxed sources | Always sandbox; caller chooses per request | Fast in-place builds; untrusted content always sandboxed; invalid combinations unrepresentable |
| D10 | Separate `TypstCompileRequestBuilder` and `TypstFileCompileRequestBuilder` | `With*` on the request; CRTP generic builder | Static safety without generics in the public API |
| D11 | Own internal process runner, inspired by CliWrap and Cake; no dependency on either | Depend on CliWrap | Needs job objects, a bounded stdout and Typst exit-code mapping; zero transitive dependencies |
| D12 | `TypstArguments` as a list, with `Render`/`RenderSafe`, extra arguments and an `ArgumentCustomization` hook | String arguments; Cake's full builder surface | `ArgumentList` removes the need for quoting variants; same escape-hatch model as Cake's `ToolSettings` |
| D13 | Parse the `human` diagnostic format, with a raw-text fallback | `short` format | Only `human` has hints, traces and spans (spike) |
| D14 | Own diagnostic IDs: generic plus a curated classified table | Generic only | `NoWarn` and `WarningsAsErrors` by code in MSBuild |
| D15 | Defaults: system fonts off, network blocked, 60 s timeout, 100 MB cap; `Untrusted` preset | Typst's own defaults | Safe and reproducible by default; the spike measured 3× faster without system fonts |
| D16 | No `ILogger` in the core; `TypstInvocation` + `ActivitySource` | `Microsoft.Extensions.Logging.Abstractions` | Minimal dependencies; hosts log the result |
| D17 | Polyfill, not PolySharp | PolySharp; both | Needs BCL API polyfills, not only compiler types |
| D18 | Tests in `Typstium.UnitTests` + `Typstium.IntegrationTests` + shared `Typstium.Testing` | `Typstium.Tests.Unit`/`.Integration`; one project with folders | Common .NET convention; different requirements; integration tests limited to the public API |
| D19 | Minimum Typst 0.15.0 | Support 0.14 | `typst info` and the current `--deps` flags; less branching |
| D20 | P/Invoke grouped per native library in `Typstium.Interop.Windows`, behind `SafeHandle`-owning types | Inline P/Invoke | Clear boundary; correct resource cleanup |
| D21 | Version differences handled by an internal per-executable `TypstCliProfile`: named capability facts plus version-specific components, chosen by `TypstCliProfileFactory`, the only code that compares versions. Newer unknown versions get the newest profile | Version `if`s where needed; a public dialect abstraction with per-version implementations now | Differences cluster in three components; designs the place for variation without speculative variations; internal, so free to reshape |
| D22 | Canonical roots: workspace roots are canonicalized (8.3 names expanded on Windows, symlinks resolved on Unix, filesystem roots kept intact) before Typst sees them | Pass the caller's path as is | Typst prints canonical paths in diagnostics; source mapping and the `--root` containment check only work if both sides use the same form |
| D23 | Kill terminates the whole Windows job object; no reliance on Polyfill's `Process.Kill(true)` | Polyfill's tree kill | The Polyfill `Kill` is not tree-aware on netstandard2.0; the job object is |
| D24 | The WinGet locator has no "newest first" ordering; the PATH locator accepts only `typst.exe` and only fully qualified entries (the resolver also rejects non-fully-qualified results from any locator) | Sort WinGet packages by version; accept `.cmd`/`.bat` shims; accept drive- or root-relative PATH entries | Shims can't be started without a shell; `C:typst.exe` resolves against the current directory of a drive, which would break 4.10 |
| D25 | `ITypstCompiler.CompileAsync` takes `CancellationToken cancellationToken = default` | Token without default | Callers that don't cancel shouldn't have to pass one |
| D26 | TYPST1008 also covers "condition is always true" | Only "always false" | Typst reports both forms with the same cause |
| D27 | `TypstCompileResult` has public factories and `Errors`/`Warnings` views | Internal factories only | Consumers fake compilers in their own tests; the factories keep status, `IsSuccess` and `Document` consistent |
| D28 | Implicit usings are enabled in `src`, and `PolyArgumentExceptions=true` | `ImplicitUsings=disable`; Polyfill's default exceptions | Less boilerplate; Polyfill's argument guards then throw the expected exception types |
| D29 | Validating regexes anchor with `\z`, never `$` | `$` | `$` accepts a trailing newline in .NET |
| D30 | Sandbox paths are scrubbed from diagnostic message, hint and trace text (the root becomes "/") | Scrub the location only | No sandbox path may leak through any diagnostic text |
| D31 | If the process exits before it is attached to the job object, continue uncontained | Fail the compile | A process that already exited needs no containment |
| D32 | A failed capability probe evicts itself from the profile cache | Cache failures | A transient failure must not poison later compiles |
| D33 | Unknown `PdfStandard.FromCliValue` values pass capability validation and go to Typst; only standards Typstium knows and the profile lacks are refused | Reject everything the profile doesn't list | Newer Typst versions add standards; a bad value surfaces as exit code 2 (`TypstInvocationException`) |
| D34 | `CliTypstCompilerSettings` is an allowed unprefixed public name | Rename to `TypstCliCompilerSettings` | It pairs with `CliTypstCompiler`, which is already an exception to the `Typst` prefix rule |
| D35 | Per-TFM `PublicAPI.Unshipped.<tfm>.txt` files alongside the shared one | One shared file | Some members differ between netstandard2.0 and net8.0 |
| D36 | Overload families have no optional parameters: each shape is a pair (no token / required `CancellationToken`); RS0026/RS0027 stay enabled | Suppress RS0026/RS0027 globally or per method | Optional defaults are compiled into callers, so later overloads become compatibility hazards; the pair pattern matches the BCL and keeps the analyzer useful for future overloads (decided in PR #1 review) |
| D37 | Paged output size is checked from file lengths before any page is read | Read pages, then compare | A runaway compile can't exhaust memory before `MaxOutputBytes` applies |
| D38 | Inputs are `TypstInputKey` / `TypstInput` / `TypstInputs`; values stay `string` and are always secret | `ImmutableDictionary<string, string>`; a `TypstInputValue` hierarchy (secret, computed); a per-input `IsSecret` flag | Validation lives on the key type and uniqueness and order on the set. Secrecy is a display policy separate from what kind of value it is, so it won't be a value subtype; values are usually customer data, so all stay redacted, and a per-input opt-out can be added later. Computed values, if added, are resolved by the builder at `Build()`, never inside a compiler, so the request stays plain, replayable data (decided in PR #1 review) |
| D39 | Process priority: `CliTypstCompilerSettings.ProcessPriority` (default `Normal`), overridable per request with `TypstCompileOptions.ProcessPriority`; values `Normal`, `BelowNormal`, `Idle` | Default `BelowNormal`; compiler-only or request-only; above-normal values | `Normal` matches running `typst` from a shell, and hosts opt in with one line. Per-request overrides allow interactive and batch work through one compiler. Raising priority needs elevated rights on Unix. Named `ProcessPriority` to keep "priority" free for a future queue (decided in PR #1 review) |
| D40 | Pipe I/O runs on dedicated threads; the kill callback terminates the job object, and the tree walk (which kills the parent too) runs on a dedicated thread | `ReadAsync` on pool threads; `Kill(entireProcessTree: true)` on the callback; job only, no tree walk | With every concurrency slot busy, blocked pool threads delayed the timeout's kill by up to 36 s, and the tree walk (a snapshot of every process) took seconds under load. Typstium never changes `ThreadPool` settings; they belong to the host (found in PR #1 review) |
| D41 | Timing tests in their own project, `Typstium.TimingTests`, run alone after the others; CI runs `build.ps1`, the same script as a local run | TUnit categories with filters; `[NotInParallel]` in the integration project with every test module serialized; CI steps in YAML only | A project makes the boundary visible and needs no filter expressions; everything else stays parallel. One script for CI and local runs can't drift apart, and a Cake build can replace its body later (decided in PR #1 review) |
| D42 | The human-format diagnostic parser matches lines by hand, without regular expressions, in time linear in stderr size | Regexes with match timeouts; `RegexOptions.NonBacktracking` | A template controls what stderr contains, and the old trace-frame pattern backtracked quadratically: seconds for one 20,000-character line, hours at the 1 MB cap, on the host after Typst had exited. `NonBacktracking` needs .NET 7+, and timeouts throw mid-parse and need a fallback per pattern. The library's remaining regexes are linear (anchored single quantifiers or non-overlapping classes); only `SourcePathMapper.PackagePath` sees stderr (found in the 2026-10-08 review) |
| D43 | Portable names for files Typstium writes and for template names, on every platform; `TypstPath` stays permissive; the sandbox writes with `CreateNew` | Validate only on Windows; validate in `TypstPath`; trust the name rules alone | Windows aliases trailing dots and spaces and reserves device names, so two accepted virtual files could become one (found in the 2026-10-08 review). Checking everywhere keeps requests portable, as rejecting case-only duplicates already does. `TypstPath` also reads back paths Typst prints, which on Linux may use those names. `CreateNew` turns any alias the rules miss (8.3 short names) into an error instead of a silent overwrite |
| D44 | Scratch folders hold a `.lease` file open with `FileShare.None`, and the cleaner deletes only folders whose lease it can open; the `MaxConcurrency` slot is taken before the workspace is prepared | Age only; a PID file; preparing workspaces before the queue | `TempRoot` is shared machine-wide by default, and age can't tell another compiler's live folder (infinite timeout, long queue) from an abandoned one (found in the 2026-10-08 review). A held lock dies with its process, unlike a PID file, which can be reused. Preparing inside the slot keeps a long queue from holding one copied template per waiting request |
| D45 | Coverage with the coverage extension TUnit already brings (Cobertura), merged with ReportGenerator run through `dnx`; unit and integration tests only; products are the projects under `src/`; opt-in with `build.ps1 -Coverage`, used by CI | Coverlet; a tool manifest; coverage of the timing tests; always on | No new package in the test projects, and `dnx` needs only the .NET 10 SDK `global.json` already requires. Instrumentation would distort the timing tests' wall-clock bounds and they add little coverage. Deriving products from `src/` keeps test infrastructure out and new products in without editing the script (decided in PR #1 review) |

## 9. To verify while planning

These are facts the plan must confirm before relying on them:

1. Polyfill provides `Process.WaitForExitAsync`, `Task.WaitAsync` and `CancellationTokenSource.CancelAsync`
   for `netstandard2.0`, and how Polyfill behaves alongside `InternalsVisibleTo` (duplicate internal
   types between the library and the unit test project).
2. TUnit supports running tests on `net472`. If not, use a minimal `net472` smoke-test console project.
3. The `.slnx` format works with the SDK and with TUnit/Microsoft.Testing.Platform in `dotnet test`.
4. `RecursiveDir` in `LogicalName` produces `\` on Windows (normalized by the provider).
5. `typst info --format json` field names, as used for capability detection.
6. A setup action for installing a pinned Typst in GitHub Actions (e.g. `typst-community/setup-typst`),
   or a direct download with SHA-256 verification against the GitHub asset digest.
7. `--creation-timestamp` and `SOURCE_DATE_EPOCH` give byte-identical PDFs (the reproducibility test).
