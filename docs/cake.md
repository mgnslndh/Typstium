# Typst in Cake builds

`Cake.Typstium` compiles [Typst](https://typst.app) documents in Cake scripts, Frosting and Cake.Sdk projects. It runs
the official Typst CLI through the Typstium API, so failed compiles fail the task with the error lines in the log. The
design and its reasoning are in the [spec](specs/cake.md); scenario IDs (CKn) below refer to its catalog.

The first generation compiles plain `.typ` files. Markdown through `@preview/cmarker` isn't covered yet; it comes later,
together with Markdown support in the other Typstium integrations.

## Install

**Cake script** (`.cake`):

```csharp
#addin nuget:?package=Cake.Typstium&loaddependencies=true
```

`loaddependencies=true` is required: the aliases use types from the `Typstium` package. Without it Cake can't load the
addin, and the script fails with these two errors (CK3):

```
Could not load …/tools/Addins/Cake.Typstium.1.0.0/lib/net8.0/Cake.Typstium.dll (missing Typstium, Version=…)
build.cake(5,18): error CS0103: The name 'TypstCompile' does not exist in the current context
```
 To load dependencies for every addin instead, set
`NuGet_LoadDependencies=true` in `cake.config`, or the environment variable `CAKE_NUGET_LOADDEPENDENCIES=true`.

**Cake.Sdk or Frosting** (a project):

```xml
<PackageReference Include="Cake.Typstium" Version="1.0.0" />
```

The aliases are available as in a script. Where you use Typstium's own types (`PdfStandard`, `TypstPageSelection`,
`TypstCompileResult`), add `using Typstium;`.

## Getting Typst

The addin looks for Typst in this order and uses the first one it finds:

1. `TypstCompileSettings.ToolPath` (or `TypstCompiler(toolPath)`)
2. the `TYPST_PATH` environment variable
3. a runtime package installed with `#tool`, for the machine running Cake
4. a runtime package referenced by a Cake.Sdk or Frosting project
5. WinGet, then well-known install folders (`~/.cargo/bin`, Homebrew, `/usr/local/bin`, Scoop), then `PATH`

When nothing is found, the error lists every place it searched.

### `#tool`

```csharp
#tool nuget:?package=Typstium.Runtime.win-x64&version=0.15.1
#tool nuget:?package=Typstium.Runtime.linux-x64&version=0.15.1&include=./**/typst
#tool nuget:?package=Typstium.Runtime.osx-arm64&version=0.15.1&include=./**/typst
```

- Add one line for each platform the script runs on, such as your laptop and your CI runners. Cake installs all of
  them, and the addin uses the one for the machine it runs on (CK2). On Arm64 Windows or Apple silicon, the x64 package
  also works, under emulation.
- Packages for Linux and macOS need `&include=./**/typst`. Cake's `#tool` picks up only `.exe` and `.dll` files by
  default, and fails with "Failed to install tool" without it.
- `Typstium.Runtime.All` doesn't work with `#tool`, because `#tool` doesn't install a package's dependencies.
- Each package is 22–26 MB. Cache the `tools/` folder on CI.

**Changing the version.** `#tool` installs each version into its own folder (`tools/Typstium.Runtime.linux-x64.0.15.1/`)
and never removes old ones, so a cached `tools/` can hold several. The addin uses the version your `#tool` line
registered in this run, so a rollback to an older version works even with a newer one still in `tools/` (CK7). Without
a `#tool` line for the platform it takes the version Cake finds in `tools/`, the highest if Cake finds none. Whenever
more than one version is installed, it warns:

```
…/tools has several versions of Typstium.Runtime.linux-x64: 0.15.1, 0.16.0; using 0.15.1, registered by #tool.
#tool doesn't remove old versions; delete the folders you don't need.
```

Delete the folders you no longer need, or key the CI cache on the script's `#tool` lines, to keep `tools/` tidy.

**Changing the working directory.** A script that changes `Context.Environment.WorkingDirectory` still finds the Typst
its `#tool` lines installed: the addin looks in the folder `#tool` really used first. Without a `#tool` line, it looks
in the tools folder under the *current* working directory. To pin it, give Cake an absolute tools folder with
`--paths_tools=<path>`, the `CAKE_PATHS_TOOLS` environment variable, or `Paths_Tools` in `cake.config`.

Platforms: `win-x64`, `win-arm64`, `osx-x64`, `osx-arm64`, `linux-x64`, `linux-arm64`, `linux-arm`.

### A runtime package in a Cake.Sdk or Frosting project

```xml
<PackageReference Include="Cake.Typstium" Version="1.0.0" />
<PackageReference Include="Typstium.Runtime.linux-x64" Version="0.15.1" />
<PackageReference Include="Typstium.Runtime.win-x64" Version="0.15.1" />
```

The build copies Typst next to the build project's output, and the addin finds it there with no `#tool` (CK4). See
[Shipping Typst with your app](runtime-packages.md) for how the packages behave.

### Installed on the runner

If the runner already has Typst (winget, Homebrew, cargo, a container image, or a CI setup step), you need nothing
else. To pin an exact binary, set `TYPST_PATH`; it takes precedence over `#tool` (CK5).

## Compiling

```csharp
Task("Docs").Does(() =>
{
    TypstCompile("docs/spec.typ");                                   // docs/spec.pdf
    TypstCompile("docs/spec.typ", "artifacts/spec.pdf");
    TypstCompile("docs/spec.typ", "artifacts/pages/spec-{0p}.png");  // one PNG per page
});
```

- **Paths** are relative to Cake's working directory. Missing output folders are created.
- **The output's extension picks the format**: `.pdf`, `.png`, `.svg`, or `.html` (Typst's experimental HTML export).
  Set `Format` in the settings to choose the format some other way.
- **PNG and SVG** produce one file per page. With more than one page, the output path needs `{p}` (the page number) or
  `{0p}` (the page number, zero-padded); `{t}` is the page count. The placeholders work in the file name only, not in
  a folder (`pages/page-{0p}.png`, not `page-{p}/image.png`).
- **Async:** each alias has a `TypstCompileAsync` twin, with and without a `CancellationToken`, for async tasks and
  parallel compiles:

  ```csharp
  Task("Docs").Does(async () =>
  {
      await Task.WhenAll(
          TypstCompileAsync("docs/spec.typ", "artifacts/spec.pdf"),
          TypstCompileAsync("docs/guide.typ", "artifacts/guide.pdf"));
  });
  ```

  In a `.Does(() => ...)` that isn't async, use `TypstCompile`. A `TypstCompileAsync` call that nothing awaits
  doesn't fail the task.

## Settings

```csharp
TypstCompile("docs/spec.typ", "artifacts/spec.pdf", new TypstCompileSettings()
    .WithFontPath("docs/fonts")
    .WithInput("version", version)
    .WithPdfStandard(PdfStandard.A2b)
    .WithCreationTimestamp(DateTimeOffset.UtcNow.Date));
```

In the document, `sys.inputs.version` reads the input.

| Setting | Fluent method | Default | Meaning |
|---|---|---|---|
| `ToolPath` | `WithToolPath` | none | The Typst executable, ahead of every other place |
| `Root` | `WithRoot` | the input's folder | The folder Typst may read files from |
| `Format` | `WithFormat` | from the extension | The output format |
| `PdfStandards` | `WithPdfStandard` (adds) | none | PDF/A or PDF/UA conformance; PDF only |
| `Ppi` | `WithPpi` | 144 | Resolution; PNG only |
| `Pages` | `WithPages` | all | The pages to export, e.g. `TypstPageSelection.Of(TypstPageRange.Single(1))` |
| `Inputs` | `WithInput` (adds or replaces) | none | Values for `sys.inputs` |
| `FontPaths` | `WithFontPath` (adds) | none | Font folders |
| `IncludeSystemFonts` | `WithSystemFonts` | off | Let Typst use the machine's installed fonts |
| `PackagePath` | `WithPackagePath` | Typst's | Vendored packages, laid out as `namespace/name/version` |
| `PackageCachePath` | `WithPackageCachePath` | Typst's | Where Typst caches downloaded packages |
| `AllowNetwork` | `WithNetwork` | off | Let Typst download packages |
| `CreationTimestamp` | `WithCreationTimestamp` | `SOURCE_DATE_EPOCH` when set | The date written into the document |
| `Timeout` | `WithTimeout` | 60 s | How long one compile may run |
| `TreatWarningsAsErrors` | `WithWarningsAsErrors` | off | Fail the task on warnings |
| `ExtraArguments` | `WithExtraArguments` | none | Arguments appended to Typst's command line |
| `ArgumentCustomization` | `WithArgumentCustomization` | none | Rewrite the whole command line; can undo every guarantee |

**The defaults make output reproducible.** System fonts are off, so a document renders the same on every machine:
put the fonts it needs in a folder and add it with `WithFontPath`. When Typst can't find a font, the warning says so.
The network is off too, so `#import "@preview/..."` works only for packages in `PackagePath` or Typst's cache. Vendor
them into a folder, or use `WithNetwork()`.

A setting that doesn't fit the format, such as `Ppi` for a PDF, `PdfStandards` for a PNG or `Pages` for HTML, is an
error rather than being ignored.

## Failures and diagnostics

Each successful compile logs which Typst ran and where it came from, which is the first thing to check when output
differs between machines:

```
Typst 0.15.1 (CakeTools: …/tools/Typstium.Runtime.linux-x64.0.15.1/tools/typst/linux-x64/typst) compiled …/docs/spec.typ to …/artifacts/spec.pdf in 412 ms.
```

The part in parentheses names where Typst was found: `ExplicitPath` (from `ToolPath`), `TYPST_PATH`, `CakeTools`
(`#tool`), `BundledRuntime` (a runtime package referenced by the project), `WinGet`, `WellKnownLocations` or `PATH`.

Every diagnostic goes to the log, errors as errors and warnings as warnings, in the form IDEs and CI problem matchers
understand:

```
C:\build\docs\spec.typ(12,3): error TYPST1001: unknown variable: versoin
```

Hints and call traces follow at `Verbose` verbosity.

- A failed compile, a timeout or an output over the size limit throws `CakeException` after logging, so the task fails
  the Cake way; `ContinueOnError` and `OnError` work as usual. Nothing is written.
- Warnings don't fail the task unless `TreatWarningsAsErrors` is set.
- When Typst can't be found or can't start, Typstium's own exception (for example
  `TypstExecutableNotFoundException`, which lists the places searched) reaches the log unchanged.
- On success the alias returns the `TypstCompileResult`, for example
  `result.Invocation.TypstVersion` or `result.Warnings`.

## Beyond the aliases

`TypstCompiler()` returns a compiler that searches for Typst the same way, for everything the core API offers: markup
and templates, files passed from memory, pages kept in memory, and batches that share one compiler's limit on
parallel compiles.

```csharp
Task("Certificates").Does(async () =>
{
    var compiler = TypstCompiler();
    foreach (var name in names)
    {
        var result = (await compiler.CompileAsync(
            TypstCompileRequest.ForFile(MakeAbsolute(File("certificates/certificate.typ")).FullPath)
                .WithInput("name", name))).EnsureSuccess();
        await ((TypstDocument.SingleFile)result.Document!).SaveAsync(
            MakeAbsolute(File($"artifacts/certificates/{name}.pdf")).FullPath, CancellationToken.None);
    }
});
```

Paths given to the core API are not made relative to Cake's working directory, so the example passes full paths. The
output folder must exist; the core API doesn't create it.
