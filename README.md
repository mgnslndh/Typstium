# Typstium

Typst for .NET: an idiomatic C# API over the official [Typst](https://typst.app) CLI. Typst does the
typesetting; Typstium finds it, runs it safely and turns its output into .NET types.

> Preview. Not on NuGet yet. Design: [docs/design.md](docs/design.md), core spec:
> [docs/specs/foundation.md](docs/specs/foundation.md).

```csharp
var compiler = TypstCompiler.CreateDefault();

// A .typ file on disk, compiled in place
var spec = (await compiler.CompileFileAsync("docs/spec.typ")).EnsureSuccess();

// A named template with per-call data, compiled in a sandbox
var templates = new DirectoryTemplateProvider("templates");
var result = await compiler.CompileAsync(
    TypstCompileRequest.ForTemplate(templates, "license")
        .WithJson("data.json", license)
        .ToPdf(PdfStandard.A2b)
        .WithOptions(TypstCompileOptions.Untrusted));

if (!result.IsSuccess)
{
    foreach (var diagnostic in result.Diagnostics)
    {
        Console.WriteLine(diagnostic); // license.typ(3,2): error TYPST1001: unknown variable: x
    }
}
```

Requires Typst 0.15 or newer, found through `TYPST_PATH`, a bundled runtime, WinGet, common install
locations or `PATH`.

## Building

```shell
dotnet build
dotnet test   # integration tests are skipped when Typst isn't installed
```

To run exactly what CI runs (build, unit, integration, then the timing tests alone), use `./build.ps1`
(PowerShell 7). Add `-Coverage` for code-coverage tables of the unit and integration tests (by visibility,
namespace and area, plus the classes with the most missed lines), as CI prints them. `-CoverageReportOnly`
merges and reports raw coverage files already in `artifacts/coverage/raw/`, such as CI's Windows and Ubuntu
artifacts copied together.
