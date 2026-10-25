<h1 align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="assets/typstium-logo-white-text.svg">
    <source media="(prefers-color-scheme: light)" srcset="assets/typstium-logo-dark-text.svg">
    <img alt="Typstium" src="assets/typstium-logo-dark-text.svg" width="400">
  </picture>
</h1>

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

To run exactly what CI runs (the build's own tests, build, unit, integration, then the timing tests alone), use
`./build.sh` or `./build.ps1`. Both start the Cake build in `build/Build` and pass their arguments on. Add
`--coverage` for code-coverage tables of the unit and integration tests (by visibility, namespace and area, plus the
classes with the most missed lines), as CI prints them. `--target=CoverageReport` merges and reports raw coverage
files already in `artifacts/coverage/raw/`, such as CI's Windows and Ubuntu artifacts copied together. Other
arguments: `--configuration=Debug`, and `--target=<task>` for a single step (`BuildTests`, `Build`, `UnitTests`,
`IntegrationTests`, `TimingTests`).
