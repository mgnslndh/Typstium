# Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the core `Typstium` library: an immutable request/result API over the Typst CLI with a CLI
backend, sandboxed workspaces, process limits, structured diagnostics and template providers, plus the
repository scaffolding and test projects around it.

**Architecture:**
- **Public API:** immutable data in the `Typstium` namespace (request, sources, output, options, result,
  diagnostics). Two builders and extension methods sit on top of `ITypstCompiler.CompileAsync`.
- **CLI backend:** `CliTypstCompiler` orchestrates a fixed pipeline of small internal components:
  validate → resolve executable → resolve profile → workspace → arguments → environment → process →
  diagnostics → outcome → document.
- **Version differences** are isolated in an internal `TypstCliProfile` chosen by
  `TypstCliProfileFactory`, the only code that compares Typst versions.

**Tech Stack:** C# (LangVersion latest), `netstandard2.0` + `net8.0` library, .NET 10 SDK, Polyfill,
System.Text.Json, System.Collections.Immutable, TUnit on Microsoft.Testing.Platform, GitHub Actions.
Typst 0.15.1 for development and CI.

**Spec:** [`docs/specs/foundation.md`](../specs/foundation.md). Read it alongside this plan; the plan
argues from it, and section numbers in this plan refer to it.

## Global Constraints

- **Target frameworks:** library `netstandard2.0;net8.0`; tests `net10.0`, plus `net472` on Windows only.
- **SDK:** `global.json` pins `10.0.401` with `rollForward: latestFeature` and
  `"test": { "runner": "Microsoft.Testing.Platform" }`.
- **Compiler settings:** `LangVersion=latest`, `Nullable=enable`, `TreatWarningsAsErrors=true`, `Deterministic=true`. `ImplicitUsings=enable`
  in every project, including `src` (the plan's code relies on implicit `System`, `System.IO`, `System.Linq`,
  `System.Collections.Generic` and `System.Threading.Tasks` usings).
- **Core dependencies:** only System.Text.Json, System.Collections.Immutable,
  System.Diagnostics.DiagnosticSource, System.Memory and Microsoft.Bcl.AsyncInterfaces (all
  `netstandard2.0` only), plus Polyfill (private, source-only). **No `Microsoft.Extensions.*`, no
  `ILogger`.**
- **Polyfill:** `PolyUseEmbeddedAttribute=true` in every project that references it. Never reference
  PolySharp.
- **Namespaces:** every public type is in the `Typstium` namespace and has a `Typst` prefix (exceptions:
  `ITypstCompiler`, `ITemplateProvider`, `CliTypstCompiler`, `PdfStandard`, `DirectoryTemplateProvider`,
  `EmbeddedResourceTemplateProvider`).
- **Internals:** internal code goes in capability namespaces: `Typstium.Compilation`,
  `Typstium.Executable`, `Typstium.Capabilities`, `Typstium.Workspace`, `Typstium.CommandLine`,
  `Typstium.Processes`, `Typstium.Diagnostics`, `Typstium.Templates`, `Typstium.Interop.Windows`,
  `Typstium.Utilities`.
- **Folder names:** shared internal helpers go in `Utilities/`, never `Helpers/` or `Misc/`.
- **Async:** every `await` in `src/` uses `.ConfigureAwait(false)` (enforced by CA2007). Use
  `try`/`finally` with `await x.DisposeAsync().ConfigureAwait(false)` instead of `await using`.
- **APIs to avoid on `netstandard2.0`** (the plan's code already avoids them):
  - `string.Contains(char)` and `StartsWith(char)`: use the string or `IndexOf` forms
  - `Path.GetRelativePath` and `Path.Join`: use `Path.Combine` and prefix stripping
  - `Encoding.GetString(ReadOnlySpan<byte>)`: use arrays
  - `Convert.ToHexString`: use the manual hex loop
- **XML doc comments:** use `<see cref="X"/>` only for types that exist by the end of the current task; otherwise write
  `<c>X</c>`. `GenerateDocumentationFile` with warnings as errors turns an unresolved cref (CS1574) into a build break.
- **Value objects:** if a value type needs validation, it's a `sealed class`, because a struct's
  `default` would bypass validation.
- **`ImmutableArray<T>` properties:** normalize a `default` value to `Empty` in the setter or constructor.
- **Minimum Typst version:** **0.15.0**. Typst 0.15 reports **0-based columns** and 1-based lines; the
  public `TypstPosition` is 1-based for both.
- **Option defaults** (`TypstCompileOptions.Default`): `Timeout` 60 s, `MaxOutputBytes` 100 MiB,
  `MemoryLimitBytes` null, system fonts off, embedded fonts on, `NetworkAccess.Blocked`,
  `CreationTimestamp.Inherit`.
- **`Untrusted` preset:** `Timeout` 10 s, `MaxOutputBytes` 20 MiB, `MemoryLimitBytes` 1 GiB.
- **Arguments always passed:** `--color never` (global, before `compile`), `--diagnostic-format human`
  and an explicit `--format`.
- **TUnit conventions:**
  - `[Test]` methods are `public async Task`
  - assert with `await Assert.That(actual).IsEqualTo(expected)`, and never on a constant value
    (analyzer TUnitAssertions0005)
  - for exceptions, prefer `await Assert.ThrowsAsync<T>(...)`; `Assert.Throws<T>` returns `T?`, so
    dereference its result with `!`
  - data sources: `[Arguments(...)]` and `[MethodDataSource(typeof(X), nameof(X.M))]` returning
    `IEnumerable<Func<T>>`
  - runtime skip: `Skip.Test("reason")`
- **Running tests:**
  - one test class: `dotnet test --project <proj> --framework net10.0 --treenode-filter "/*/*/<Class>/*"`
  - everything: `dotnet test`
- **Commits:** end every message with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Work
  on branch `spike/cli-and-markdown` or a feature branch created from it, never on `main`.

## Review Focus

Inputs the spec implies but doesn't test directly. Each one has a pinning test in its owning task:

1. **Paths with spaces and non-ASCII characters** in the temp root, the template folder or font paths
   (e.g. `C:\Users\Åsa Ström\AppData\Local\Temp`). Arguments must survive escaping on `netstandard2.0`,
   and diagnostics must still map back. Pinned in Task 3 (escaper) and Task 19 (a temp root with a space
   and `å`).
2. **Two virtual files whose paths differ only by case** (`Data.json` and `data.json`). They would
   overwrite each other on Windows and macOS file systems, so they're rejected with `ArgumentException`.
   Pinned in Task 6.
3. **`--input` data larger than the 32K command-line limit** (`WithJsonInput` with a big model) must
   throw `ArgumentException` that recommends a virtual file, before starting a process. Pinned in
   Task 17.
4. **Cold start under parallel load:** many `CompileAsync` calls at once on a new compiler. The resolver,
   profile factory and orphan cleanup each run once, and cleanup never deletes a sandbox that's in use.
   Pinned in Task 17 (unit) and Task 19 (integration).
5. **Malformed or truncated stderr** (cut off mid-block by the stderr cap, unknown block formats,
   multi-line spans) must never throw. It degrades to generic diagnostics with the raw text. Pinned in
   Task 10.

---

## File Structure

```
global.json                                   SDK pin + MTP runner
Directory.Build.props                         shared compiler settings
Directory.Packages.props                      central package versions
.editorconfig  .gitattributes                 style, line endings
Typstium.slnx                                 solution
.github/workflows/ci.yml                      CI (Task 20)

src/Typstium/
  Typstium.csproj
  PublicAPI.Shipped.txt  PublicAPI.Unshipped.txt          (Task 20)
  ITypstCompiler.cs  TypstCompiler.cs  CliTypstCompiler.cs  CliTypstCompilerSettings.cs
  TypstCompileRequest.cs  TypstCompileOptions.cs  TypstFontOptions.cs  TypstPackageOptions.cs
  TypstNetworkAccess.cs  TypstCreationTimestamp.cs
  TypstCompileResult.cs  TypstCompileStatus.cs  TypstInvocation.cs
  TypstCompileRequestBuilder.cs  TypstFileCompileRequestBuilder.cs  TypstCompilerExtensions.cs
  Exceptions/        TypstException.cs + one file per exception type
  Sources/           TypstSource.cs  TypstVirtualFile.cs  TypstPath.cs
  Output/            TypstOutput.cs  TypstOutputFormat.cs  TypstPdfOptions.cs  PdfStandard.cs
                     TypstPageSelection.cs  TypstPageRange.cs  TypstDocument.cs
  Templates/         ITemplateProvider.cs  TypstTemplate.cs  TypstTemplateFile.cs  TypstTemplateName.cs
                     TypstTemplateVersion.cs  DirectoryTemplateProvider.cs
                     EmbeddedResourceTemplateProvider.cs  TemplateBundleRules.cs  TemplateHasher.cs
  Diagnostics/       TypstDiagnostic.cs  TypstDiagnosticId.cs  TypstDiagnosticSeverity.cs
                     TypstLocation.cs  TypstPosition.cs  TypstSourceFile.cs  TypstTraceFrame.cs
                     RawDiagnostic.cs  IDiagnosticParser.cs  HumanDiagnosticParserV015.cs
                     IDiagnosticClassifier.cs  DiagnosticClassifierV015.cs  DiagnosticIds.cs
                     SourcePathMapper.cs  DiagnosticHints.cs  DiagnosticAssembler.cs
  Executable/        ITypstExecutableResolver.cs  ITypstExecutableLocator.cs  TypstExecutable.cs
                     TypstLocatorResult.cs  TypstLocatorSearch.cs  TypstExecutableResolver.cs
                     TypstExecutableLocators.cs  ILocatorEnvironment.cs  SystemLocatorEnvironment.cs
                     Locators.cs
  Capabilities/      TypstVersion.cs  TypstCapabilities.cs  TypstCliProfile.cs  TypstCliProfileFactory.cs
                     TypstInfoParser.cs
  Workspace/         WorkspaceLayout.cs  IWorkspace.cs  InPlaceWorkspace.cs  SandboxWorkspace.cs
                     WorkspaceFactory.cs  OrphanedWorkspaceCleaner.cs
  CommandLine/       TypstArgument.cs  TypstArguments.cs  WindowsArgumentEscaper.cs
                     ICompileArgumentsBuilder.cs  CompileArgumentsInput.cs  CompileArgumentsBuilderV015.cs
                     EnvironmentChanges.cs  ProcessEnvironmentBuilder.cs
  Processes/         ITypstProcessRunner.cs  ProcessSpec.cs  ProcessOutcome.cs  TypstProcessRunner.cs
                     StreamReading.cs  IProcessContainment.cs  ProcessContainment.cs
                     WindowsJobObject.cs  NoProcessContainment.cs
  Interop/Windows/   Kernel32.cs  SafeJobObjectHandle.cs
  Compilation/       RequestValidator.cs  CapabilityValidator.cs  OutcomeMapper.cs  DocumentCollector.cs
                     TypstTelemetry.cs

tests/fixtures/typst-0.15/diagnostics/        *.typ + *.human.stderr (moved from spike, {ROOT} placeholder)
tests/fixtures/capture-diagnostics.sh         regenerates a version folder
tests/Typstium.Testing/                       RepositoryPaths, Fixtures/, Fakes/, Assertions/, Typst/
tests/Typstium.UnitTests/                     mirrors src namespaces; Testing/ holds internal fakes
tests/Typstium.IntegrationTests/              public API only
```

---

### Task 1: Repository scaffolding

Creates the build and test infrastructure. The deliverable is `dotnet build` and `dotnet test` passing,
with one smoke test in each test project.

**Files:**
- Create: `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, `.gitattributes`, `Typstium.slnx`
- Create: `src/Typstium/Typstium.csproj`
- Create: `tests/Typstium.Testing/Typstium.Testing.csproj`, `tests/Typstium.Testing/RepositoryPaths.cs`
- Create: `tests/Typstium.UnitTests/Typstium.UnitTests.csproj`, `tests/Typstium.UnitTests/RepositoryPathsTests.cs`
- Create: `tests/Typstium.IntegrationTests/Typstium.IntegrationTests.csproj`, `tests/Typstium.IntegrationTests/SamplesTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `Typstium.Testing.RepositoryPaths` with `static string Root`, `static string Samples`,
  `static string Fixtures` (absolute paths). Every project file and the central package versions.

- [ ] **Step 1: Create `global.json`**

```json
{
  "sdk": {
    "version": "10.0.401",
    "rollForward": "latestFeature"
  },
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

- [ ] **Step 2: Create `Directory.Build.props`**

```xml
<Project>
  <PropertyGroup>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <Deterministic>true</Deterministic>
    <ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
    <PolyUseEmbeddedAttribute>true</PolyUseEmbeddedAttribute>
    <Authors>Magnus Lindhe</Authors>
    <Copyright>Copyright (c) 2026 Magnus Lindhe</Copyright>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <RepositoryUrl>https://github.com/mgnslndh/Typstium</RepositoryUrl>
    <PackageTags>typst;pdf;typesetting;documents</PackageTags>
  </PropertyGroup>
</Project>
```

- [ ] **Step 3: Create `Directory.Packages.props`**

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Polyfill" Version="11.4.3" />
    <PackageVersion Include="System.Text.Json" Version="10.0.12" />
    <PackageVersion Include="System.Collections.Immutable" Version="10.0.12" />
    <PackageVersion Include="System.Diagnostics.DiagnosticSource" Version="10.0.12" />
    <PackageVersion Include="System.Memory" Version="4.6.3" />
    <PackageVersion Include="Microsoft.Bcl.AsyncInterfaces" Version="10.0.12" />
    <PackageVersion Include="Microsoft.CodeAnalysis.PublicApiAnalyzers" Version="5.6.0" />
    <PackageVersion Include="TUnit" Version="1.73.5" />
    <PackageVersion Include="TUnit.Core" Version="1.73.5" />
    <PackageVersion Include="TUnit.Assertions" Version="1.73.5" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Create `.editorconfig`**

```ini
root = true

[*]
charset = utf-8
end_of_line = lf
insert_final_newline = true
trim_trailing_whitespace = true
indent_style = space
indent_size = 4

[*.{json,yml,yaml,xml,csproj,props,targets,slnx}]
indent_size = 2

[*.md]
trim_trailing_whitespace = false

[*.cs]
csharp_style_namespace_declarations = file_scoped:suggestion
csharp_style_var_for_built_in_types = true:suggestion
csharp_style_var_when_type_is_apparent = true:suggestion
dotnet_style_qualification_for_field = false:suggestion
dotnet_naming_rule.private_fields_underscore.symbols = private_fields
dotnet_naming_rule.private_fields_underscore.style = underscore_prefix
dotnet_naming_rule.private_fields_underscore.severity = suggestion
dotnet_naming_symbols.private_fields.applicable_kinds = field
dotnet_naming_symbols.private_fields.applicable_accessibilities = private
dotnet_naming_style.underscore_prefix.capitalization = camel_case
dotnet_naming_style.underscore_prefix.required_prefix = _

[src/**.cs]
# Library code must not capture a synchronization context (classic ASP.NET on .NET Framework).
dotnet_diagnostic.CA2007.severity = warning
```

- [ ] **Step 5: Create `.gitattributes`**

```gitattributes
* text=auto eol=lf
*.cmd text eol=crlf
*.ps1 text eol=crlf
*.otf binary
*.ttf binary
*.png binary
*.pdf binary
*.nupkg binary
```

- [ ] **Step 6: Create `src/Typstium/Typstium.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>netstandard2.0;net8.0</TargetFrameworks>
    <RootNamespace>Typstium</RootNamespace>
    <ImplicitUsings>enable</ImplicitUsings>
    <PackageId>Typstium</PackageId>
    <Description>Typst for .NET: an idiomatic C# API over the official Typst CLI.</Description>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);CS1591</NoWarn>
  </PropertyGroup>

  <PropertyGroup Condition="$([MSBuild]::IsTargetFrameworkCompatible('$(TargetFramework)', 'net8.0'))">
    <IsAotCompatible>true</IsAotCompatible>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Polyfill" PrivateAssets="all" />
  </ItemGroup>

  <ItemGroup Condition="'$(TargetFramework)' == 'netstandard2.0'">
    <PackageReference Include="System.Text.Json" />
    <PackageReference Include="System.Collections.Immutable" />
    <PackageReference Include="System.Diagnostics.DiagnosticSource" />
    <PackageReference Include="System.Memory" />
    <PackageReference Include="Microsoft.Bcl.AsyncInterfaces" />
  </ItemGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="Typstium.UnitTests" />
  </ItemGroup>

</Project>
```

- [ ] **Step 7: Create `tests/Typstium.Testing/Typstium.Testing.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>net10.0</TargetFrameworks>
    <TargetFrameworks Condition="'$(OS)' == 'Windows_NT'">net10.0;net472</TargetFrameworks>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Polyfill" PrivateAssets="all" />
    <PackageReference Include="TUnit.Core" />
    <PackageReference Include="TUnit.Assertions" />
    <ProjectReference Include="..\..\src\Typstium\Typstium.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 8: Create `tests/Typstium.Testing/RepositoryPaths.cs`**

```csharp
namespace Typstium.Testing;

/// <summary>Absolute paths into the repository, found by walking up to Typstium.slnx.</summary>
public static class RepositoryPaths
{
    private static readonly Lazy<string> RootPath = new(FindRoot);

    public static string Root => RootPath.Value;

    public static string Samples => Path.Combine(Root, "samples");

    public static string Fixtures => Path.Combine(Root, "tests", "fixtures");

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Typstium.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException($"Could not find Typstium.slnx above {AppContext.BaseDirectory}.");
    }
}
```

- [ ] **Step 9: Create `tests/Typstium.UnitTests/Typstium.UnitTests.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>net10.0</TargetFrameworks>
    <TargetFrameworks Condition="'$(OS)' == 'Windows_NT'">net10.0;net472</TargetFrameworks>
    <RuntimeIdentifier Condition="'$(TargetFramework)' == 'net472'">win-x64</RuntimeIdentifier>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Polyfill" PrivateAssets="all" />
    <PackageReference Include="TUnit" />
    <ProjectReference Include="..\..\src\Typstium\Typstium.csproj" />
    <ProjectReference Include="..\Typstium.Testing\Typstium.Testing.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 10: Create `tests/Typstium.IntegrationTests/Typstium.IntegrationTests.csproj`**

Same as the unit test project. It has no `InternalsVisibleTo`, because `src/Typstium.csproj` only grants
internals to `Typstium.UnitTests`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>net10.0</TargetFrameworks>
    <TargetFrameworks Condition="'$(OS)' == 'Windows_NT'">net10.0;net472</TargetFrameworks>
    <RuntimeIdentifier Condition="'$(TargetFramework)' == 'net472'">win-x64</RuntimeIdentifier>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Polyfill" PrivateAssets="all" />
    <PackageReference Include="TUnit" />
    <ProjectReference Include="..\..\src\Typstium\Typstium.csproj" />
    <ProjectReference Include="..\Typstium.Testing\Typstium.Testing.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 11: Write the smoke tests**

`tests/Typstium.UnitTests/RepositoryPathsTests.cs`:

```csharp
using Typstium.Testing;

namespace Typstium.UnitTests;

public class RepositoryPathsTests
{
    [Test]
    public async Task Root_contains_the_solution_file()
    {
        var exists = File.Exists(Path.Combine(RepositoryPaths.Root, "Typstium.slnx"));
        await Assert.That(exists).IsTrue();
    }
}
```

`tests/Typstium.IntegrationTests/SamplesTests.cs`:

```csharp
using Typstium.Testing;

namespace Typstium.IntegrationTests;

public class SamplesTests
{
    [Test]
    public async Task License_sample_exists()
    {
        var exists = File.Exists(Path.Combine(RepositoryPaths.Samples, "templates", "license", "license.typ"));
        await Assert.That(exists).IsTrue();
    }
}
```

- [ ] **Step 12: Create the solution and add the projects**

Run:
```bash
dotnet new sln --format slnx -n Typstium
dotnet sln Typstium.slnx add src/Typstium/Typstium.csproj tests/Typstium.Testing/Typstium.Testing.csproj tests/Typstium.UnitTests/Typstium.UnitTests.csproj tests/Typstium.IntegrationTests/Typstium.IntegrationTests.csproj
```
Expected: `Typstium.slnx` exists and lists four projects.

- [ ] **Step 13: Build and test**

Run: `dotnet build`
Expected: `Build succeeded` with 0 warnings, 0 errors.

Run: `dotnet test`
Expected: `Test run summary: Passed!` On Windows: 4 tests (2 projects × `net10.0` + `net472`). On Linux: 2.

If `Typstium.Testing` fails to build because `TUnit.Core` expects to be in a test project, replace the
`TUnit.Core`/`TUnit.Assertions` references with `<PackageReference Include="TUnit.Assertions" />` only,
and move any use of `Skip` (Task 19) into the integration test project.

- [ ] **Step 14: Normalize line endings and commit**

`.gitattributes` now forces LF, so renormalize the files committed earlier:

```bash
git add --renormalize .
git add global.json Directory.Build.props Directory.Packages.props .editorconfig .gitattributes Typstium.slnx src tests
git commit -m "Scaffold solution, build settings and test projects

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Value objects

Implements the small validated types that the rest of the API is built from (spec 4.4).

**Files:**
- Create: `src/Typstium/Sources/TypstPath.cs`, `src/Typstium/Templates/TypstTemplateName.cs`, `src/Typstium/Templates/TypstTemplateVersion.cs`, `src/Typstium/Diagnostics/TypstDiagnosticId.cs`, `src/Typstium/Diagnostics/TypstPosition.cs`, `src/Typstium/Capabilities/TypstVersion.cs`
- Test: `tests/Typstium.UnitTests/Sources/TypstPathTests.cs`, `tests/Typstium.UnitTests/Templates/TypstTemplateNameTests.cs`, `tests/Typstium.UnitTests/Diagnostics/TypstDiagnosticIdTests.cs`, `tests/Typstium.UnitTests/Capabilities/TypstVersionTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces (all in namespace `Typstium`):
  - `sealed class TypstPath : IEquatable<TypstPath>`: `static TypstPath Parse(string)`, `static bool TryParse(string?, out TypstPath?)`, `string Value` (no leading `/`, e.g. `assets/logo.png`), `string RootRelative` (`/assets/logo.png`), `IReadOnlyList<string> Segments`, `string FileName`, `ToString()` returns `RootRelative`, ordinal equality
  - `sealed class TypstTemplateName : IEquatable<TypstTemplateName>`: `Parse`, `TryParse`, `string Value`, `string LastSegment`
  - `sealed class TypstTemplateVersion : IEquatable<TypstTemplateVersion>`: constructor `(string value)`, `string Value`
  - `sealed class TypstDiagnosticId : IEquatable<TypstDiagnosticId>`: `Parse`, `string Value`, `int Number`
  - `readonly record struct TypstPosition(int Line, int Column)`
  - `readonly record struct TypstVersion(int Major, int Minor, int Patch) : IComparable<TypstVersion>`: `Parse`, `TryParse`, comparison operators, `ToString()` gives `"0.15.1"`

- [ ] **Step 1: Write the failing `TypstPath` tests**

`tests/Typstium.UnitTests/Sources/TypstPathTests.cs`:

```csharp
namespace Typstium.UnitTests.Sources;

public class TypstPathTests
{
    [Test]
    [Arguments("logo.png", "logo.png")]
    [Arguments("/logo.png", "logo.png")]
    [Arguments("assets/logo.png", "assets/logo.png")]
    [Arguments("assets\\logo.png", "assets/logo.png")]
    [Arguments("./assets/./logo.png", "assets/logo.png")]
    [Arguments("data/åäö file.json", "data/åäö file.json")]
    public async Task Parse_normalizes(string input, string expected)
    {
        var path = TypstPath.Parse(input);
        await Assert.That(path.Value).IsEqualTo(expected);
        await Assert.That(path.RootRelative).IsEqualTo("/" + expected);
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    [Arguments("/")]
    [Arguments("../secret.txt")]
    [Arguments("assets/../../secret.txt")]
    [Arguments("assets/..")]
    [Arguments("C:/Windows/win.ini")]
    [Arguments("C:\\Windows\\win.ini")]
    [Arguments("\\\\server\\share\\file.txt")]
    [Arguments("assets//logo.png")]
    [Arguments("assets/lo\0go.png")]
    [Arguments("assets/lo:go.png")]
    [Arguments("assets/lo*go.png")]
    public async Task Parse_rejects_invalid_paths(string input)
    {
        var parsed = TypstPath.TryParse(input, out var path);
        await Assert.That(parsed).IsFalse();
        await Assert.That(path).IsNull();
        var exception = Assert.Throws<ArgumentException>(() => TypstPath.Parse(input));
        await Assert.That(exception!.ParamName).IsEqualTo("value");
    }

    [Test]
    public async Task Paths_compare_ordinally()
    {
        await Assert.That(TypstPath.Parse("/a/b.typ")).IsEqualTo(TypstPath.Parse("a\\b.typ"));
        await Assert.That(TypstPath.Parse("a/B.typ").Equals(TypstPath.Parse("a/b.typ"))).IsFalse();
    }

    [Test]
    public async Task Segments_and_file_name()
    {
        var path = TypstPath.Parse("assets/img/logo.png");
        await Assert.That(path.Segments).IsEquivalentTo(new[] { "assets", "img", "logo.png" });
        await Assert.That(path.FileName).IsEqualTo("logo.png");
        await Assert.That(path.ToString()).IsEqualTo("/assets/img/logo.png");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/Typstium.UnitTests --framework net10.0 --treenode-filter "/*/*/TypstPathTests/*"`
Expected: build FAILS with `CS0246: The type or namespace name 'TypstPath' could not be found`.

- [ ] **Step 3: Implement `TypstPath`**

`src/Typstium/Sources/TypstPath.cs`:

```csharp
using System.Diagnostics.CodeAnalysis;

namespace Typstium;

/// <summary>
/// A path relative to the Typst project root, normalized to forward slashes.
/// Cannot escape the root: absolute paths, drive letters, UNC paths and ".." segments are rejected.
/// </summary>
public sealed class TypstPath : IEquatable<TypstPath>
{
    private static readonly char[] InvalidCharacters = ['\0', ':', '*', '?', '"', '<', '>', '|'];

    private TypstPath(string value, IReadOnlyList<string> segments)
    {
        Value = value;
        Segments = segments;
    }

    /// <summary>The normalized path without a leading slash, e.g. <c>assets/logo.png</c>.</summary>
    public string Value { get; }

    /// <summary>The path in Typst's root-relative form, e.g. <c>/assets/logo.png</c>.</summary>
    public string RootRelative => "/" + Value;

    public IReadOnlyList<string> Segments { get; }

    public string FileName => Segments[Segments.Count - 1];

    public static TypstPath Parse(string value)
    {
        if (!TryParseCore(value, out var path, out var error))
        {
            throw new ArgumentException(error, nameof(value));
        }

        return path;
    }

    public static bool TryParse(string? value, [NotNullWhen(true)] out TypstPath? path)
    {
        if (TryParseCore(value, out var parsed, out _))
        {
            path = parsed;
            return true;
        }

        path = null;
        return false;
    }

    private static bool TryParseCore(string? value, [NotNullWhen(true)] out TypstPath? path, out string error)
    {
        path = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            error = "A Typst path must not be empty.";
            return false;
        }

        var text = value!.Replace('\\', '/');
        if (text.StartsWith("//", StringComparison.Ordinal))
        {
            error = $"'{value}' is a UNC path; Typst paths are relative to the project root.";
            return false;
        }

        if (text.Length >= 2 && char.IsLetter(text[0]) && text[1] == ':')
        {
            error = $"'{value}' contains a drive letter; Typst paths are relative to the project root.";
            return false;
        }

        if (text.StartsWith("/", StringComparison.Ordinal))
        {
            text = text.Substring(1);
        }

        var segments = new List<string>();
        foreach (var segment in text.Split('/'))
        {
            if (segment.Length == 0)
            {
                error = $"'{value}' contains an empty path segment.";
                return false;
            }

            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                error = $"'{value}' contains '..'; Typst paths cannot leave the project root.";
                return false;
            }

            if (segment.IndexOfAny(InvalidCharacters) >= 0)
            {
                error = $"'{value}' contains a character that isn't allowed in file names.";
                return false;
            }

            segments.Add(segment);
        }

        if (segments.Count == 0)
        {
            error = $"'{value}' doesn't name a file.";
            return false;
        }

        error = string.Empty;
        path = new TypstPath(string.Join("/", segments), segments.AsReadOnly());
        return true;
    }

    public bool Equals(TypstPath? other) => other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as TypstPath);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => RootRelative;

    public static bool operator ==(TypstPath? left, TypstPath? right) => Equals(left, right);

    public static bool operator !=(TypstPath? left, TypstPath? right) => !Equals(left, right);
}
```

- [ ] **Step 4: Run the `TypstPath` tests to verify they pass**

Run: `dotnet test --project tests/Typstium.UnitTests --framework net10.0 --treenode-filter "/*/*/TypstPathTests/*"`
Expected: PASS (all cases).

- [ ] **Step 5: Write the failing tests for the other value objects**

`tests/Typstium.UnitTests/Templates/TypstTemplateNameTests.cs`:

```csharp
namespace Typstium.UnitTests.Templates;

public class TypstTemplateNameTests
{
    [Test]
    [Arguments("license", "license", "license")]
    [Arguments("invoices/standard", "invoices/standard", "standard")]
    [Arguments("release-notes_v2.1", "release-notes_v2.1", "release-notes_v2.1")]
    public async Task Parse_accepts_valid_names(string input, string value, string lastSegment)
    {
        var name = TypstTemplateName.Parse(input);
        await Assert.That(name.Value).IsEqualTo(value);
        await Assert.That(name.LastSegment).IsEqualTo(lastSegment);
    }

    [Test]
    [Arguments("")]
    [Arguments("../license")]
    [Arguments("a/../b")]
    [Arguments("/license")]
    [Arguments("license/")]
    [Arguments("lic ense")]
    [Arguments("lic\\ense")]
    [Arguments("a//b")]
    public async Task Parse_rejects_invalid_names(string input)
    {
        var parsed = TypstTemplateName.TryParse(input, out _);
        await Assert.That(parsed).IsFalse();
    }
}
```

`tests/Typstium.UnitTests/Diagnostics/TypstDiagnosticIdTests.cs`:

```csharp
namespace Typstium.UnitTests.Diagnostics;

public class TypstDiagnosticIdTests
{
    [Test]
    public async Task Parse_accepts_the_typst_prefix_and_four_digits()
    {
        var id = TypstDiagnosticId.Parse("TYPST1001");
        await Assert.That(id.Value).IsEqualTo("TYPST1001");
        await Assert.That(id.Number).IsEqualTo(1001);
        await Assert.That(id).IsEqualTo(TypstDiagnosticId.Parse("TYPST1001"));
    }

    [Test]
    [Arguments("TYPST101")]
    [Arguments("typst1001")]
    [Arguments("CS1001")]
    [Arguments("TYPST10011")]
    public async Task Parse_rejects_other_formats(string input)
    {
        var exception = Assert.Throws<ArgumentException>(() => TypstDiagnosticId.Parse(input));
        await Assert.That(exception).IsNotNull();
    }
}
```

`tests/Typstium.UnitTests/Capabilities/TypstVersionTests.cs`:

```csharp
namespace Typstium.UnitTests.Capabilities;

public class TypstVersionTests
{
    [Test]
    [Arguments("0.15.1", 0, 15, 1)]
    [Arguments("1.0.0", 1, 0, 0)]
    [Arguments("0.16.0-rc1", 0, 16, 0)]
    public async Task Parse_reads_major_minor_patch(string input, int major, int minor, int patch)
    {
        var version = TypstVersion.Parse(input);
        await Assert.That(version).IsEqualTo(new TypstVersion(major, minor, patch));
    }

    [Test]
    public async Task Versions_compare_numerically()
    {
        var older = TypstVersion.Parse("0.9.0");
        var newer = TypstVersion.Parse("0.15.0");
        await Assert.That(older < newer).IsTrue();
        await Assert.That(newer.CompareTo(older)).IsGreaterThan(0);
        await Assert.That(newer.ToString()).IsEqualTo("0.15.0");
    }

    [Test]
    [Arguments("")]
    [Arguments("0.15")]
    [Arguments("v0.15.1")]
    [Arguments("a.b.c")]
    public async Task TryParse_rejects_malformed_versions(string input)
    {
        var parsed = TypstVersion.TryParse(input, out _);
        await Assert.That(parsed).IsFalse();
    }
}
```

- [ ] **Step 6: Run the tests to verify they fail**

Run: `dotnet test --project tests/Typstium.UnitTests --framework net10.0`
Expected: build FAILS with `CS0246` for `TypstTemplateName`, `TypstDiagnosticId` and `TypstVersion`.

- [ ] **Step 7: Implement the remaining value objects**

`src/Typstium/Templates/TypstTemplateName.cs`:

```csharp
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Typstium;

/// <summary>A template name: segments of <c>[A-Za-z0-9._-]</c> separated by <c>/</c>, without "..".</summary>
public sealed class TypstTemplateName : IEquatable<TypstTemplateName>
{
    private static readonly Regex Segment = new("^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant);

    private TypstTemplateName(string value) => Value = value;

    public string Value { get; }

    public string LastSegment => Value.Substring(Value.LastIndexOf('/') + 1);

    public static TypstTemplateName Parse(string value) =>
        TryParse(value, out var name)
            ? name
            : throw new ArgumentException($"'{value}' is not a valid template name. Use segments of letters, digits, '.', '_' and '-' separated by '/'.", nameof(value));

    public static bool TryParse(string? value, [NotNullWhen(true)] out TypstTemplateName? name)
    {
        name = null;
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        foreach (var segment in value!.Split('/'))
        {
            if (segment is "." or ".." || !Segment.IsMatch(segment))
            {
                return false;
            }
        }

        name = new TypstTemplateName(value);
        return true;
    }

    public bool Equals(TypstTemplateName? other) => other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as TypstTemplateName);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => Value;
}
```

`src/Typstium/Templates/TypstTemplateVersion.cs`:

```csharp
namespace Typstium;

/// <summary>An opaque template version: a content hash or a provider ETag. Compared ordinally.</summary>
public sealed class TypstTemplateVersion : IEquatable<TypstTemplateVersion>
{
    public TypstTemplateVersion(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public bool Equals(TypstTemplateVersion? other) => other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as TypstTemplateVersion);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => Value;
}
```

`src/Typstium/Diagnostics/TypstDiagnosticId.cs`:

```csharp
using System.Globalization;
using System.Text.RegularExpressions;

namespace Typstium;

/// <summary>A stable diagnostic code such as <c>TYPST1001</c>. Typst has no codes; Typstium assigns them.</summary>
public sealed class TypstDiagnosticId : IEquatable<TypstDiagnosticId>
{
    private static readonly Regex Format = new("^TYPST[0-9]{4}$", RegexOptions.CultureInvariant);

    private TypstDiagnosticId(string value) => Value = value;

    public string Value { get; }

    public int Number => int.Parse(Value.Substring(5), CultureInfo.InvariantCulture);

    public static TypstDiagnosticId Parse(string value)
    {
        if (value is null || !Format.IsMatch(value))
        {
            throw new ArgumentException($"'{value}' is not a diagnostic id. Expected TYPST followed by four digits.", nameof(value));
        }

        return new TypstDiagnosticId(value);
    }

    public bool Equals(TypstDiagnosticId? other) => other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as TypstDiagnosticId);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => Value;
}
```

`src/Typstium/Diagnostics/TypstPosition.cs`:

```csharp
namespace Typstium;

/// <summary>A 1-based line and column, as used by MSBuild and editors.</summary>
public readonly record struct TypstPosition(int Line, int Column)
{
    public override string ToString() => $"{Line},{Column}";
}
```

`src/Typstium/Capabilities/TypstVersion.cs`:

```csharp
using System.Globalization;

namespace Typstium;

/// <summary>A Typst version, compared numerically. Pre-release suffixes are ignored.</summary>
public readonly record struct TypstVersion(int Major, int Minor, int Patch) : IComparable<TypstVersion>
{
    public static TypstVersion Parse(string value) =>
        TryParse(value, out var version)
            ? version
            : throw new FormatException($"'{value}' is not a Typst version (expected major.minor.patch).");

    public static bool TryParse(string? value, out TypstVersion version)
    {
        version = default;
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var core = value!;
        var dash = core.IndexOf('-');
        if (dash >= 0)
        {
            core = core.Substring(0, dash);
        }

        var parts = core.Split('.');
        if (parts.Length != 3
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minor)
            || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var patch))
        {
            return false;
        }

        version = new TypstVersion(major, minor, patch);
        return true;
    }

    public int CompareTo(TypstVersion other)
    {
        var major = Major.CompareTo(other.Major);
        if (major != 0)
        {
            return major;
        }

        var minor = Minor.CompareTo(other.Minor);
        return minor != 0 ? minor : Patch.CompareTo(other.Patch);
    }

    public static bool operator <(TypstVersion left, TypstVersion right) => left.CompareTo(right) < 0;

    public static bool operator >(TypstVersion left, TypstVersion right) => left.CompareTo(right) > 0;

    public static bool operator <=(TypstVersion left, TypstVersion right) => left.CompareTo(right) <= 0;

    public static bool operator >=(TypstVersion left, TypstVersion right) => left.CompareTo(right) >= 0;

    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}
```

- [ ] **Step 8: Run all unit tests**

Run: `dotnet test --project tests/Typstium.UnitTests`
Expected: PASS on every target framework.

- [ ] **Step 9: Commit**

```bash
git add src tests
git commit -m "Add TypstPath, template name/version, diagnostic id, position and version value objects

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task 3: Command-line arguments (`TypstArguments`)

The immutable argument list with secret redaction (spec 4.8), plus the Windows escaping rules used to
render it on `netstandard2.0`.

**Files:**
- Create: `src/Typstium/CommandLine/TypstArgument.cs`, `src/Typstium/CommandLine/TypstArguments.cs`, `src/Typstium/CommandLine/WindowsArgumentEscaper.cs`
- Test: `tests/Typstium.UnitTests/CommandLine/TypstArgumentsTests.cs`, `tests/Typstium.UnitTests/CommandLine/WindowsArgumentEscaperTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `public sealed class TypstArgument`: `static Plain(string)`, `static Secret(string)`, `string Value`, `bool IsSecret`
  - `public sealed class TypstArguments : IReadOnlyList<TypstArgument>, IEquatable<TypstArguments>`:
    - `static Empty`, `static Of(params string[])`
    - `Add(string)`, `AddSecret(string)`, `AddSwitch(string name, string value)`, `AddSwitchSecret(string name, string value)`, `AddRange(TypstArguments)`
    - `Insert(int, TypstArgument)`, `RemoveAt(int)`, `SetItem(int, TypstArgument)`, `IndexOf(string)`
    - `Render()`, `RenderSafe()`, `internal IReadOnlyList<string> ToValues()`
  - `internal static class WindowsArgumentEscaper`: `string Escape(string)`, `string Join(IEnumerable<string>)`

- [ ] **Step 1: Write the failing tests**

`tests/Typstium.UnitTests/CommandLine/TypstArgumentsTests.cs`:

```csharp
namespace Typstium.UnitTests.CommandLine;

public class TypstArgumentsTests
{
    [Test]
    public async Task Builder_methods_return_new_instances()
    {
        var empty = TypstArguments.Empty;
        var one = empty.Add("compile");
        await Assert.That(empty.Count).IsEqualTo(0);
        await Assert.That(one.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Switches_become_two_items_and_secrets_are_redacted()
    {
        var args = TypstArguments.Empty
            .AddSwitch("--root", "C:\\work dir")
            .AddSwitchSecret("--input", "license={\"id\":1}")
            .AddSecret("token");

        await Assert.That(args.ToValues()).IsEquivalentTo(new[] { "--root", "C:\\work dir", "--input", "license={\"id\":1}", "token" });
        await Assert.That(args.RenderSafe()).IsEqualTo("--root \"C:\\work dir\" --input [REDACTED] [REDACTED]");
        await Assert.That(args.Render()).Contains("license=");
    }

    [Test]
    public async Task Insert_remove_set_and_index_of()
    {
        var args = TypstArguments.Of("compile", "--root", "a", "main.typ");
        var index = args.IndexOf("--root");
        var changed = args.SetItem(index + 1, TypstArgument.Plain("b")).RemoveAt(0).Insert(0, TypstArgument.Plain("c"));
        await Assert.That(index).IsEqualTo(1);
        await Assert.That(changed.ToValues()).IsEquivalentTo(new[] { "c", "--root", "b", "main.typ" });
    }

    [Test]
    public async Task AddSwitch_requires_a_dash_prefixed_name()
    {
        var exception = Assert.Throws<ArgumentException>(() => TypstArguments.Empty.AddSwitch("root", "x"));
        await Assert.That(exception!.ParamName).IsEqualTo("name");
    }

    [Test]
    public async Task Equality_is_by_sequence()
    {
        var left = TypstArguments.Of("a", "b");
        var right = TypstArguments.Empty.Add("a").Add("b");
        await Assert.That(left).IsEqualTo(right);
        await Assert.That(left.Equals(TypstArguments.Empty.Add("a").AddSecret("b"))).IsFalse();
    }
}
```

`tests/Typstium.UnitTests/CommandLine/WindowsArgumentEscaperTests.cs`. On Windows it also round-trips
through the real Windows parser (`CommandLineToArgvW`). This pins Review Focus 1 (spaces, quotes,
trailing backslashes, non-ASCII):

```csharp
using System.Runtime.InteropServices;
using Typstium.CommandLine;

namespace Typstium.UnitTests.CommandLine;

public class WindowsArgumentEscaperTests
{
    [Test]
    [Arguments("simple", "simple")]
    [Arguments("", "\"\"")]
    [Arguments("with space", "\"with space\"")]
    [Arguments("quote\"inside", "\"quote\\\"inside\"")]
    [Arguments("C:\\Program Files\\", "\"C:\\Program Files\\\\\"")]
    [Arguments("a\\\\b", "a\\\\b")]
    [Arguments("tab\tinside", "\"tab\tinside\"")]
    public async Task Escape_follows_windows_rules(string input, string expected)
    {
        await Assert.That(WindowsArgumentEscaper.Escape(input)).IsEqualTo(expected);
    }

    public static IEnumerable<Func<string[]>> RoundTripCases()
    {
        yield return () => ["compile", "--root", @"C:\Users\Åsa Ström\AppData\Local\Temp\typstium\ws-1\root"];
        yield return () => ["--input", "data={\"name\":\"a \\\"quoted\\\" value\"}"];
        yield return () => [@"C:\trailing\", @"two\\", "", " ", "\"", "ends with space "];
    }

    [Test]
    [MethodDataSource(nameof(RoundTripCases))]
    public async Task Join_round_trips_through_the_windows_parser(string[] arguments)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Skip.Test("CommandLineToArgvW is Windows-only.");
        }

        var commandLine = "typst.exe " + WindowsArgumentEscaper.Join(arguments);
        var parsed = ParseWithWindows(commandLine).Skip(1).ToArray();
        await Assert.That(parsed).IsEquivalentTo(arguments);
    }

    private static string[] ParseWithWindows(string commandLine)
    {
        var pointer = CommandLineToArgvW(commandLine, out var count);
        try
        {
            var result = new string[count];
            for (var i = 0; i < count; i++)
            {
                result[i] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(pointer, i * IntPtr.Size))!;
            }

            return result;
        }
        finally
        {
            LocalFree(pointer);
        }
    }

    [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CommandLineToArgvW(string commandLine, out int count);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/Typstium.UnitTests --framework net10.0 --treenode-filter "/*/*/TypstArgumentsTests/*"`
Expected: build FAILS with `CS0246` for `TypstArguments`.

- [ ] **Step 3: Implement**

`src/Typstium/CommandLine/TypstArgument.cs`:

```csharp
namespace Typstium;

/// <summary>One command-line argument. Secret arguments are redacted by <see cref="TypstArguments.RenderSafe"/>.</summary>
public sealed class TypstArgument : IEquatable<TypstArgument>
{
    private TypstArgument(string value, bool isSecret)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
        IsSecret = isSecret;
    }

    public string Value { get; }

    public bool IsSecret { get; }

    public static TypstArgument Plain(string value) => new(value, isSecret: false);

    public static TypstArgument Secret(string value) => new(value, isSecret: true);

    public bool Equals(TypstArgument? other) =>
        other is not null && IsSecret == other.IsSecret && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as TypstArgument);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value) ^ (IsSecret ? 1 : 0);

    public override string ToString() => IsSecret ? TypstArguments.Redacted : Value;
}
```

`src/Typstium/CommandLine/TypstArguments.cs`:

```csharp
using System.Collections;
using System.Collections.Immutable;
using Typstium.CommandLine;

namespace Typstium;

/// <summary>
/// An immutable list of command-line arguments. A list, not a string: each item reaches Typst
/// unchanged, so no quoting variants are needed.
/// </summary>
public sealed class TypstArguments : IReadOnlyList<TypstArgument>, IEquatable<TypstArguments>
{
    internal const string Redacted = "[REDACTED]";

    private readonly ImmutableArray<TypstArgument> _items;

    private TypstArguments(ImmutableArray<TypstArgument> items) => _items = items;

    public static TypstArguments Empty { get; } = new(ImmutableArray<TypstArgument>.Empty);

    public static TypstArguments Of(params string[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new TypstArguments(values.Select(TypstArgument.Plain).ToImmutableArray());
    }

    public int Count => _items.Length;

    public TypstArgument this[int index] => _items[index];

    public TypstArguments Add(string value) => new(_items.Add(TypstArgument.Plain(value)));

    public TypstArguments AddSecret(string value) => new(_items.Add(TypstArgument.Secret(value)));

    public TypstArguments AddSwitch(string name, string value) => AddSwitchCore(name, TypstArgument.Plain(value));

    public TypstArguments AddSwitchSecret(string name, string value) => AddSwitchCore(name, TypstArgument.Secret(value));

    public TypstArguments AddRange(TypstArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return new TypstArguments(_items.AddRange(arguments._items));
    }

    public TypstArguments Insert(int index, TypstArgument argument)
    {
        ArgumentNullException.ThrowIfNull(argument);
        return new TypstArguments(_items.Insert(index, argument));
    }

    public TypstArguments RemoveAt(int index) => new(_items.RemoveAt(index));

    public TypstArguments SetItem(int index, TypstArgument argument)
    {
        ArgumentNullException.ThrowIfNull(argument);
        return new TypstArguments(_items.SetItem(index, argument));
    }

    /// <summary>The index of the first argument with this exact value, or -1.</summary>
    public int IndexOf(string value)
    {
        for (var i = 0; i < _items.Length; i++)
        {
            if (string.Equals(_items[i].Value, value, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Renders the arguments for display, including secrets. Not used to start processes.</summary>
    public string Render() => WindowsArgumentEscaper.Join(_items.Select(item => item.Value));

    /// <summary>Renders the arguments for logs, with secrets replaced by [REDACTED].</summary>
    public string RenderSafe() =>
        string.Join(" ", _items.Select(item => item.IsSecret ? Redacted : WindowsArgumentEscaper.Escape(item.Value)));

    internal IReadOnlyList<string> ToValues() => _items.Select(item => item.Value).ToArray();

    public IEnumerator<TypstArgument> GetEnumerator() => ((IEnumerable<TypstArgument>)_items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Equals(TypstArguments? other) => other is not null && _items.SequenceEqual(other._items);

    public override bool Equals(object? obj) => Equals(obj as TypstArguments);

    public override int GetHashCode()
    {
        var hash = 17;
        foreach (var item in _items)
        {
            hash = unchecked((hash * 31) + item.GetHashCode());
        }

        return hash;
    }

    public override string ToString() => RenderSafe();

    private TypstArguments AddSwitchCore(string name, TypstArgument value)
    {
        if (string.IsNullOrEmpty(name) || !name.StartsWith("-", StringComparison.Ordinal))
        {
            throw new ArgumentException("A switch name must start with '-', for example '--root'.", nameof(name));
        }

        return new TypstArguments(_items.Add(TypstArgument.Plain(name)).Add(value));
    }
}
```

`src/Typstium/CommandLine/WindowsArgumentEscaper.cs`:

```csharp
using System.Text;

namespace Typstium.CommandLine;

/// <summary>
/// Quotes arguments using the MSVCRT/CommandLineToArgvW rules, which .NET also uses to split
/// ProcessStartInfo.Arguments on every platform. Used on netstandard2.0 (no ArgumentList) and for display.
/// </summary>
internal static class WindowsArgumentEscaper
{
    private static readonly char[] NeedsQuoting = [' ', '\t', '\n', '\v', '"'];

    public static string Join(IEnumerable<string> arguments) => string.Join(" ", arguments.Select(Escape));

    public static string Escape(string argument)
    {
        if (argument.Length > 0 && argument.IndexOfAny(NeedsQuoting) < 0)
        {
            return argument;
        }

        var builder = new StringBuilder(argument.Length + 2);
        builder.Append('"');
        for (var i = 0; i < argument.Length; i++)
        {
            var backslashes = 0;
            while (i < argument.Length && argument[i] == '\\')
            {
                backslashes++;
                i++;
            }

            if (i == argument.Length)
            {
                // Backslashes before the closing quote must be doubled.
                builder.Append('\\', backslashes * 2);
                break;
            }

            if (argument[i] == '"')
            {
                builder.Append('\\', (backslashes * 2) + 1);
                builder.Append('"');
            }
            else
            {
                builder.Append('\\', backslashes);
                builder.Append(argument[i]);
            }
        }

        builder.Append('"');
        return builder.ToString();
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --project tests/Typstium.UnitTests --treenode-filter "/*/*/TypstArgumentsTests/*"`
Then: `dotnet test --project tests/Typstium.UnitTests --treenode-filter "/*/*/WindowsArgumentEscaperTests/*"`
Expected: PASS. On Linux the round-trip cases are reported as skipped.

- [ ] **Step 5: Commit**

```bash
git add src tests
git commit -m "Add TypstArguments with secret redaction and Windows argument escaping

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Diagnostics model and process exceptions

The public diagnostic types (spec 4.12) and the exception hierarchy that doesn't depend on later types
(spec 4.11). `TypstCompileException` comes in Task 8, and `TypstExecutableNotFoundException` in Task 17.

**Files:**
- Create: `src/Typstium/Diagnostics/TypstDiagnosticSeverity.cs`, `TypstSourceFile.cs`, `TypstLocation.cs`, `TypstTraceFrame.cs`, `TypstDiagnostic.cs` (all under `src/Typstium/Diagnostics/`)
- Create: `src/Typstium/Exceptions/TypstException.cs`, `TypstFeatureNotSupportedException.cs`, `TypstInvocationException.cs`, `TypstProcessException.cs`, `TypstProcessStartException.cs`, `TypstProcessCrashedException.cs`, `TypstProcessOutputException.cs` (all under `src/Typstium/Exceptions/`)
- Test: `tests/Typstium.UnitTests/Diagnostics/TypstDiagnosticTests.cs`, `tests/Typstium.UnitTests/Exceptions/ExceptionTests.cs`

**Interfaces:**
- Consumes: `TypstPath`, `TypstDiagnosticId`, `TypstPosition`, `TypstVersion` (Task 2).
- Produces:
  - `enum TypstDiagnosticSeverity { Error, Warning }`
  - `abstract record TypstSourceFile` with nested `Project(TypstPath Path, string? FullPath)`, `Virtual(TypstPath Path)`, `Package(string Spec, TypstPath Path)`, `Markup` (singleton `TypstSourceFile.Markup.Instance`), and `string DisplayPath`
  - `sealed record TypstLocation(TypstSourceFile File, TypstPosition Start, TypstPosition? End)`
  - `sealed record TypstTraceFrame(string Message, TypstLocation? Location)`
  - `sealed record TypstDiagnostic`: constructor `(TypstDiagnosticId id, TypstDiagnosticSeverity severity, string message, TypstLocation? location, ImmutableArray<string> hints, ImmutableArray<TypstTraceFrame> trace)`, properties of the same names, `WithHints(IEnumerable<string>)`, `ToString()`
  - `abstract class TypstException : Exception`
  - `TypstFeatureNotSupportedException(string feature, TypstVersion installedVersion, TypstVersion? requiredVersion)`
  - `TypstInvocationException(string commandLine, int exitCode, string standardError)`
  - `abstract class TypstProcessException : TypstException`
  - `TypstProcessStartException(string executablePath, Exception innerException)`
  - `TypstProcessCrashedException(int exitCode, string standardError)`
  - `TypstProcessOutputException(string description)` and `(string description, Exception innerException)`

- [ ] **Step 1: Write the failing tests**

`tests/Typstium.UnitTests/Diagnostics/TypstDiagnosticTests.cs`:

```csharp
using System.Collections.Immutable;

namespace Typstium.UnitTests.Diagnostics;

public class TypstDiagnosticTests
{
    private static readonly TypstDiagnosticId UnknownVariable = TypstDiagnosticId.Parse("TYPST1001");

    [Test]
    public async Task ToString_uses_the_msbuild_canonical_shape_with_the_full_path_when_on_disk()
    {
        var file = new TypstSourceFile.Project(TypstPath.Parse("docs/spec.typ"), "C:\\repo\\docs\\spec.typ");
        var diagnostic = new TypstDiagnostic(UnknownVariable, TypstDiagnosticSeverity.Error, "unknown variable: x",
            new TypstLocation(file, new TypstPosition(3, 2), null), ImmutableArray<string>.Empty, ImmutableArray<TypstTraceFrame>.Empty);

        await Assert.That(diagnostic.ToString()).IsEqualTo("C:\\repo\\docs\\spec.typ(3,2): error TYPST1001: unknown variable: x");
    }

    [Test]
    public async Task ToString_uses_display_paths_for_sandboxed_files_and_omits_missing_locations()
    {
        var located = new TypstDiagnostic(UnknownVariable, TypstDiagnosticSeverity.Warning, "w",
            new TypstLocation(new TypstSourceFile.Virtual(TypstPath.Parse("data.json")), new TypstPosition(1, 1), null),
            default, default);
        var unlocated = new TypstDiagnostic(UnknownVariable, TypstDiagnosticSeverity.Error, "e", null, default, default);

        await Assert.That(located.ToString()).IsEqualTo("/data.json(1,1): warning TYPST1001: w");
        await Assert.That(unlocated.ToString()).IsEqualTo("error TYPST1001: e");
        await Assert.That(unlocated.Hints.IsDefault).IsFalse();
        await Assert.That(unlocated.Trace.IsDefault).IsFalse();
    }

    [Test]
    public async Task Display_paths_per_source_kind()
    {
        var package = new TypstSourceFile.Package("@preview/cmarker:0.1.10", TypstPath.Parse("lib.typ"));
        await Assert.That(package.DisplayPath).IsEqualTo("@preview/cmarker:0.1.10/lib.typ");
        await Assert.That(TypstSourceFile.Markup.Instance.DisplayPath).IsEqualTo("<markup>");
    }

    [Test]
    public async Task WithHints_appends()
    {
        var diagnostic = new TypstDiagnostic(UnknownVariable, TypstDiagnosticSeverity.Error, "e", null, ImmutableArray.Create("a"), default);
        var extended = diagnostic.WithHints(new[] { "b" });
        await Assert.That(extended.Hints).IsEquivalentTo(new[] { "a", "b" });
    }
}
```

`tests/Typstium.UnitTests/Exceptions/ExceptionTests.cs`:

```csharp
using System.ComponentModel;

namespace Typstium.UnitTests.Exceptions;

public class ExceptionTests
{
    [Test]
    public async Task Process_exceptions_share_the_typst_base()
    {
        Exception start = new TypstProcessStartException("C:\\typst.exe", new Win32Exception(5));
        Exception crashed = new TypstProcessCrashedException(101, "thread 'main' panicked");
        Exception output = new TypstProcessOutputException("missing page file");

        await Assert.That(start is TypstProcessException && start is TypstException).IsTrue();
        await Assert.That(crashed is TypstProcessException).IsTrue();
        await Assert.That(output is TypstProcessException).IsTrue();
    }

    [Test]
    public async Task Exceptions_carry_their_data()
    {
        var crashed = new TypstProcessCrashedException(101, "panic");
        var invocation = new TypstInvocationException("typst --bad", 2, "error: unexpected argument");
        var feature = new TypstFeatureNotSupportedException("html", new TypstVersion(0, 14, 0), new TypstVersion(0, 15, 0));

        await Assert.That(crashed.ExitCode).IsEqualTo(101);
        await Assert.That(crashed.Message).Contains("101");
        await Assert.That(invocation.CommandLine).IsEqualTo("typst --bad");
        await Assert.That(invocation.Message).Contains("bug in Typstium");
        await Assert.That(feature.InstalledVersion).IsEqualTo(new TypstVersion(0, 14, 0));
        await Assert.That(feature.Message).Contains("0.15.0");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/Typstium.UnitTests --framework net10.0 --treenode-filter "/*/*/TypstDiagnosticTests/*"`
Expected: build FAILS with `CS0246` for `TypstDiagnostic`.

- [ ] **Step 3: Implement the diagnostics model**

`src/Typstium/Diagnostics/TypstDiagnosticSeverity.cs`:

```csharp
namespace Typstium;

public enum TypstDiagnosticSeverity
{
    Error,
    Warning,
}
```

`src/Typstium/Diagnostics/TypstSourceFile.cs`:

```csharp
namespace Typstium;

/// <summary>Where a diagnostic's file came from. Sandbox paths never appear here.</summary>
public abstract record TypstSourceFile
{
    private TypstSourceFile()
    {
    }

    /// <summary>The text used in messages, e.g. <c>/license.typ</c> or <c>@preview/cmarker:0.1.10/lib.typ</c>.</summary>
    public abstract string DisplayPath { get; }

    /// <summary>A file of the project: a file-source root or a template bundle.</summary>
    /// <param name="Path">Root-relative path.</param>
    /// <param name="FullPath">Absolute path on disk; set only for file sources compiled in place.</param>
    public sealed record Project(TypstPath Path, string? FullPath) : TypstSourceFile
    {
        public override string DisplayPath => FullPath ?? Path.RootRelative;
    }

    /// <summary>A <c>TypstVirtualFile</c> from the request.</summary>
    public sealed record Virtual(TypstPath Path) : TypstSourceFile
    {
        public override string DisplayPath => Path.RootRelative;
    }

    /// <summary>A file inside a Typst package, e.g. spec <c>@preview/cmarker:0.1.10</c>, path <c>lib.typ</c>.</summary>
    public sealed record Package(string Spec, TypstPath Path) : TypstSourceFile
    {
        public override string DisplayPath => Spec + Path.RootRelative;
    }

    /// <summary>Inline markup from <c>TypstSource.Markup</c>.</summary>
    public sealed record Markup : TypstSourceFile
    {
        private Markup()
        {
        }

        public static Markup Instance { get; } = new();

        public override string DisplayPath => "<markup>";
    }
}
```

`src/Typstium/Diagnostics/TypstLocation.cs`:

```csharp
namespace Typstium;

/// <param name="End">Inclusive end position, when Typst reports a single-line span.</param>
public sealed record TypstLocation(TypstSourceFile File, TypstPosition Start, TypstPosition? End);
```

`src/Typstium/Diagnostics/TypstTraceFrame.cs`:

```csharp
namespace Typstium;

/// <summary>One "while calling …" frame, innermost first.</summary>
public sealed record TypstTraceFrame(string Message, TypstLocation? Location);
```

`src/Typstium/Diagnostics/TypstDiagnostic.cs`:

```csharp
using System.Collections.Immutable;
using System.Text;

namespace Typstium;

public sealed record TypstDiagnostic
{
    public TypstDiagnostic(
        TypstDiagnosticId id,
        TypstDiagnosticSeverity severity,
        string message,
        TypstLocation? location,
        ImmutableArray<string> hints,
        ImmutableArray<TypstTraceFrame> trace)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Severity = severity;
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Location = location;
        Hints = hints.IsDefault ? ImmutableArray<string>.Empty : hints;
        Trace = trace.IsDefault ? ImmutableArray<TypstTraceFrame>.Empty : trace;
    }

    public TypstDiagnosticId Id { get; }

    public TypstDiagnosticSeverity Severity { get; }

    public string Message { get; }

    public TypstLocation? Location { get; }

    public ImmutableArray<string> Hints { get; }

    public ImmutableArray<TypstTraceFrame> Trace { get; }

    public TypstDiagnostic WithHints(IEnumerable<string> additionalHints) =>
        new(Id, Severity, Message, Location, Hints.AddRange(additionalHints), Trace);

    /// <summary>MSBuild canonical form: <c>path(line,col): error TYPST1001: message</c>.</summary>
    public override string ToString()
    {
        var builder = new StringBuilder();
        if (Location is not null)
        {
            builder.Append(Location.File.DisplayPath)
                .Append('(').Append(Location.Start.Line).Append(',').Append(Location.Start.Column).Append("): ");
        }

        builder.Append(Severity == TypstDiagnosticSeverity.Error ? "error" : "warning")
            .Append(' ').Append(Id.Value).Append(": ").Append(Message);
        return builder.ToString();
    }
}
```

- [ ] **Step 4: Implement the exceptions**

`src/Typstium/Exceptions/TypstException.cs`:

```csharp
namespace Typstium;

/// <summary>
/// Base class for failures caused by the environment or by Typstium, never by the template.
/// Template failures are results (<c>TypstCompileResult</c>).
/// </summary>
public abstract class TypstException : Exception
{
    protected TypstException(string message)
        : base(message)
    {
    }

    protected TypstException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
```

`src/Typstium/Exceptions/TypstFeatureNotSupportedException.cs`:

```csharp
namespace Typstium;

/// <summary>The installed Typst is too old for the request, or lacks a feature it needs.</summary>
public sealed class TypstFeatureNotSupportedException : TypstException
{
    public TypstFeatureNotSupportedException(string feature, TypstVersion installedVersion, TypstVersion? requiredVersion)
        : base(BuildMessage(feature, installedVersion, requiredVersion))
    {
        Feature = feature;
        InstalledVersion = installedVersion;
        RequiredVersion = requiredVersion;
    }

    public string Feature { get; }

    public TypstVersion InstalledVersion { get; }

    public TypstVersion? RequiredVersion { get; }

    private static string BuildMessage(string feature, TypstVersion installed, TypstVersion? required) =>
        required is { } version
            ? $"'{feature}' requires Typst {version} or newer, but Typst {installed} is installed."
            : $"'{feature}' is not supported by the installed Typst {installed}.";
}
```

`src/Typstium/Exceptions/TypstInvocationException.cs`:

```csharp
namespace Typstium;

/// <summary>Typst rejected the command line (exit code 2). This is a Typstium bug or an ArgumentCustomization mistake.</summary>
public sealed class TypstInvocationException : TypstException
{
    public TypstInvocationException(string commandLine, int exitCode, string standardError)
        : base($"Typst rejected the command line (exit code {exitCode}). This is a bug in Typstium or a mistake in ArgumentCustomization/ExtraArguments.{Environment.NewLine}Command: {commandLine}{Environment.NewLine}{standardError}")
    {
        CommandLine = commandLine;
        ExitCode = exitCode;
        StandardError = standardError;
    }

    /// <summary>The command line with secrets redacted.</summary>
    public string CommandLine { get; }

    public int ExitCode { get; }

    public string StandardError { get; }
}
```

`src/Typstium/Exceptions/TypstProcessException.cs`:

```csharp
namespace Typstium;

/// <summary>The Typst process misbehaved: it couldn't start, crashed, or produced unreadable output.</summary>
public abstract class TypstProcessException : TypstException
{
    protected TypstProcessException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
```

`src/Typstium/Exceptions/TypstProcessStartException.cs`:

```csharp
namespace Typstium;

/// <summary>The Typst process could not be started. Fix the configuration; retrying won't help.</summary>
public sealed class TypstProcessStartException : TypstProcessException
{
    public TypstProcessStartException(string executablePath, Exception innerException)
        : base($"Could not start Typst at '{executablePath}': {innerException.Message} Check that the file exists, is executable and matches this machine's architecture.", innerException)
    {
        ExecutablePath = executablePath;
    }

    public string ExecutablePath { get; }
}
```

`src/Typstium/Exceptions/TypstProcessCrashedException.cs`:

```csharp
namespace Typstium;

/// <summary>Typst exited with an unexpected code, e.g. 101 for a Rust panic or 137 when killed for memory.</summary>
public sealed class TypstProcessCrashedException : TypstProcessException
{
    public TypstProcessCrashedException(int exitCode, string standardError)
        : base($"Typst exited unexpectedly with code {exitCode}.{Environment.NewLine}{standardError}", innerException: null)
    {
        ExitCode = exitCode;
        StandardError = standardError;
    }

    public int ExitCode { get; }

    public string StandardError { get; }
}
```

`src/Typstium/Exceptions/TypstProcessOutputException.cs`:

```csharp
namespace Typstium;

/// <summary>Typst's output could not be read, which points to a Typst/Typstium version mismatch.</summary>
public sealed class TypstProcessOutputException : TypstProcessException
{
    public TypstProcessOutputException(string description)
        : this(description, innerException: null)
    {
    }

    public TypstProcessOutputException(string description, Exception? innerException)
        : base($"Typst produced output that Typstium could not read: {description}", innerException)
    {
        Description = description;
    }

    public string Description { get; }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test --project tests/Typstium.UnitTests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src tests
git commit -m "Add diagnostics model and process exception hierarchy

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Template abstraction

The provider interface and the template bundle (spec 4.9). The concrete providers come in Task 19.

**Files:**
- Create: `src/Typstium/Templates/ITemplateProvider.cs`, `src/Typstium/Templates/TypstTemplateFile.cs`, `src/Typstium/Templates/TypstTemplate.cs`
- Create: `tests/Typstium.Testing/Fakes/InMemoryTemplateProvider.cs`
- Test: `tests/Typstium.UnitTests/Templates/TypstTemplateTests.cs`

**Interfaces:**
- Consumes: `TypstPath`, `TypstTemplateName`, `TypstTemplateVersion` (Task 2).
- Produces:
  - `public interface ITemplateProvider { ValueTask<TypstTemplate?> GetTemplateAsync(TypstTemplateName name, CancellationToken cancellationToken); }`
  - `public abstract class TypstTemplateFile`: `protected TypstTemplateFile(TypstPath path)`, `TypstPath Path`, `abstract ValueTask<Stream> OpenReadAsync(CancellationToken)`, `static FromBytes(TypstPath, ReadOnlyMemory<byte>)`, `static FromFile(TypstPath, string physicalPath)`
  - `public sealed class TypstTemplate`: constructor `(TypstTemplateName name, TypstPath mainFile, TypstTemplateVersion version, IReadOnlyList<TypstTemplateFile> files)`; properties `Name`, `MainFile`, `Version`, `Files`
  - Test fake `Typstium.Testing.Fakes.InMemoryTemplateProvider : ITemplateProvider`: `Add(string name, string mainFile, params (string Path, string Content)[] files)` returns `this`; `int RequestCount`

- [ ] **Step 1: Write the failing tests**

`tests/Typstium.UnitTests/Templates/TypstTemplateTests.cs`:

```csharp
using System.Text;

namespace Typstium.UnitTests.Templates;

public class TypstTemplateTests
{
    private static readonly TypstTemplateName Name = TypstTemplateName.Parse("license");
    private static readonly TypstTemplateVersion Version = new("v1");

    [Test]
    public async Task A_valid_bundle_keeps_its_files()
    {
        var main = TypstTemplateFile.FromBytes(TypstPath.Parse("license.typ"), Encoding.UTF8.GetBytes("= Hi"));
        var logo = TypstTemplateFile.FromBytes(TypstPath.Parse("logo.svg"), new byte[] { 1, 2 });

        var template = new TypstTemplate(Name, TypstPath.Parse("license.typ"), Version, new[] { main, logo });

        await Assert.That(template.Files.Count).IsEqualTo(2);
        await Assert.That(template.MainFile).IsEqualTo(TypstPath.Parse("/license.typ"));
    }

    [Test]
    public async Task The_main_file_must_be_part_of_the_bundle()
    {
        var logo = TypstTemplateFile.FromBytes(TypstPath.Parse("logo.svg"), new byte[] { 1 });
        var exception = Assert.Throws<ArgumentException>(() => new TypstTemplate(Name, TypstPath.Parse("license.typ"), Version, new[] { logo }));
        await Assert.That(exception!.ParamName).IsEqualTo("mainFile");
    }

    [Test]
    public async Task Paths_differing_only_by_case_are_rejected()
    {
        var a = TypstTemplateFile.FromBytes(TypstPath.Parse("Logo.svg"), new byte[] { 1 });
        var b = TypstTemplateFile.FromBytes(TypstPath.Parse("logo.svg"), new byte[] { 2 });
        var main = TypstTemplateFile.FromBytes(TypstPath.Parse("license.typ"), new byte[] { 3 });
        var exception = Assert.Throws<ArgumentException>(() => new TypstTemplate(Name, TypstPath.Parse("license.typ"), Version, new[] { main, a, b }));
        await Assert.That(exception!.Message).Contains("differ only by case");
    }

    [Test]
    public async Task Files_open_their_content()
    {
        var temp = Path.Combine(Path.GetTempPath(), "typstium-test-" + Guid.NewGuid().ToString("N") + ".typ");
        File.WriteAllText(temp, "from disk");
        try
        {
            var fromFile = TypstTemplateFile.FromFile(TypstPath.Parse("a.typ"), temp);
            var fromBytes = TypstTemplateFile.FromBytes(TypstPath.Parse("b.typ"), Encoding.UTF8.GetBytes("from memory"));

            await Assert.That(await ReadAsync(fromFile)).IsEqualTo("from disk");
            await Assert.That(await ReadAsync(fromBytes)).IsEqualTo("from memory");
        }
        finally
        {
            File.Delete(temp);
        }
    }

    private static async Task<string> ReadAsync(TypstTemplateFile file)
    {
        using var stream = await file.OpenReadAsync(CancellationToken.None);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/Typstium.UnitTests --framework net10.0 --treenode-filter "/*/*/TypstTemplateTests/*"`
Expected: build FAILS with `CS0246` for `TypstTemplate`.

- [ ] **Step 3: Implement**

`src/Typstium/Templates/ITemplateProvider.cs`:

```csharp
namespace Typstium;

/// <summary>Supplies named template bundles: a main file plus every file it may reference.</summary>
public interface ITemplateProvider
{
    /// <returns>The template, or <c>null</c> when no template has this name.</returns>
    ValueTask<TypstTemplate?> GetTemplateAsync(TypstTemplateName name, CancellationToken cancellationToken);
}
```

`src/Typstium/Templates/TypstTemplateFile.cs`:

```csharp
namespace Typstium;

/// <summary>One file of a template bundle. Derive from it to stream content from a database or blob store.</summary>
public abstract class TypstTemplateFile
{
    protected TypstTemplateFile(TypstPath path) => Path = path ?? throw new ArgumentNullException(nameof(path));

    public TypstPath Path { get; }

    public abstract ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken);

    public static TypstTemplateFile FromBytes(TypstPath path, ReadOnlyMemory<byte> content) => new MemoryTemplateFile(path, content);

    public static TypstTemplateFile FromFile(TypstPath path, string physicalPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(physicalPath);
        return new PhysicalTemplateFile(path, System.IO.Path.GetFullPath(physicalPath));
    }

    private sealed class MemoryTemplateFile(TypstPath path, ReadOnlyMemory<byte> content) : TypstTemplateFile(path)
    {
        public override ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken) =>
            new(new MemoryStream(content.ToArray(), writable: false));
    }

    private sealed class PhysicalTemplateFile(TypstPath path, string physicalPath) : TypstTemplateFile(path)
    {
        public override ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken) =>
            new(new FileStream(physicalPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true));
    }
}
```

`src/Typstium/Templates/TypstTemplate.cs`:

```csharp
using System.Collections.Immutable;

namespace Typstium;

/// <summary>A template bundle: its main file plus every file the main file may reference.</summary>
public sealed class TypstTemplate
{
    public TypstTemplate(TypstTemplateName name, TypstPath mainFile, TypstTemplateVersion version, IReadOnlyList<TypstTemplateFile> files)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        MainFile = mainFile ?? throw new ArgumentNullException(nameof(mainFile));
        Version = version ?? throw new ArgumentNullException(nameof(version));
        ArgumentNullException.ThrowIfNull(files);

        var seen = new Dictionary<string, TypstPath>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            if (file is null)
            {
                throw new ArgumentException("Template files must not be null.", nameof(files));
            }

            if (seen.TryGetValue(file.Path.Value, out var existing))
            {
                throw new ArgumentException(
                    $"Template '{name}' contains '{existing}' and '{file.Path}', which are duplicates or differ only by case.",
                    nameof(files));
            }

            seen.Add(file.Path.Value, file.Path);
        }

        if (!files.Any(file => file.Path == mainFile))
        {
            throw new ArgumentException($"The main file '{mainFile}' is not one of the files of template '{name}'.", nameof(mainFile));
        }

        Files = files.ToImmutableArray();
    }

    public TypstTemplateName Name { get; }

    public TypstPath MainFile { get; }

    public TypstTemplateVersion Version { get; }

    public IReadOnlyList<TypstTemplateFile> Files { get; }
}
```

The duplicate message must contain "differ only by case", because the test checks for it.

`tests/Typstium.Testing/Fakes/InMemoryTemplateProvider.cs`:

```csharp
using System.Text;

namespace Typstium.Testing.Fakes;

/// <summary>A template provider backed by in-memory strings, for tests.</summary>
public sealed class InMemoryTemplateProvider : ITemplateProvider
{
    private readonly Dictionary<string, TypstTemplate> _templates = new(StringComparer.Ordinal);
    private int _requestCount;

    public int RequestCount => _requestCount;

    public InMemoryTemplateProvider Add(string name, string mainFile, params (string Path, string Content)[] files)
    {
        var templateName = TypstTemplateName.Parse(name);
        var templateFiles = files
            .Select(file => TypstTemplateFile.FromBytes(TypstPath.Parse(file.Path), Encoding.UTF8.GetBytes(file.Content)))
            .ToList();
        _templates[name] = new TypstTemplate(templateName, TypstPath.Parse(mainFile), new TypstTemplateVersion("test-" + name), templateFiles);
        return this;
    }

    public ValueTask<TypstTemplate?> GetTemplateAsync(TypstTemplateName name, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _requestCount);
        return new ValueTask<TypstTemplate?>(_templates.TryGetValue(name.Value, out var template) ? template : null);
    }
}
```

The `(TypstPath path, …) : TypstTemplateFile(path)` primary-constructor syntax needs C# 12 and works on
every target framework.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --project tests/Typstium.UnitTests --treenode-filter "/*/*/TypstTemplateTests/*"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src tests
git commit -m "Add template provider abstraction and template bundles

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Sources and virtual files

The closed `TypstSource` hierarchy and in-memory virtual files (spec 4.3, 4.4). A file source can't hold
virtual files, and two virtual files whose paths differ only by case are rejected (Review Focus 2).

**Files:**
- Create: `src/Typstium/Sources/TypstVirtualFile.cs`, `src/Typstium/Sources/TypstSource.cs`, `src/Typstium/Sources/VirtualFileSet.cs`, `src/Typstium/Utilities/JsonDefaults.cs`
- Modify: `tests/Typstium.UnitTests/Typstium.UnitTests.csproj` (System.Text.Json on net472, for the JSON source generator)
- Test: `tests/Typstium.UnitTests/Sources/TypstVirtualFileTests.cs`, `tests/Typstium.UnitTests/Sources/TypstSourceTests.cs`

**Interfaces:**
- Consumes: `TypstPath` (Task 2); `ITemplateProvider`, `TypstTemplateName` (Tasks 2, 5).
- Produces:
  - `public sealed record TypstVirtualFile`: `TypstPath Path`, `ReadOnlyMemory<byte> Content`, `FromBytes`, `FromText`, `FromJson<T>` (3 overloads), `FromStreamAsync(TypstPath, Stream, CancellationToken)`
  - `public abstract record TypstSource`: static `File(string)`, `File(string, string)`, `Template(ITemplateProvider, TypstTemplateName)`, `Template(…, ImmutableArray<TypstVirtualFile>)`, `Markup(string)`, `Markup(string, ImmutableArray<TypstVirtualFile>)`
    - nested `FileSource { string Path; string? Root }`
    - nested `TemplateSource { ITemplateProvider Provider; TypstTemplateName Name; ImmutableArray<TypstVirtualFile> Files }`
    - nested `MarkupSource { string Text; ImmutableArray<TypstVirtualFile> Files }`
  - `internal static class VirtualFileSet`: `ImmutableArray<TypstVirtualFile> Validate(ImmutableArray<TypstVirtualFile> files, string parameterName)`
  - `internal static class JsonDefaults`: `JsonSerializerOptions Options` (web defaults, camelCase), `const string UnreferencedCodeMessage`, `const string DynamicCodeMessage`

- [ ] **Step 1: Write the failing tests**

`tests/Typstium.UnitTests/Sources/TypstVirtualFileTests.cs`:

```csharp
using System.Text;
using System.Text.Json.Serialization;

namespace Typstium.UnitTests.Sources;

public class TypstVirtualFileTests
{
    public sealed record License(string LicenseId, int Seats);

    [Test]
    public async Task FromText_writes_utf8_without_bom()
    {
        var file = TypstVirtualFile.FromText(TypstPath.Parse("notes.md"), "åäö");
        await Assert.That(file.Content.ToArray()).IsEquivalentTo(Encoding.UTF8.GetBytes("åäö"));
    }

    [Test]
    public async Task FromJson_uses_camel_case_web_defaults()
    {
        var file = TypstVirtualFile.FromJson(TypstPath.Parse("data.json"), new License("FJL-1", 25));
        var json = Encoding.UTF8.GetString(file.Content.ToArray());
        await Assert.That(json).IsEqualTo("{\"licenseId\":\"FJL-1\",\"seats\":25}");
    }

    [Test]
    public async Task FromJson_with_type_info_is_aot_friendly()
    {
        var file = TypstVirtualFile.FromJson(TypstPath.Parse("data.json"), new License("FJL-2", 1), TestJsonContext.Default.License);
        var json = Encoding.UTF8.GetString(file.Content.ToArray());
        await Assert.That(json).Contains("FJL-2");
    }

    [Test]
    public async Task FromStreamAsync_reads_the_stream_once_up_front()
    {
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var file = await TypstVirtualFile.FromStreamAsync(TypstPath.Parse("logo.png"), stream, CancellationToken.None);
        await Assert.That(file.Content.Length).IsEqualTo(3);
        await Assert.That(stream.Position).IsEqualTo(3L);
    }
}

[JsonSerializable(typeof(TypstVirtualFileTests.License))]
internal sealed partial class TestJsonContext : JsonSerializerContext;
```

`tests/Typstium.UnitTests/Sources/TypstSourceTests.cs`:

```csharp
using System.Collections.Immutable;
using Typstium.Testing.Fakes;

namespace Typstium.UnitTests.Sources;

public class TypstSourceTests
{
    private static TypstVirtualFile Text(string path) => TypstVirtualFile.FromText(TypstPath.Parse(path), "x");

    [Test]
    public async Task File_sources_hold_a_path_and_optional_root()
    {
        var source = TypstSource.File("docs/spec.typ", "docs");
        var file = source as TypstSource.FileSource;
        await Assert.That(file).IsNotNull();
        await Assert.That(file!.Root).IsEqualTo("docs");
    }

    [Test]
    public async Task Template_and_markup_sources_normalize_default_file_arrays()
    {
        var template = (TypstSource.TemplateSource)TypstSource.Template(new InMemoryTemplateProvider(), TypstTemplateName.Parse("a"), default);
        var markup = (TypstSource.MarkupSource)TypstSource.Markup("= Hi");
        await Assert.That(template.Files.IsDefault).IsFalse();
        await Assert.That(markup.Files.Length).IsEqualTo(0);
        await Assert.That(markup.Text).IsEqualTo("= Hi");
    }

    [Test]
    public async Task Virtual_files_differing_only_by_case_are_rejected()
    {
        var files = ImmutableArray.Create(Text("Data.json"), Text("data.json"));
        var exception = Assert.Throws<ArgumentException>(() => TypstSource.Markup("= Hi", files));
        await Assert.That(exception!.Message).Contains("differ only by case");
        await Assert.That(exception.ParamName).IsEqualTo("files");
    }

    [Test]
    public async Task Exact_duplicate_virtual_files_are_rejected()
    {
        var files = ImmutableArray.Create(Text("data.json"), Text("/data.json"));
        var exception = Assert.Throws<ArgumentException>(() => TypstSource.Markup("= Hi", files));
        await Assert.That(exception!.Message).Contains("more than once");
    }

    [Test]
    public async Task File_source_requires_a_path()
    {
        var exception = Assert.Throws<ArgumentException>(() => TypstSource.File(" "));
        await Assert.That(exception).IsNotNull();
    }
}
```

The JSON source generator in the test needs System.Text.Json referenced directly on `net472`, because analyzers don't flow transitively. Add this to `tests/Typstium.UnitTests/Typstium.UnitTests.csproj`:

```xml
  <ItemGroup Condition="'$(TargetFramework)' == 'net472'">
    <PackageReference Include="System.Text.Json" />
  </ItemGroup>
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/Typstium.UnitTests --framework net10.0 --treenode-filter "/*/*/TypstSourceTests/*"`
Expected: build FAILS with `CS0246` for `TypstSource`.

- [ ] **Step 3: Implement**

`src/Typstium/Utilities/JsonDefaults.cs`:

```csharp
using System.Text.Json;

namespace Typstium.Utilities;

internal static class JsonDefaults
{
    internal const string UnreferencedCodeMessage =
        "JSON serialization may need types that trimming removes. Use the overload that takes a JsonTypeInfo<T>.";

    internal const string DynamicCodeMessage =
        "JSON serialization may need runtime code generation. Use the overload that takes a JsonTypeInfo<T>.";

    /// <summary>Web defaults: camelCase property names, as Typst templates conventionally read them.</summary>
    internal static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);
}
```

`src/Typstium/Sources/TypstVirtualFile.cs`:

```csharp
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Typstium.Utilities;

namespace Typstium;

/// <summary>
/// A file that exists only for one compile, held in memory so a request can be replayed.
/// Written into the sandbox root at <see cref="Path"/>.
/// </summary>
public sealed record TypstVirtualFile
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private TypstVirtualFile(TypstPath path, ReadOnlyMemory<byte> content)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
        Content = content;
    }

    public TypstPath Path { get; }

    public ReadOnlyMemory<byte> Content { get; }

    public static TypstVirtualFile FromBytes(TypstPath path, ReadOnlyMemory<byte> content) => new(path, content);

    public static TypstVirtualFile FromText(TypstPath path, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new TypstVirtualFile(path, Utf8NoBom.GetBytes(text));
    }

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(JsonDefaults.UnreferencedCodeMessage)]
    [RequiresDynamicCode(JsonDefaults.DynamicCodeMessage)]
#endif
    public static TypstVirtualFile FromJson<T>(TypstPath path, T value) => FromJson(path, value, JsonDefaults.Options);

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(JsonDefaults.UnreferencedCodeMessage)]
    [RequiresDynamicCode(JsonDefaults.DynamicCodeMessage)]
#endif
    public static TypstVirtualFile FromJson<T>(TypstPath path, T value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new TypstVirtualFile(path, JsonSerializer.SerializeToUtf8Bytes(value, options));
    }

    public static TypstVirtualFile FromJson<T>(TypstPath path, T value, JsonTypeInfo<T> typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        return new TypstVirtualFile(path, JsonSerializer.SerializeToUtf8Bytes(value, typeInfo));
    }

    public static async Task<TypstVirtualFile> FromStreamAsync(TypstPath path, Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, 81920, cancellationToken).ConfigureAwait(false);
        return new TypstVirtualFile(path, buffer.ToArray());
    }
}
```

`src/Typstium/Sources/VirtualFileSet.cs`:

```csharp
using System.Collections.Immutable;

namespace Typstium;

internal static class VirtualFileSet
{
    /// <summary>Normalizes a default array to empty and rejects nulls and case-insensitive duplicates.</summary>
    public static ImmutableArray<TypstVirtualFile> Validate(ImmutableArray<TypstVirtualFile> files, string parameterName)
    {
        if (files.IsDefault)
        {
            return ImmutableArray<TypstVirtualFile>.Empty;
        }

        var seen = new Dictionary<string, TypstPath>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            if (file is null)
            {
                throw new ArgumentException("Virtual files must not be null.", parameterName);
            }

            if (seen.TryGetValue(file.Path.Value, out var existing))
            {
                var message = existing == file.Path
                    ? $"The virtual file '{file.Path}' is added more than once."
                    : $"The virtual files '{existing}' and '{file.Path}' differ only by case; they would overwrite each other on Windows and macOS.";
                throw new ArgumentException(message, parameterName);
            }

            seen.Add(file.Path.Value, file.Path);
        }

        return files;
    }
}
```

`src/Typstium/Sources/TypstSource.cs`:

```csharp
using System.Collections.Immutable;

namespace Typstium;

/// <summary>
/// What to compile. Exactly one of three kinds; the kind decides where --root points (spec 4.3):
/// file sources compile in place, template and markup sources compile in a sandbox.
/// </summary>
public abstract record TypstSource
{
    private TypstSource()
    {
    }

    /// <summary>A .typ file on disk, compiled in place. The root is the file's folder.</summary>
    public static TypstSource File(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new FileSource(path, root: null);
    }

    /// <summary>A .typ file on disk, compiled in place with an explicit project root.</summary>
    public static TypstSource File(string path, string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        return new FileSource(path, root);
    }

    public static TypstSource Template(ITemplateProvider provider, TypstTemplateName name) =>
        Template(provider, name, ImmutableArray<TypstVirtualFile>.Empty);

    public static TypstSource Template(ITemplateProvider provider, TypstTemplateName name, ImmutableArray<TypstVirtualFile> files)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(name);
        return new TemplateSource(provider, name, VirtualFileSet.Validate(files, nameof(files)));
    }

    public static TypstSource Markup(string markup) => Markup(markup, ImmutableArray<TypstVirtualFile>.Empty);

    public static TypstSource Markup(string markup, ImmutableArray<TypstVirtualFile> files)
    {
        ArgumentNullException.ThrowIfNull(markup);
        return new MarkupSource(markup, VirtualFileSet.Validate(files, nameof(files)));
    }

    public sealed record FileSource : TypstSource
    {
        internal FileSource(string path, string? root)
        {
            Path = path;
            Root = root;
        }

        public string Path { get; }

        public string? Root { get; }
    }

    public sealed record TemplateSource : TypstSource
    {
        internal TemplateSource(ITemplateProvider provider, TypstTemplateName name, ImmutableArray<TypstVirtualFile> files)
        {
            Provider = provider;
            Name = name;
            Files = files;
        }

        public ITemplateProvider Provider { get; }

        public TypstTemplateName Name { get; }

        public ImmutableArray<TypstVirtualFile> Files { get; }
    }

    public sealed record MarkupSource : TypstSource
    {
        internal MarkupSource(string text, ImmutableArray<TypstVirtualFile> files)
        {
            Text = text;
            Files = files;
        }

        /// <summary>The Typst markup, sent to Typst over stdin.</summary>
        public string Text { get; }

        public ImmutableArray<TypstVirtualFile> Files { get; }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --project tests/Typstium.UnitTests --treenode-filter "/*/*/TypstSourceTests/*"`
Then: `dotnet test --project tests/Typstium.UnitTests --treenode-filter "/*/*/TypstVirtualFileTests/*"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src tests
git commit -m "Add TypstSource kinds and in-memory virtual files

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Output formats and documents

What to produce and how the result comes back (spec 4.5).

**Files:**
- Create: `src/Typstium/Output/TypstOutputFormat.cs`, `PdfStandard.cs`, `TypstPageRange.cs`, `TypstPageSelection.cs`, `TypstPdfOptions.cs`, `TypstOutput.cs`, `TypstDocument.cs` (all under `src/Typstium/Output/`)
- Create: `src/Typstium/Utilities/ByteMemory.cs`
- Test: `tests/Typstium.UnitTests/Output/TypstOutputTests.cs`, `tests/Typstium.UnitTests/Output/TypstDocumentTests.cs`

**Interfaces:**
- Consumes: nothing beyond the BCL.
- Produces:
  - `enum TypstOutputFormat { Pdf, Png, Svg, Html }`
  - `sealed class PdfStandard`: statics `Pdf14 Pdf15 Pdf16 Pdf17 Pdf20 A1b A1a A2b A2u A2a A3b A3u A3a A4 A4f A4e UA1`, `string CliValue`, `static FromCliValue(string)`, `internal static IReadOnlyList<PdfStandard> KnownInTypst015`
  - `sealed record TypstPageRange`: `Single(int)`, `Between(int, int)`, `From(int)`, `int First`, `int? Last`, `internal string ToCliValue()`
  - `sealed record TypstPageSelection`: `All`, `Of(params TypstPageRange[])`, `ImmutableArray<TypstPageRange> Ranges`, `bool IsAll`, `internal string ToCliValue()`
  - `sealed record TypstPdfOptions`: `Default`, `ImmutableArray<PdfStandard> Standards`, `bool Tagged = true`, `TypstPageSelection Pages`
  - `abstract record TypstOutput`:
    - `Format`; `internal bool IsPaged`; `internal string FileExtension`
    - factories `Pdf()`, `Pdf(params PdfStandard[])`, `Pdf(TypstPdfOptions)`, `Png()`, `Png(int)`, `Png(int, TypstPageSelection)`, `Svg()`, `Svg(TypstPageSelection)`, `Html()`
    - nested `PdfOutput { Options }`, `PngOutput { Ppi, Pages }`, `SvgOutput { Pages }`, `HtmlOutput`
  - `abstract record TypstDocument`:
    - `Format`; static `CreateSingleFile(TypstOutputFormat, ReadOnlyMemory<byte>)` and `CreatePaged(TypstOutputFormat, ImmutableArray<ReadOnlyMemory<byte>>)`
    - nested `SingleFile { Content; WriteToAsync(Stream, CancellationToken); SaveAsync(string, CancellationToken) }`
    - nested `Paged { Pages; Task<IReadOnlyList<string>> SaveAsync(string pathPattern, CancellationToken) }`
  - `internal static class ByteMemory { static ArraySegment<byte> AsSegment(ReadOnlyMemory<byte>) }`

- [ ] **Step 1: Write the failing tests**

`tests/Typstium.UnitTests/Output/TypstOutputTests.cs`:

```csharp
namespace Typstium.UnitTests.Output;

public class TypstOutputTests
{
    [Test]
    public async Task Pdf_with_standards()
    {
        var output = (TypstOutput.PdfOutput)TypstOutput.Pdf(PdfStandard.A2b, PdfStandard.UA1);
        await Assert.That(output.Format).IsEqualTo(TypstOutputFormat.Pdf);
        await Assert.That(output.Options.Standards.Select(s => s.CliValue)).IsEquivalentTo(new[] { "a-2b", "ua-1" });
        await Assert.That(output.Options.Tagged).IsTrue();
        await Assert.That(output.IsPaged).IsFalse();
    }

    [Test]
    public async Task Png_and_svg_are_paged()
    {
        var png = (TypstOutput.PngOutput)TypstOutput.Png(300, TypstPageSelection.Of(TypstPageRange.Single(1), TypstPageRange.From(3)));
        await Assert.That(png.Ppi).IsEqualTo(300);
        await Assert.That(png.Pages.ToCliValue()).IsEqualTo("1,3-");
        await Assert.That(png.IsPaged).IsTrue();
        await Assert.That(TypstOutput.Svg().IsPaged).IsTrue();
        await Assert.That(TypstOutput.Html().FileExtension).IsEqualTo("html");
    }

    [Test]
    public async Task Page_ranges_render_like_the_cli()
    {
        await Assert.That(TypstPageRange.Single(2).ToCliValue()).IsEqualTo("2");
        await Assert.That(TypstPageRange.Between(3, 6).ToCliValue()).IsEqualTo("3-6");
        await Assert.That(TypstPageRange.From(8).ToCliValue()).IsEqualTo("8-");
        await Assert.That(TypstPageSelection.All.IsAll).IsTrue();
    }

    [Test]
    public async Task Invalid_values_are_rejected()
    {
        await Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => TypstOutput.Png(0))).IsNotNull();
        await Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => TypstPageRange.Between(5, 2))).IsNotNull();
        await Assert.That(Assert.Throws<ArgumentException>(() => TypstPageSelection.Of())).IsNotNull();
        await Assert.That(Assert.Throws<ArgumentException>(() => PdfStandard.FromCliValue("a-2b,ua-1"))).IsNotNull();
    }

    [Test]
    public async Task Pdf_standards_compare_by_cli_value()
    {
        await Assert.That(PdfStandard.FromCliValue("a-2b")).IsEqualTo(PdfStandard.A2b);
        await Assert.That(PdfStandard.KnownInTypst015.Count).IsEqualTo(17);
    }
}
```

`tests/Typstium.UnitTests/Output/TypstDocumentTests.cs`:

```csharp
using System.Collections.Immutable;

namespace Typstium.UnitTests.Output;

public class TypstDocumentTests
{
    [Test]
    public async Task Single_file_documents_write_their_content()
    {
        var document = (TypstDocument.SingleFile)TypstDocument.CreateSingleFile(TypstOutputFormat.Pdf, new byte[] { 37, 80, 68, 70 });
        using var destination = new MemoryStream();
        await document.WriteToAsync(destination, CancellationToken.None);
        await Assert.That(destination.ToArray()).IsEquivalentTo(new byte[] { 37, 80, 68, 70 });
    }

    [Test]
    public async Task Paged_documents_save_with_a_page_pattern()
    {
        var pages = ImmutableArray.Create<ReadOnlyMemory<byte>>(new byte[] { 1 }, new byte[] { 2 });
        var document = (TypstDocument.Paged)TypstDocument.CreatePaged(TypstOutputFormat.Png, pages);
        var directory = Path.Combine(Path.GetTempPath(), "typstium-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var written = await document.SaveAsync(Path.Combine(directory, "page-{0p}-of-{t}.png"), CancellationToken.None);
            await Assert.That(written.Select(w => Path.GetFileName(w)!)).IsEquivalentTo(new[] { "page-1-of-2.png", "page-2-of-2.png" });
            await Assert.That(File.ReadAllBytes(written[1])).IsEquivalentTo(new byte[] { 2 });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task Multi_page_patterns_must_contain_a_page_placeholder()
    {
        var pages = ImmutableArray.Create<ReadOnlyMemory<byte>>(new byte[] { 1 }, new byte[] { 2 });
        var document = (TypstDocument.Paged)TypstDocument.CreatePaged(TypstOutputFormat.Svg, pages);
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => document.SaveAsync("page.svg", CancellationToken.None));
        await Assert.That(exception.ParamName).IsEqualTo("pathPattern");
    }

    [Test]
    public async Task Factories_reject_the_wrong_shape_for_a_format()
    {
        await Assert.That(Assert.Throws<ArgumentException>(() => TypstDocument.CreateSingleFile(TypstOutputFormat.Png, new byte[] { 1 }))).IsNotNull();
        await Assert.That(Assert.Throws<ArgumentException>(() => TypstDocument.CreatePaged(TypstOutputFormat.Pdf, ImmutableArray<ReadOnlyMemory<byte>>.Empty))).IsNotNull();
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/Typstium.UnitTests --framework net10.0 --treenode-filter "/*/*/TypstOutputTests/*"`
Expected: build FAILS with `CS0246` for `TypstOutput`.

- [ ] **Step 3: Implement**

`src/Typstium/Output/TypstOutputFormat.cs`:

```csharp
namespace Typstium;

public enum TypstOutputFormat
{
    Pdf,
    Png,
    Svg,
    Html,
}
```

`src/Typstium/Output/PdfStandard.cs`:

```csharp
using System.Text.RegularExpressions;

namespace Typstium;

/// <summary>A PDF standard Typst can enforce. The CLI value (e.g. <c>a-2b</c>) is its identity.</summary>
public sealed class PdfStandard : IEquatable<PdfStandard>
{
    private static readonly Regex CliValueFormat = new("^[a-z0-9.-]+$", RegexOptions.CultureInvariant);

    private PdfStandard(string cliValue) => CliValue = cliValue;

    public static PdfStandard Pdf14 { get; } = new("1.4");
    public static PdfStandard Pdf15 { get; } = new("1.5");
    public static PdfStandard Pdf16 { get; } = new("1.6");
    public static PdfStandard Pdf17 { get; } = new("1.7");
    public static PdfStandard Pdf20 { get; } = new("2.0");
    public static PdfStandard A1b { get; } = new("a-1b");
    public static PdfStandard A1a { get; } = new("a-1a");
    public static PdfStandard A2b { get; } = new("a-2b");
    public static PdfStandard A2u { get; } = new("a-2u");
    public static PdfStandard A2a { get; } = new("a-2a");
    public static PdfStandard A3b { get; } = new("a-3b");
    public static PdfStandard A3u { get; } = new("a-3u");
    public static PdfStandard A3a { get; } = new("a-3a");
    public static PdfStandard A4 { get; } = new("a-4");
    public static PdfStandard A4f { get; } = new("a-4f");
    public static PdfStandard A4e { get; } = new("a-4e");
    public static PdfStandard UA1 { get; } = new("ua-1");

    // Declared after the instances: static initializers run in textual order.
    internal static IReadOnlyList<PdfStandard> KnownInTypst015 { get; } =
        [Pdf14, Pdf15, Pdf16, Pdf17, Pdf20, A1b, A1a, A2b, A2u, A2a, A3b, A3u, A3a, A4, A4f, A4e, UA1];

    public string CliValue { get; }

    /// <summary>Escape hatch for standards added by newer Typst versions.</summary>
    public static PdfStandard FromCliValue(string value)
    {
        if (value is null || !CliValueFormat.IsMatch(value))
        {
            throw new ArgumentException($"'{value}' is not a single PDF standard value such as 'a-2b'.", nameof(value));
        }

        return new PdfStandard(value);
    }

    public bool Equals(PdfStandard? other) => other is not null && string.Equals(CliValue, other.CliValue, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as PdfStandard);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(CliValue);

    public override string ToString() => CliValue;
}
```

`src/Typstium/Output/TypstPageRange.cs`:

```csharp
using System.Globalization;

namespace Typstium;

/// <summary>A 1-based page range: one page, a closed range, or "from page N to the end".</summary>
public sealed record TypstPageRange
{
    private TypstPageRange(int first, int? last)
    {
        First = first;
        Last = last;
    }

    public int First { get; }

    public int? Last { get; }

    public static TypstPageRange Single(int page)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page);
        return new TypstPageRange(page, page);
    }

    public static TypstPageRange Between(int first, int last)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(first);
        if (last < first)
        {
            throw new ArgumentOutOfRangeException(nameof(last), last, "The last page must not come before the first page.");
        }

        return new TypstPageRange(first, last);
    }

    public static TypstPageRange From(int first)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(first);
        return new TypstPageRange(first, null);
    }

    internal string ToCliValue()
    {
        var first = First.ToString(CultureInfo.InvariantCulture);
        if (Last is not { } last)
        {
            return first + "-";
        }

        return last == First ? first : first + "-" + last.ToString(CultureInfo.InvariantCulture);
    }
}
```

`src/Typstium/Output/TypstPageSelection.cs`:

```csharp
using System.Collections.Immutable;

namespace Typstium;

public sealed record TypstPageSelection
{
    private TypstPageSelection(ImmutableArray<TypstPageRange> ranges) => Ranges = ranges;

    public static TypstPageSelection All { get; } = new(ImmutableArray<TypstPageRange>.Empty);

    public ImmutableArray<TypstPageRange> Ranges { get; }

    public bool IsAll => Ranges.IsEmpty;

    public static TypstPageSelection Of(params TypstPageRange[] ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        if (ranges.Length == 0)
        {
            throw new ArgumentException("At least one range is required; use TypstPageSelection.All for every page.", nameof(ranges));
        }

        if (ranges.Any(range => range is null))
        {
            throw new ArgumentException("Page ranges must not be null.", nameof(ranges));
        }

        return new TypstPageSelection(ranges.ToImmutableArray());
    }

    internal string ToCliValue() => string.Join(",", Ranges.Select(range => range.ToCliValue()));
}
```

`src/Typstium/Output/TypstPdfOptions.cs`:

```csharp
using System.Collections.Immutable;

namespace Typstium;

public sealed record TypstPdfOptions
{
    private readonly ImmutableArray<PdfStandard> _standards = ImmutableArray<PdfStandard>.Empty;
    private readonly TypstPageSelection _pages = TypstPageSelection.All;

    public static TypstPdfOptions Default { get; } = new();

    public ImmutableArray<PdfStandard> Standards
    {
        get => _standards;
        init
        {
            var standards = value.IsDefault ? ImmutableArray<PdfStandard>.Empty : value;
            if (standards.Any(standard => standard is null))
            {
                throw new ArgumentException("PDF standards must not be null.", nameof(value));
            }

            _standards = standards;
        }
    }

    /// <summary>Whether to write a tagged PDF (Typst's default). Set to false to pass --no-pdf-tags.</summary>
    public bool Tagged { get; init; } = true;

    public TypstPageSelection Pages
    {
        get => _pages;
        init => _pages = value ?? throw new ArgumentNullException(nameof(value));
    }
}
```

`src/Typstium/Output/TypstOutput.cs`:

```csharp
using System.Collections.Immutable;

namespace Typstium;

/// <summary>The output format and its options. Exactly one of four kinds.</summary>
public abstract record TypstOutput
{
    private TypstOutput()
    {
    }

    public abstract TypstOutputFormat Format { get; }

    /// <summary>PNG and SVG produce one file per page.</summary>
    internal bool IsPaged => Format is TypstOutputFormat.Png or TypstOutputFormat.Svg;

    internal string FileExtension => Format switch
    {
        TypstOutputFormat.Pdf => "pdf",
        TypstOutputFormat.Png => "png",
        TypstOutputFormat.Svg => "svg",
        TypstOutputFormat.Html => "html",
        _ => throw new InvalidOperationException($"Unknown output format {Format}."),
    };

    public static TypstOutput Pdf() => new PdfOutput(TypstPdfOptions.Default);

    public static TypstOutput Pdf(params PdfStandard[] standards)
    {
        ArgumentNullException.ThrowIfNull(standards);
        return new PdfOutput(TypstPdfOptions.Default with { Standards = standards.ToImmutableArray() });
    }

    public static TypstOutput Pdf(TypstPdfOptions options) => new PdfOutput(options);

    public static TypstOutput Png() => new PngOutput(144, TypstPageSelection.All);

    public static TypstOutput Png(int ppi) => new PngOutput(ppi, TypstPageSelection.All);

    public static TypstOutput Png(int ppi, TypstPageSelection pages) => new PngOutput(ppi, pages);

    public static TypstOutput Svg() => new SvgOutput(TypstPageSelection.All);

    public static TypstOutput Svg(TypstPageSelection pages) => new SvgOutput(pages);

    /// <summary>Experimental HTML export; requires Typst's html feature.</summary>
    public static TypstOutput Html() => HtmlOutput.Instance;

    public sealed record PdfOutput : TypstOutput
    {
        internal PdfOutput(TypstPdfOptions options) => Options = options ?? throw new ArgumentNullException(nameof(options));

        public TypstPdfOptions Options { get; }

        public override TypstOutputFormat Format => TypstOutputFormat.Pdf;
    }

    public sealed record PngOutput : TypstOutput
    {
        internal PngOutput(int ppi, TypstPageSelection pages)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ppi);
            Ppi = ppi;
            Pages = pages ?? throw new ArgumentNullException(nameof(pages));
        }

        public int Ppi { get; }

        public TypstPageSelection Pages { get; }

        public override TypstOutputFormat Format => TypstOutputFormat.Png;
    }

    public sealed record SvgOutput : TypstOutput
    {
        internal SvgOutput(TypstPageSelection pages) => Pages = pages ?? throw new ArgumentNullException(nameof(pages));

        public TypstPageSelection Pages { get; }

        public override TypstOutputFormat Format => TypstOutputFormat.Svg;
    }

    public sealed record HtmlOutput : TypstOutput
    {
        private HtmlOutput()
        {
        }

        internal static HtmlOutput Instance { get; } = new();

        public override TypstOutputFormat Format => TypstOutputFormat.Html;
    }
}
```

`src/Typstium/Utilities/ByteMemory.cs`:

```csharp
using System.Runtime.InteropServices;

namespace Typstium.Utilities;

internal static class ByteMemory
{
    /// <summary>The array behind the memory, without copying when possible (netstandard2.0 has no span I/O).</summary>
    public static ArraySegment<byte> AsSegment(ReadOnlyMemory<byte> memory) =>
        MemoryMarshal.TryGetArray(memory, out var segment) ? segment : new ArraySegment<byte>(memory.ToArray());
}
```

`src/Typstium/Output/TypstDocument.cs`:

```csharp
using System.Collections.Immutable;
using System.Globalization;
using Typstium.Utilities;

namespace Typstium;

/// <summary>A compiled document. PDF and HTML are a single file; PNG and SVG are one file per page.</summary>
public abstract record TypstDocument
{
    private TypstDocument()
    {
    }

    public abstract TypstOutputFormat Format { get; }

    public static TypstDocument CreateSingleFile(TypstOutputFormat format, ReadOnlyMemory<byte> content)
    {
        if (format is not (TypstOutputFormat.Pdf or TypstOutputFormat.Html))
        {
            throw new ArgumentException($"{format} output is paged; use CreatePaged.", nameof(format));
        }

        return new SingleFile(format, content);
    }

    public static TypstDocument CreatePaged(TypstOutputFormat format, ImmutableArray<ReadOnlyMemory<byte>> pages)
    {
        if (format is not (TypstOutputFormat.Png or TypstOutputFormat.Svg))
        {
            throw new ArgumentException($"{format} output is a single file; use CreateSingleFile.", nameof(format));
        }

        if (pages.IsDefaultOrEmpty)
        {
            throw new ArgumentException("A paged document needs at least one page.", nameof(pages));
        }

        return new Paged(format, pages);
    }

    public sealed record SingleFile : TypstDocument
    {
        internal SingleFile(TypstOutputFormat format, ReadOnlyMemory<byte> content)
        {
            Format = format;
            Content = content;
        }

        public override TypstOutputFormat Format { get; }

        public ReadOnlyMemory<byte> Content { get; }

        public Task WriteToAsync(Stream destination, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(destination);
            var segment = ByteMemory.AsSegment(Content);
            return destination.WriteAsync(segment.Array!, segment.Offset, segment.Count, cancellationToken);
        }

        public async Task SaveAsync(string path, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await WriteToAsync(stream, cancellationToken).ConfigureAwait(false);
        }
    }

    public sealed record Paged : TypstDocument
    {
        internal Paged(TypstOutputFormat format, ImmutableArray<ReadOnlyMemory<byte>> pages)
        {
            Format = format;
            Pages = pages;
        }

        public override TypstOutputFormat Format { get; }

        public ImmutableArray<ReadOnlyMemory<byte>> Pages { get; }

        /// <summary>
        /// Saves every page. The pattern uses Typst's placeholders: <c>{p}</c> page number,
        /// <c>{0p}</c> zero-padded page number, <c>{t}</c> page count.
        /// </summary>
        /// <returns>The written file paths, in page order.</returns>
        public async Task<IReadOnlyList<string>> SaveAsync(string pathPattern, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pathPattern);
            if (Pages.Length > 1
                && pathPattern.IndexOf("{p}", StringComparison.Ordinal) < 0
                && pathPattern.IndexOf("{0p}", StringComparison.Ordinal) < 0)
            {
                throw new ArgumentException("A multi-page document needs {p} or {0p} in the path pattern.", nameof(pathPattern));
            }

            var written = new List<string>(Pages.Length);
            for (var i = 0; i < Pages.Length; i++)
            {
                var path = FormatPath(pathPattern, i + 1, Pages.Length);
                var segment = ByteMemory.AsSegment(Pages[i]);
                using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                {
                    await stream.WriteAsync(segment.Array!, segment.Offset, segment.Count, cancellationToken).ConfigureAwait(false);
                }

                written.Add(path);
            }

            return written;
        }

        internal static string FormatPath(string pattern, int pageNumber, int pageCount)
        {
            var digits = pageCount.ToString(CultureInfo.InvariantCulture).Length;
            return pattern
                .Replace("{0p}", pageNumber.ToString(CultureInfo.InvariantCulture).PadLeft(digits, '0'))
                .Replace("{p}", pageNumber.ToString(CultureInfo.InvariantCulture))
                .Replace("{t}", pageCount.ToString(CultureInfo.InvariantCulture));
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --project tests/Typstium.UnitTests --treenode-filter "/*/*/TypstOutputTests/*"`
Then: `dotnet test --project tests/Typstium.UnitTests --treenode-filter "/*/*/TypstDocumentTests/*"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src tests
git commit -m "Add output formats, PDF standards, page selections and documents

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Options, request, result and the compiler interface

The contract every backend receives and returns (spec 4.1, 4.2, 4.5, 4.7).

**Files:**
- Create: `src/Typstium/TypstNetworkAccess.cs`, `TypstFontOptions.cs`, `TypstPackageOptions.cs`, `TypstCreationTimestamp.cs`, `TypstCompileOptions.cs`, `TypstCompileRequest.cs`, `InputKeys.cs`, `TypstCompileStatus.cs`, `TypstInvocation.cs`, `TypstCompileResult.cs`, `ITypstCompiler.cs` (all under `src/Typstium/`)
- Create: `src/Typstium/Exceptions/TypstCompileException.cs`
- Create: `tests/Typstium.Testing/Fakes/FakeInvocation.cs`
- Test: `tests/Typstium.UnitTests/TypstCompileOptionsTests.cs`, `tests/Typstium.UnitTests/TypstCompileRequestTests.cs`, `tests/Typstium.UnitTests/TypstCompileResultTests.cs`

**Interfaces:**
- Consumes: `TypstArguments` (Task 3), diagnostics model and `TypstException` (Task 4), `TypstSource` (Task 6), `TypstOutput`, `TypstDocument` (Task 7), `TypstVersion` (Task 2).
- Produces:
  - `enum TypstNetworkAccess { Blocked, Allowed }`
  - `sealed record TypstFontOptions { Default; ImmutableArray<string> FontPaths; bool IncludeSystemFonts (false); bool IncludeEmbeddedFonts (true) }`
  - `sealed record TypstPackageOptions { Default; string? PackagePath; string? PackageCachePath; TypstNetworkAccess NetworkAccess (Blocked) }`
  - `abstract record TypstCreationTimestamp { Inherit; Now; Fixed(DateTimeOffset) }` with nested `InheritTimestamp`, `NowTimestamp`, `FixedTimestamp(DateTimeOffset Value)`
  - `sealed record TypstCompileOptions`: `Default`, `Untrusted`, `Fonts`, `Packages`, `Timeout`, `MaxOutputBytes`, `MemoryLimitBytes`, `CreationTimestamp`, `Jobs`, `ExtraGlobalArguments`, `ExtraArguments`, `ArgumentCustomization`
  - `sealed partial record TypstCompileRequest(TypstSource source)`: `Source`, `Inputs` (ordinal), `Output`, `Options`
  - `internal static class InputKeys { static void Validate(string key, string parameterName) }`
  - `enum TypstCompileStatus { Succeeded, Failed, TimedOut, OutputLimitExceeded }`
  - `sealed record TypstInvocation(string ExecutablePath, string ExecutableFoundBy, TypstVersion TypstVersion, string CommandLine, TimeSpan ProcessDuration, TimeSpan TotalDuration)`
  - `sealed class TypstCompileResult`:
    - factories `CreateSucceeded(TypstDocument, ImmutableArray<TypstDiagnostic>, TypstInvocation)` and `CreateUnsuccessful(TypstCompileStatus, ImmutableArray<TypstDiagnostic>, TypstInvocation)`
    - members `Status`, `IsSuccess`, `Document`, `Diagnostics`, `Errors`, `Warnings`, `Invocation`, `EnsureSuccess()`
  - `sealed class TypstCompileException : TypstException { TypstCompileResult Result }`
  - `interface ITypstCompiler { Task<TypstCompileResult> CompileAsync(TypstCompileRequest request, CancellationToken cancellationToken = default); }`
  - Test fake `Typstium.Testing.Fakes.FakeInvocation.Create()` returns a `TypstInvocation`

- [ ] **Step 1: Write the failing tests**

`tests/Typstium.UnitTests/TypstCompileOptionsTests.cs`:

```csharp
namespace Typstium.UnitTests;

public class TypstCompileOptionsTests
{
    [Test]
    public async Task Default_is_safe_and_reproducible()
    {
        var options = TypstCompileOptions.Default;
        await Assert.That(options.Timeout).IsEqualTo(TimeSpan.FromSeconds(60));
        await Assert.That(options.MaxOutputBytes).IsEqualTo(100L * 1024 * 1024);
        await Assert.That(options.MemoryLimitBytes).IsNull();
        await Assert.That(options.Fonts.IncludeSystemFonts).IsFalse();
        await Assert.That(options.Fonts.IncludeEmbeddedFonts).IsTrue();
        await Assert.That(options.Packages.NetworkAccess).IsEqualTo(TypstNetworkAccess.Blocked);
        await Assert.That(options.CreationTimestamp).IsEqualTo(TypstCreationTimestamp.Inherit);
        await Assert.That(options.ArgumentCustomization).IsNull();
    }

    [Test]
    public async Task Untrusted_tightens_limits()
    {
        var options = TypstCompileOptions.Untrusted;
        await Assert.That(options.Timeout).IsEqualTo(TimeSpan.FromSeconds(10));
        await Assert.That(options.MaxOutputBytes).IsEqualTo(20L * 1024 * 1024);
        await Assert.That(options.MemoryLimitBytes).IsEqualTo(1024L * 1024 * 1024);
    }

    [Test]
    public async Task Invalid_values_cannot_be_set()
    {
        await Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => TypstCompileOptions.Default with { Timeout = TimeSpan.Zero })).IsNotNull();
        await Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => TypstCompileOptions.Default with { MaxOutputBytes = 0 })).IsNotNull();
        await Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => TypstCompileOptions.Default with { Jobs = 0 })).IsNotNull();
        await Assert.That(Assert.Throws<ArgumentNullException>(() => TypstCompileOptions.Default with { Fonts = null! })).IsNotNull();
    }

    [Test]
    public async Task Infinite_timeout_is_allowed_explicitly()
    {
        var options = TypstCompileOptions.Default with { Timeout = Timeout.InfiniteTimeSpan };
        await Assert.That(options.Timeout).IsEqualTo(Timeout.InfiniteTimeSpan);
    }

    [Test]
    public async Task Font_paths_default_to_empty_not_default()
    {
        var fonts = TypstFontOptions.Default with { FontPaths = default };
        await Assert.That(fonts.FontPaths.IsDefault).IsFalse();
    }
}
```

`tests/Typstium.UnitTests/TypstCompileRequestTests.cs`:

```csharp
using System.Collections.Immutable;

namespace Typstium.UnitTests;

public class TypstCompileRequestTests
{
    [Test]
    public async Task Defaults_to_pdf_with_default_options()
    {
        var request = new TypstCompileRequest(TypstSource.Markup("= Hi"));
        await Assert.That(request.Output.Format).IsEqualTo(TypstOutputFormat.Pdf);
        await Assert.That(request.Options).IsEqualTo(TypstCompileOptions.Default);
        await Assert.That(request.Inputs.Count).IsEqualTo(0);
    }

    [Test]
    [Arguments("")]
    [Arguments(" ")]
    [Arguments("a=b")]
    public async Task Input_keys_must_be_non_empty_and_free_of_equals_signs(string key)
    {
        var inputs = ImmutableDictionary<string, string>.Empty.Add(key, "value");
        var exception = Assert.Throws<ArgumentException>(() => new TypstCompileRequest(TypstSource.Markup("x")) { Inputs = inputs });
        await Assert.That(exception).IsNotNull();
    }

    [Test]
    public async Task Source_cannot_be_null()
    {
        await Assert.That(Assert.Throws<ArgumentNullException>(() => new TypstCompileRequest(null!))).IsNotNull();
    }
}
```

`tests/Typstium.UnitTests/TypstCompileResultTests.cs`:

```csharp
using System.Collections.Immutable;
using Typstium.Testing.Fakes;

namespace Typstium.UnitTests;

public class TypstCompileResultTests
{
    private static readonly TypstDiagnostic Error = new(
        TypstDiagnosticId.Parse("TYPST0001"), TypstDiagnosticSeverity.Error, "boom", null, default, default);

    [Test]
    public async Task Succeeded_results_carry_a_document()
    {
        var document = TypstDocument.CreateSingleFile(TypstOutputFormat.Pdf, new byte[] { 1 });
        var result = TypstCompileResult.CreateSucceeded(document, ImmutableArray<TypstDiagnostic>.Empty, FakeInvocation.Create());
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Document).IsNotNull();
        await Assert.That(result.EnsureSuccess()).IsEqualTo(result);
    }

    [Test]
    public async Task Unsuccessful_results_have_no_document_and_throw_from_EnsureSuccess()
    {
        var result = TypstCompileResult.CreateUnsuccessful(TypstCompileStatus.Failed, ImmutableArray.Create(Error), FakeInvocation.Create());
        await Assert.That(result.Document).IsNull();
        await Assert.That(result.Errors.Count()).IsEqualTo(1);
        var exception = Assert.Throws<TypstCompileException>(() => result.EnsureSuccess());
        await Assert.That(exception!.Result).IsEqualTo(result);
        await Assert.That(exception.Message).Contains("boom");
    }

    [Test]
    public async Task CreateUnsuccessful_rejects_the_succeeded_status()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            TypstCompileResult.CreateUnsuccessful(TypstCompileStatus.Succeeded, ImmutableArray<TypstDiagnostic>.Empty, FakeInvocation.Create()));
        await Assert.That(exception).IsNotNull();
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/Typstium.UnitTests --framework net10.0 --treenode-filter "/*/*/TypstCompileOptionsTests/*"`
Expected: build FAILS with `CS0246` for `TypstCompileOptions`.

- [ ] **Step 3: Implement the options**

`src/Typstium/TypstNetworkAccess.cs`:

```csharp
namespace Typstium;

public enum TypstNetworkAccess
{
    /// <summary>Packages come only from the package path or cache; downloads fail fast.</summary>
    Blocked,

    /// <summary>Typst may download missing packages from the internet.</summary>
    Allowed,
}
```

`src/Typstium/TypstFontOptions.cs`:

```csharp
using System.Collections.Immutable;

namespace Typstium;

public sealed record TypstFontOptions
{
    private readonly ImmutableArray<string> _fontPaths = ImmutableArray<string>.Empty;

    public static TypstFontOptions Default { get; } = new();

    /// <summary>Folders searched recursively for fonts (--font-path).</summary>
    public ImmutableArray<string> FontPaths
    {
        get => _fontPaths;
        init => _fontPaths = value.IsDefault ? ImmutableArray<string>.Empty : value;
    }

    /// <summary>Off by default: about 3× faster on Windows and identical output on every machine.</summary>
    public bool IncludeSystemFonts { get; init; }

    public bool IncludeEmbeddedFonts { get; init; } = true;
}
```

`src/Typstium/TypstPackageOptions.cs`:

```csharp
namespace Typstium;

public sealed record TypstPackageOptions
{
    public static TypstPackageOptions Default { get; } = new();

    /// <summary>Vendored packages laid out as namespace/name/version (--package-path). Null: Typst's default.</summary>
    public string? PackagePath { get; init; }

    /// <summary>The package cache (--package-cache-path). Null: Typst's default.</summary>
    public string? PackageCachePath { get; init; }

    public TypstNetworkAccess NetworkAccess { get; init; } = TypstNetworkAccess.Blocked;
}
```

`src/Typstium/TypstCreationTimestamp.cs`:

```csharp
namespace Typstium;

/// <summary>The document creation timestamp written into the output.</summary>
public abstract record TypstCreationTimestamp
{
    private TypstCreationTimestamp()
    {
    }

    /// <summary>Use the host's SOURCE_DATE_EPOCH when set (the CI convention), otherwise the current time.</summary>
    public static TypstCreationTimestamp Inherit { get; } = new InheritTimestamp();

    /// <summary>Always the current time, even if SOURCE_DATE_EPOCH is set.</summary>
    public static TypstCreationTimestamp Now { get; } = new NowTimestamp();

    /// <summary>A fixed timestamp, for byte-identical output.</summary>
    public static TypstCreationTimestamp Fixed(DateTimeOffset value) => new FixedTimestamp(value);

    public sealed record InheritTimestamp : TypstCreationTimestamp;

    public sealed record NowTimestamp : TypstCreationTimestamp;

    public sealed record FixedTimestamp(DateTimeOffset Value) : TypstCreationTimestamp;
}
```

`src/Typstium/TypstCompileOptions.cs`:

```csharp
namespace Typstium;

/// <summary>How to compile and under which policy. Configured once and shared by many requests.</summary>
public sealed record TypstCompileOptions
{
    private TypstFontOptions _fonts = TypstFontOptions.Default;
    private TypstPackageOptions _packages = TypstPackageOptions.Default;
    private TimeSpan _timeout = TimeSpan.FromSeconds(60);
    private long _maxOutputBytes = 100L * 1024 * 1024;
    private long? _memoryLimitBytes;
    private TypstCreationTimestamp _creationTimestamp = TypstCreationTimestamp.Inherit;
    private int? _jobs;
    private TypstArguments _extraGlobalArguments = TypstArguments.Empty;
    private TypstArguments _extraArguments = TypstArguments.Empty;

    public static TypstCompileOptions Default { get; } = new();

    /// <summary>Tighter limits for templates written by end users.</summary>
    public static TypstCompileOptions Untrusted { get; } = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
        MaxOutputBytes = 20L * 1024 * 1024,
        MemoryLimitBytes = 1024L * 1024 * 1024,
    };

    public TypstFontOptions Fonts
    {
        get => _fonts;
        init => _fonts = value ?? throw new ArgumentNullException(nameof(value));
    }

    public TypstPackageOptions Packages
    {
        get => _packages;
        init => _packages = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>Wall-clock limit for the Typst process. Use Timeout.InfiniteTimeSpan to disable.</summary>
    public TimeSpan Timeout
    {
        get => _timeout;
        init
        {
            if (value != System.Threading.Timeout.InfiniteTimeSpan && value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The timeout must be positive or Timeout.InfiniteTimeSpan.");
            }

            _timeout = value;
        }
    }

    public long MaxOutputBytes
    {
        get => _maxOutputBytes;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _maxOutputBytes = value;
        }
    }

    /// <summary>Memory cap enforced with a Windows job object. Ignored on other platforms.</summary>
    public long? MemoryLimitBytes
    {
        get => _memoryLimitBytes;
        init
        {
            if (value is { } limit)
            {
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit, nameof(value));
            }

            _memoryLimitBytes = value;
        }
    }

    public TypstCreationTimestamp CreationTimestamp
    {
        get => _creationTimestamp;
        init => _creationTimestamp = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>--jobs. Null leaves Typst's default (number of CPUs).</summary>
    public int? Jobs
    {
        get => _jobs;
        init
        {
            if (value is { } jobs)
            {
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(jobs, nameof(value));
            }

            _jobs = value;
        }
    }

    /// <summary>Arguments placed before the subcommand, e.g. global Typst flags.</summary>
    public TypstArguments ExtraGlobalArguments
    {
        get => _extraGlobalArguments;
        init => _extraGlobalArguments = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>Arguments appended after the generated ones.</summary>
    public TypstArguments ExtraArguments
    {
        get => _extraArguments;
        init => _extraArguments = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Receives the complete generated argument list and may change anything. It can undo safety
    /// guarantees (--root, font flags); use with care.
    /// </summary>
    public Func<TypstArguments, TypstArguments>? ArgumentCustomization { get; init; }
}
```

- [ ] **Step 4: Implement the request, result and interface**

`src/Typstium/InputKeys.cs`:

```csharp
namespace Typstium;

internal static class InputKeys
{
    public static void Validate(string key, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Input keys must not be empty.", parameterName);
        }

        if (key.IndexOf('=') >= 0)
        {
            throw new ArgumentException($"The input key '{key}' contains '=', which Typst uses to separate key and value.", parameterName);
        }
    }
}
```

`src/Typstium/TypstCompileRequest.cs`:

```csharp
using System.Collections.Immutable;

namespace Typstium;

/// <summary>What to compile: the immutable contract every backend receives.</summary>
public sealed partial record TypstCompileRequest
{
    private TypstSource _source;
    private ImmutableDictionary<string, string> _inputs = ImmutableDictionary.Create<string, string>(StringComparer.Ordinal);
    private TypstOutput _output = TypstOutput.Pdf();
    private TypstCompileOptions _options = TypstCompileOptions.Default;

    public TypstCompileRequest(TypstSource source) => _source = source ?? throw new ArgumentNullException(nameof(source));

    public TypstSource Source
    {
        get => _source;
        init => _source = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>Key/value strings visible to the template as <c>sys.inputs</c>.</summary>
    public ImmutableDictionary<string, string> Inputs
    {
        get => _inputs;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            foreach (var pair in value)
            {
                InputKeys.Validate(pair.Key, nameof(value));
                if (pair.Value is null)
                {
                    throw new ArgumentException($"The input '{pair.Key}' has a null value.", nameof(value));
                }
            }

            _inputs = value.WithComparers(StringComparer.Ordinal);
        }
    }

    public TypstOutput Output
    {
        get => _output;
        init => _output = value ?? throw new ArgumentNullException(nameof(value));
    }

    public TypstCompileOptions Options
    {
        get => _options;
        init => _options = value ?? throw new ArgumentNullException(nameof(value));
    }
}
```

`src/Typstium/TypstCompileStatus.cs`:

```csharp
namespace Typstium;

public enum TypstCompileStatus
{
    Succeeded,
    Failed,
    TimedOut,
    OutputLimitExceeded,
}
```

`src/Typstium/TypstInvocation.cs`:

```csharp
namespace Typstium;

/// <summary>How a compile ran: everything a host wants to log.</summary>
/// <param name="ExecutableFoundBy">The locator that found the executable, e.g. "WinGet".</param>
/// <param name="CommandLine">The command line with inputs and secrets redacted.</param>
/// <param name="ProcessDuration">Time spent in the Typst process.</param>
/// <param name="TotalDuration">Time spent in the whole CompileAsync call.</param>
public sealed record TypstInvocation(
    string ExecutablePath,
    string ExecutableFoundBy,
    TypstVersion TypstVersion,
    string CommandLine,
    TimeSpan ProcessDuration,
    TimeSpan TotalDuration);
```

`src/Typstium/TypstCompileResult.cs`:

```csharp
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Typstium;

/// <summary>
/// The outcome of a compile. Template problems (errors, timeouts, too much output) are results,
/// not exceptions. Status, IsSuccess and Document always agree.
/// </summary>
public sealed class TypstCompileResult
{
    private TypstCompileResult(TypstCompileStatus status, TypstDocument? document, ImmutableArray<TypstDiagnostic> diagnostics, TypstInvocation invocation)
    {
        Status = status;
        Document = document;
        Diagnostics = diagnostics.IsDefault ? ImmutableArray<TypstDiagnostic>.Empty : diagnostics;
        Invocation = invocation ?? throw new ArgumentNullException(nameof(invocation));
    }

    public TypstCompileStatus Status { get; }

    [MemberNotNullWhen(true, nameof(Document))]
    public bool IsSuccess => Status == TypstCompileStatus.Succeeded;

    public TypstDocument? Document { get; }

    public ImmutableArray<TypstDiagnostic> Diagnostics { get; }

    public IEnumerable<TypstDiagnostic> Errors => Diagnostics.Where(d => d.Severity == TypstDiagnosticSeverity.Error);

    public IEnumerable<TypstDiagnostic> Warnings => Diagnostics.Where(d => d.Severity == TypstDiagnosticSeverity.Warning);

    public TypstInvocation Invocation { get; }

    public static TypstCompileResult CreateSucceeded(TypstDocument document, ImmutableArray<TypstDiagnostic> diagnostics, TypstInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new TypstCompileResult(TypstCompileStatus.Succeeded, document, diagnostics, invocation);
    }

    public static TypstCompileResult CreateUnsuccessful(TypstCompileStatus status, ImmutableArray<TypstDiagnostic> diagnostics, TypstInvocation invocation)
    {
        if (status == TypstCompileStatus.Succeeded)
        {
            throw new ArgumentException("Use CreateSucceeded for successful results.", nameof(status));
        }

        if (status is not (TypstCompileStatus.Failed or TypstCompileStatus.TimedOut or TypstCompileStatus.OutputLimitExceeded))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown compile status.");
        }

        return new TypstCompileResult(status, document: null, diagnostics, invocation);
    }

    /// <summary>Returns this result if it succeeded; otherwise throws <see cref="TypstCompileException"/>.</summary>
    public TypstCompileResult EnsureSuccess() => IsSuccess ? this : throw new TypstCompileException(this);
}
```

`src/Typstium/Exceptions/TypstCompileException.cs`:

```csharp
using System.Text;

namespace Typstium;

/// <summary>Thrown only by <see cref="TypstCompileResult.EnsureSuccess"/>, for callers that treat failure as exceptional.</summary>
public sealed class TypstCompileException : TypstException
{
    private const int MaxDiagnosticsInMessage = 10;

    public TypstCompileException(TypstCompileResult result)
        : base(BuildMessage(result ?? throw new ArgumentNullException(nameof(result))))
    {
        Result = result;
    }

    public TypstCompileResult Result { get; }

    private static string BuildMessage(TypstCompileResult result)
    {
        var builder = new StringBuilder($"Typst compilation did not succeed: {result.Status}.");
        foreach (var diagnostic in result.Errors.Take(MaxDiagnosticsInMessage))
        {
            builder.AppendLine().Append(diagnostic);
        }

        return builder.ToString();
    }
}
```

`src/Typstium/ITypstCompiler.cs`:

```csharp
namespace Typstium;

/// <summary>Compiles Typst documents. The only method a backend implements; everything else builds on it.</summary>
public interface ITypstCompiler
{
    Task<TypstCompileResult> CompileAsync(TypstCompileRequest request, CancellationToken cancellationToken = default);
}
```

`tests/Typstium.Testing/Fakes/FakeInvocation.cs`:

```csharp
namespace Typstium.Testing.Fakes;

public static class FakeInvocation
{
    public static TypstInvocation Create() =>
        new("typst", "Fake", new TypstVersion(0, 15, 1), "typst compile", TimeSpan.FromMilliseconds(30), TimeSpan.FromMilliseconds(35));
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test --project tests/Typstium.UnitTests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src tests
git commit -m "Add compile options, request, result and the ITypstCompiler contract

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Request builders and convenience extensions

The developer-experience layer on top of the request (spec 4.6). Both builders and every extension
produce a `TypstCompileRequest`.

**Files:**
- Create: `src/Typstium/RequestBuilderCore.cs`, `src/Typstium/TypstCompileRequestBuilder.cs`, `src/Typstium/TypstFileCompileRequestBuilder.cs`, `src/Typstium/TypstCompileRequest.Builders.cs`, `src/Typstium/TypstCompilerExtensions.cs`
- Create: `tests/Typstium.Testing/Fakes/RecordingTypstCompiler.cs`
- Test: `tests/Typstium.UnitTests/TypstCompileRequestBuilderTests.cs`, `tests/Typstium.UnitTests/TypstCompilerExtensionsTests.cs`

**Interfaces:**
- Consumes: everything from Tasks 2–8.
- Produces:
  - `TypstCompileRequest.ForTemplate(ITemplateProvider, string)`, `ForTemplate(ITemplateProvider, TypstTemplateName)` and `ForMarkup(string)` return `TypstCompileRequestBuilder`
  - `TypstCompileRequest.ForFile(string)` and `ForFile(string, string root)` return `TypstFileCompileRequestBuilder`
  - `TypstCompileRequestBuilder`: `WithFile(string, ReadOnlyMemory<byte>)`, `WithFile(TypstVirtualFile)`, `WithText(string, string)`, `WithJson<T>` (3 overloads), `WithInput`, `WithJsonInput<T>` (3 overloads), `WithOutput`, `ToPdf(params PdfStandard[])`, `ToPng(int ppi = 144)`, `ToSvg()`, `WithOptions`, `ConfigureOptions`, `Build()`
  - `TypstFileCompileRequestBuilder`: the same minus `WithFile`/`WithText`/`WithJson`
  - `static class TypstCompilerExtensions`: `CompileAsync` (builder ×2, source ×3), `CompileFileAsync` ×3, `CompileMarkupAsync` ×3, `CompileTemplateAsync` ×3
  - Test fake `Typstium.Testing.Fakes.RecordingTypstCompiler : ITypstCompiler` with `IReadOnlyList<TypstCompileRequest> Requests`

- [ ] **Step 1: Write the failing tests**

`tests/Typstium.Testing/Fakes/RecordingTypstCompiler.cs`:

```csharp
using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace Typstium.Testing.Fakes;

/// <summary>Records every request and returns a successful one-byte PDF.</summary>
public sealed class RecordingTypstCompiler : ITypstCompiler
{
    private readonly ConcurrentQueue<TypstCompileRequest> _requests = new();

    public IReadOnlyList<TypstCompileRequest> Requests => _requests.ToArray();

    public Task<TypstCompileResult> CompileAsync(TypstCompileRequest request, CancellationToken cancellationToken = default)
    {
        _requests.Enqueue(request);
        var document = TypstDocument.CreateSingleFile(TypstOutputFormat.Pdf, new byte[] { 37 });
        return Task.FromResult(TypstCompileResult.CreateSucceeded(document, ImmutableArray<TypstDiagnostic>.Empty, FakeInvocation.Create()));
    }
}
```

`tests/Typstium.UnitTests/TypstCompileRequestBuilderTests.cs`:

```csharp
using System.Text;
using Typstium.Testing.Fakes;

namespace Typstium.UnitTests;

public class TypstCompileRequestBuilderTests
{
    private sealed record Model(string Name);

    [Test]
    public async Task Template_builder_collects_files_inputs_output_and_options()
    {
        var provider = new InMemoryTemplateProvider();
        var request = TypstCompileRequest.ForTemplate(provider, "license")
            .WithJson("data.json", new Model("Asa"))
            .WithFile("logo.png", new byte[] { 1, 2 })
            .WithText("notes.md", "# Notes")
            .WithInput("edition", "Enterprise")
            .ToPdf(PdfStandard.A2b)
            .WithOptions(TypstCompileOptions.Untrusted)
            .Build();

        var source = (TypstSource.TemplateSource)request.Source;
        await Assert.That(source.Files.Select(f => f.Path.Value)).IsEquivalentTo(new[] { "data.json", "logo.png", "notes.md" });
        await Assert.That(Encoding.UTF8.GetString(source.Files[0].Content.ToArray())).IsEqualTo("{\"name\":\"Asa\"}");
        await Assert.That(request.Inputs["edition"]).IsEqualTo("Enterprise");
        await Assert.That(((TypstOutput.PdfOutput)request.Output).Options.Standards[0]).IsEqualTo(PdfStandard.A2b);
        await Assert.That(request.Options).IsEqualTo(TypstCompileOptions.Untrusted);
    }

    [Test]
    public async Task File_builder_passes_json_through_inputs()
    {
        var request = TypstCompileRequest.ForFile("docs/spec.typ")
            .WithJsonInput("data", new Model("x"))
            .ToPng(300)
            .ConfigureOptions(o => o with { Jobs = 2 })
            .Build();

        await Assert.That(request.Source is TypstSource.FileSource).IsTrue();
        await Assert.That(request.Inputs["data"]).IsEqualTo("{\"name\":\"x\"}");
        await Assert.That(((TypstOutput.PngOutput)request.Output).Ppi).IsEqualTo(300);
        await Assert.That(request.Options.Jobs).IsEqualTo(2);
    }

    [Test]
    public async Task Builder_rejects_case_colliding_files_at_build_time()
    {
        var builder = TypstCompileRequest.ForMarkup("= Hi").WithText("Data.json", "{}").WithText("data.json", "{}");
        var exception = Assert.Throws<ArgumentException>(() => builder.Build());
        await Assert.That(exception!.Message).Contains("differ only by case");
    }

    [Test]
    public async Task Invalid_input_keys_fail_immediately()
    {
        var exception = Assert.Throws<ArgumentException>(() => TypstCompileRequest.ForMarkup("x").WithInput("a=b", "c"));
        await Assert.That(exception!.ParamName).IsEqualTo("key");
    }
}
```

`tests/Typstium.UnitTests/TypstCompilerExtensionsTests.cs`:

```csharp
using Typstium.Testing.Fakes;

namespace Typstium.UnitTests;

public class TypstCompilerExtensionsTests
{
    [Test]
    public async Task Each_extension_builds_the_matching_request()
    {
        var compiler = new RecordingTypstCompiler();
        var provider = new InMemoryTemplateProvider();

        await compiler.CompileFileAsync("docs/spec.typ");
        await compiler.CompileMarkupAsync("= Hi", TypstOutput.Svg());
        await compiler.CompileTemplateAsync(provider, "license", TypstOutput.Pdf(), TypstCompileOptions.Untrusted);
        await compiler.CompileAsync(TypstCompileRequest.ForMarkup("= Builder"));

        var requests = compiler.Requests;
        await Assert.That(requests.Count).IsEqualTo(4);
        await Assert.That(requests[0].Source is TypstSource.FileSource).IsTrue();
        await Assert.That(requests[1].Output.Format).IsEqualTo(TypstOutputFormat.Svg);
        await Assert.That(requests[2].Options).IsEqualTo(TypstCompileOptions.Untrusted);
        await Assert.That(((TypstSource.MarkupSource)requests[3].Source).Text).IsEqualTo("= Builder");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/Typstium.UnitTests --framework net10.0 --treenode-filter "/*/*/TypstCompileRequestBuilderTests/*"`
Expected: build FAILS with `CS0117: 'TypstCompileRequest' does not contain a definition for 'ForTemplate'`.

- [ ] **Step 3: Implement the builders**

`src/Typstium/RequestBuilderCore.cs`:

```csharp
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Typstium.Utilities;

namespace Typstium;

/// <summary>State shared by both request builders.</summary>
internal sealed class RequestBuilderCore
{
    private readonly ImmutableDictionary<string, string>.Builder _inputs =
        ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);

    public TypstOutput Output { get; set; } = TypstOutput.Pdf();

    public TypstCompileOptions Options { get; set; } = TypstCompileOptions.Default;

    public void AddInput(string key, string value)
    {
        InputKeys.Validate(key, nameof(key));
        ArgumentNullException.ThrowIfNull(value);
        _inputs[key] = value;
    }

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(JsonDefaults.UnreferencedCodeMessage)]
    [RequiresDynamicCode(JsonDefaults.DynamicCodeMessage)]
#endif
    public void AddJsonInput<T>(string key, T value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        AddInput(key, JsonSerializer.Serialize(value, options));
    }

    public void AddJsonInput<T>(string key, T value, JsonTypeInfo<T> typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        AddInput(key, JsonSerializer.Serialize(value, typeInfo));
    }

    public void Configure(Func<TypstCompileOptions, TypstCompileOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        Options = configure(Options) ?? throw new InvalidOperationException("ConfigureOptions returned null.");
    }

    public TypstCompileRequest Build(TypstSource source) =>
        new(source) { Inputs = _inputs.ToImmutable(), Output = Output, Options = Options };
}
```

`src/Typstium/TypstCompileRequestBuilder.cs`:

```csharp
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Typstium.Utilities;

namespace Typstium;

/// <summary>Builds requests for template and markup sources, which compile in a sandbox and accept virtual files.</summary>
public sealed class TypstCompileRequestBuilder
{
    private readonly Func<ImmutableArray<TypstVirtualFile>, TypstSource> _createSource;
    private readonly ImmutableArray<TypstVirtualFile>.Builder _files = ImmutableArray.CreateBuilder<TypstVirtualFile>();
    private readonly RequestBuilderCore _core = new();

    internal TypstCompileRequestBuilder(Func<ImmutableArray<TypstVirtualFile>, TypstSource> createSource) => _createSource = createSource;

    public TypstCompileRequestBuilder WithFile(string path, ReadOnlyMemory<byte> content) =>
        WithFile(TypstVirtualFile.FromBytes(TypstPath.Parse(path), content));

    public TypstCompileRequestBuilder WithFile(TypstVirtualFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        _files.Add(file);
        return this;
    }

    public TypstCompileRequestBuilder WithText(string path, string text) =>
        WithFile(TypstVirtualFile.FromText(TypstPath.Parse(path), text));

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(JsonDefaults.UnreferencedCodeMessage)]
    [RequiresDynamicCode(JsonDefaults.DynamicCodeMessage)]
#endif
    public TypstCompileRequestBuilder WithJson<T>(string path, T value) =>
        WithFile(TypstVirtualFile.FromJson(TypstPath.Parse(path), value));

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(JsonDefaults.UnreferencedCodeMessage)]
    [RequiresDynamicCode(JsonDefaults.DynamicCodeMessage)]
#endif
    public TypstCompileRequestBuilder WithJson<T>(string path, T value, JsonSerializerOptions options) =>
        WithFile(TypstVirtualFile.FromJson(TypstPath.Parse(path), value, options));

    public TypstCompileRequestBuilder WithJson<T>(string path, T value, JsonTypeInfo<T> typeInfo) =>
        WithFile(TypstVirtualFile.FromJson(TypstPath.Parse(path), value, typeInfo));

    public TypstCompileRequestBuilder WithInput(string key, string value)
    {
        _core.AddInput(key, value);
        return this;
    }

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(JsonDefaults.UnreferencedCodeMessage)]
    [RequiresDynamicCode(JsonDefaults.DynamicCodeMessage)]
#endif
    public TypstCompileRequestBuilder WithJsonInput<T>(string key, T value) => WithJsonInput(key, value, JsonDefaults.Options);

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(JsonDefaults.UnreferencedCodeMessage)]
    [RequiresDynamicCode(JsonDefaults.DynamicCodeMessage)]
#endif
    public TypstCompileRequestBuilder WithJsonInput<T>(string key, T value, JsonSerializerOptions options)
    {
        _core.AddJsonInput(key, value, options);
        return this;
    }

    public TypstCompileRequestBuilder WithJsonInput<T>(string key, T value, JsonTypeInfo<T> typeInfo)
    {
        _core.AddJsonInput(key, value, typeInfo);
        return this;
    }

    public TypstCompileRequestBuilder WithOutput(TypstOutput output)
    {
        _core.Output = output ?? throw new ArgumentNullException(nameof(output));
        return this;
    }

    public TypstCompileRequestBuilder ToPdf(params PdfStandard[] standards) =>
        WithOutput(standards is { Length: > 0 } ? TypstOutput.Pdf(standards) : TypstOutput.Pdf());

    public TypstCompileRequestBuilder ToPng(int ppi = 144) => WithOutput(TypstOutput.Png(ppi));

    public TypstCompileRequestBuilder ToSvg() => WithOutput(TypstOutput.Svg());

    public TypstCompileRequestBuilder WithOptions(TypstCompileOptions options)
    {
        _core.Options = options ?? throw new ArgumentNullException(nameof(options));
        return this;
    }

    public TypstCompileRequestBuilder ConfigureOptions(Func<TypstCompileOptions, TypstCompileOptions> configure)
    {
        _core.Configure(configure);
        return this;
    }

    public TypstCompileRequest Build() => _core.Build(_createSource(_files.ToImmutable()));
}
```

`src/Typstium/TypstFileCompileRequestBuilder.cs`:

```csharp
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Typstium.Utilities;

namespace Typstium;

/// <summary>
/// Builds requests for file sources, which compile in place. There are no file methods: a file source
/// cannot carry virtual files. Pass data through inputs instead.
/// </summary>
public sealed class TypstFileCompileRequestBuilder
{
    private readonly TypstSource _source;
    private readonly RequestBuilderCore _core = new();

    internal TypstFileCompileRequestBuilder(TypstSource source) => _source = source;

    public TypstFileCompileRequestBuilder WithInput(string key, string value)
    {
        _core.AddInput(key, value);
        return this;
    }

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(JsonDefaults.UnreferencedCodeMessage)]
    [RequiresDynamicCode(JsonDefaults.DynamicCodeMessage)]
#endif
    public TypstFileCompileRequestBuilder WithJsonInput<T>(string key, T value) => WithJsonInput(key, value, JsonDefaults.Options);

#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode(JsonDefaults.UnreferencedCodeMessage)]
    [RequiresDynamicCode(JsonDefaults.DynamicCodeMessage)]
#endif
    public TypstFileCompileRequestBuilder WithJsonInput<T>(string key, T value, JsonSerializerOptions options)
    {
        _core.AddJsonInput(key, value, options);
        return this;
    }

    public TypstFileCompileRequestBuilder WithJsonInput<T>(string key, T value, JsonTypeInfo<T> typeInfo)
    {
        _core.AddJsonInput(key, value, typeInfo);
        return this;
    }

    public TypstFileCompileRequestBuilder WithOutput(TypstOutput output)
    {
        _core.Output = output ?? throw new ArgumentNullException(nameof(output));
        return this;
    }

    public TypstFileCompileRequestBuilder ToPdf(params PdfStandard[] standards) =>
        WithOutput(standards is { Length: > 0 } ? TypstOutput.Pdf(standards) : TypstOutput.Pdf());

    public TypstFileCompileRequestBuilder ToPng(int ppi = 144) => WithOutput(TypstOutput.Png(ppi));

    public TypstFileCompileRequestBuilder ToSvg() => WithOutput(TypstOutput.Svg());

    public TypstFileCompileRequestBuilder WithOptions(TypstCompileOptions options)
    {
        _core.Options = options ?? throw new ArgumentNullException(nameof(options));
        return this;
    }

    public TypstFileCompileRequestBuilder ConfigureOptions(Func<TypstCompileOptions, TypstCompileOptions> configure)
    {
        _core.Configure(configure);
        return this;
    }

    public TypstCompileRequest Build() => _core.Build(_source);
}
```

`src/Typstium/TypstCompileRequest.Builders.cs`:

```csharp
namespace Typstium;

public sealed partial record TypstCompileRequest
{
    public static TypstCompileRequestBuilder ForTemplate(ITemplateProvider provider, string templateName) =>
        ForTemplate(provider, TypstTemplateName.Parse(templateName));

    public static TypstCompileRequestBuilder ForTemplate(ITemplateProvider provider, TypstTemplateName templateName)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(templateName);
        return new TypstCompileRequestBuilder(files => TypstSource.Template(provider, templateName, files));
    }

    public static TypstCompileRequestBuilder ForMarkup(string markup)
    {
        ArgumentNullException.ThrowIfNull(markup);
        return new TypstCompileRequestBuilder(files => TypstSource.Markup(markup, files));
    }

    public static TypstFileCompileRequestBuilder ForFile(string path) => new(TypstSource.File(path));

    public static TypstFileCompileRequestBuilder ForFile(string path, string root) => new(TypstSource.File(path, root));
}
```

- [ ] **Step 4: Implement the extensions**

`src/Typstium/TypstCompilerExtensions.cs`:

```csharp
namespace Typstium;

/// <summary>
/// Discoverable one-liners. Each builds a <see cref="TypstCompileRequest"/> and calls
/// <see cref="ITypstCompiler.CompileAsync"/>, so every backend supports them.
/// </summary>
public static class TypstCompilerExtensions
{
    public static Task<TypstCompileResult> CompileAsync(this ITypstCompiler compiler, TypstCompileRequestBuilder builder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(builder);
        return compiler.CompileAsync(builder.Build(), cancellationToken);
    }

    public static Task<TypstCompileResult> CompileAsync(this ITypstCompiler compiler, TypstFileCompileRequestBuilder builder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(builder);
        return compiler.CompileAsync(builder.Build(), cancellationToken);
    }

    public static Task<TypstCompileResult> CompileAsync(this ITypstCompiler compiler, TypstSource source, CancellationToken cancellationToken = default) =>
        Compile(compiler, new TypstCompileRequest(source), cancellationToken);

    public static Task<TypstCompileResult> CompileAsync(this ITypstCompiler compiler, TypstSource source, TypstOutput output, CancellationToken cancellationToken = default) =>
        Compile(compiler, new TypstCompileRequest(source) { Output = output }, cancellationToken);

    public static Task<TypstCompileResult> CompileAsync(this ITypstCompiler compiler, TypstSource source, TypstOutput output, TypstCompileOptions options, CancellationToken cancellationToken = default) =>
        Compile(compiler, new TypstCompileRequest(source) { Output = output, Options = options }, cancellationToken);

    public static Task<TypstCompileResult> CompileFileAsync(this ITypstCompiler compiler, string path, CancellationToken cancellationToken = default) =>
        compiler.CompileAsync(TypstSource.File(path), cancellationToken);

    public static Task<TypstCompileResult> CompileFileAsync(this ITypstCompiler compiler, string path, TypstOutput output, CancellationToken cancellationToken = default) =>
        compiler.CompileAsync(TypstSource.File(path), output, cancellationToken);

    public static Task<TypstCompileResult> CompileFileAsync(this ITypstCompiler compiler, string path, TypstOutput output, TypstCompileOptions options, CancellationToken cancellationToken = default) =>
        compiler.CompileAsync(TypstSource.File(path), output, options, cancellationToken);

    public static Task<TypstCompileResult> CompileMarkupAsync(this ITypstCompiler compiler, string markup, CancellationToken cancellationToken = default) =>
        compiler.CompileAsync(TypstSource.Markup(markup), cancellationToken);

    public static Task<TypstCompileResult> CompileMarkupAsync(this ITypstCompiler compiler, string markup, TypstOutput output, CancellationToken cancellationToken = default) =>
        compiler.CompileAsync(TypstSource.Markup(markup), output, cancellationToken);

    public static Task<TypstCompileResult> CompileMarkupAsync(this ITypstCompiler compiler, string markup, TypstOutput output, TypstCompileOptions options, CancellationToken cancellationToken = default) =>
        compiler.CompileAsync(TypstSource.Markup(markup), output, options, cancellationToken);

    public static Task<TypstCompileResult> CompileTemplateAsync(this ITypstCompiler compiler, ITemplateProvider provider, string templateName, CancellationToken cancellationToken = default) =>
        compiler.CompileAsync(TypstSource.Template(provider, TypstTemplateName.Parse(templateName)), cancellationToken);

    public static Task<TypstCompileResult> CompileTemplateAsync(this ITypstCompiler compiler, ITemplateProvider provider, string templateName, TypstOutput output, CancellationToken cancellationToken = default) =>
        compiler.CompileAsync(TypstSource.Template(provider, TypstTemplateName.Parse(templateName)), output, cancellationToken);

    public static Task<TypstCompileResult> CompileTemplateAsync(this ITypstCompiler compiler, ITemplateProvider provider, string templateName, TypstOutput output, TypstCompileOptions options, CancellationToken cancellationToken = default) =>
        compiler.CompileAsync(TypstSource.Template(provider, TypstTemplateName.Parse(templateName)), output, options, cancellationToken);

    private static Task<TypstCompileResult> Compile(ITypstCompiler compiler, TypstCompileRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(compiler);
        return compiler.CompileAsync(request, cancellationToken);
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test --project tests/Typstium.UnitTests`
Expected: PASS on all target frameworks.

- [ ] **Step 6: Commit**

```bash
git add src tests
git commit -m "Add request builders and ITypstCompiler convenience extensions

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 10: Diagnostic parser for Typst 0.15 (human format)

Moves the spike's captured diagnostics into versioned fixtures and parses Typst's human format into raw
diagnostics (spec 4.12, 5.7). This task pins Review Focus 5: malformed or truncated stderr never throws.

**Files:**
- Create: `tests/fixtures/typst-0.15/diagnostics/` (moved from `spike/diagnostics/`), `tests/fixtures/capture-diagnostics.sh`
- Create: `src/Typstium/Diagnostics/RawDiagnostic.cs`, `src/Typstium/Diagnostics/IDiagnosticParser.cs`, `src/Typstium/Diagnostics/HumanDiagnosticParserV015.cs`
- Create: `tests/Typstium.Testing/Fixtures/DiagnosticFixture.cs`, `tests/Typstium.Testing/Fixtures/DiagnosticFixtures.cs`
- Test: `tests/Typstium.UnitTests/Diagnostics/HumanDiagnosticParserV015Tests.cs`

**Interfaces:**
- Consumes: `TypstDiagnosticSeverity` (Task 4), `TypstVersion` (Task 2).
- Produces (namespace `Typstium.Diagnostics`, internal):
  - `sealed record RawLocation(string Path, int Line, int Column, int? EndColumn)`: 1-based line and column, inclusive end column, path as printed by Typst
  - `sealed record RawTraceFrame(string Message, RawLocation? Location)`
  - `sealed record RawDiagnostic(TypstDiagnosticSeverity Severity, string Message, RawLocation? Location, ImmutableArray<string> Hints, ImmutableArray<RawTraceFrame> Trace, string RawText, bool IsRecognized)`
  - `interface IDiagnosticParser { ImmutableArray<RawDiagnostic> Parse(string standardError); }`
  - `sealed class HumanDiagnosticParserV015 : IDiagnosticParser`
- Produces (test infrastructure, namespace `Typstium.Testing.Fixtures`):
  - `sealed record DiagnosticFixture(TypstVersion TypstVersion, string Name, string StandardErrorTemplate)` with `const string RootPlaceholder = "{ROOT}"` and `string StandardErrorFor(string root)`
  - `static class DiagnosticFixtures` with `IEnumerable<Func<DiagnosticFixture>> All()` and `DiagnosticFixture Get(TypstVersion version, string name)`

- [ ] **Step 1: Move the spike captures into versioned fixtures**

The captured stderr contains the absolute spike root printed by Typst
(`\\?\C:\Dev\GitHub\mgnslndh\Typstium\spike\diagnostics\root`). Replace it with the `{ROOT}` placeholder.
Run from the repo root in Git Bash:

```bash
mkdir -p tests/fixtures/typst-0.15/diagnostics/sub
cp spike/diagnostics/root/0[1-9]-*.typ spike/diagnostics/root/1[01]-*.typ tests/fixtures/typst-0.15/diagnostics/
cp spike/diagnostics/root/sub/helper.typ tests/fixtures/typst-0.15/diagnostics/sub/
cp spike/diagnostics/root/bad.png tests/fixtures/typst-0.15/diagnostics/
export ROOTPAT='\\?\C:\Dev\GitHub\mgnslndh\Typstium\spike\diagnostics\root'
for f in spike/diagnostics/output/*.human.stderr; do
  perl -pe 's/\Q$ENV{ROOTPAT}\E/{ROOT}/g' "$f" > "tests/fixtures/typst-0.15/diagnostics/$(basename "$f")"
done
grep -L '{ROOT}' tests/fixtures/typst-0.15/diagnostics/*.human.stderr
```

Expected: the final `grep -L` prints nothing (every capture contains `{ROOT}`), and 11 `.human.stderr`
files exist.

- [ ] **Step 2: Add the capture script for future Typst versions**

`tests/fixtures/capture-diagnostics.sh`:

```bash
#!/usr/bin/env bash
# Regenerates the human-format diagnostic captures for one Typst version.
# Usage (repo root, Git Bash or Linux):
#   tests/fixtures/capture-diagnostics.sh <path-to-typst> tests/fixtures/typst-<major.minor>/diagnostics
# Copy the .typ cases from an existing version folder first. Absolute roots become {ROOT}.
set -eu
TYPST="$1"
DIR="$2"
OUT=$(mktemp -d)
ABS=$(cd "$DIR" && pwd)
if command -v cygpath >/dev/null 2>&1; then
  ROOTPAT='\\?\'"$(cygpath -w "$ABS")"
else
  ROOTPAT="$ABS"
fi
export ROOTPAT
for f in "$DIR"/*.typ; do
  name=$(basename "$f" .typ)
  "$TYPST" --color never compile --root "$DIR" --diagnostic-format human "$f" "$OUT/$name.pdf" 2> "$OUT/$name.stderr" || true
  perl -pe 's/\Q$ENV{ROOTPAT}\E/{ROOT}/g' "$OUT/$name.stderr" > "$DIR/$name.human.stderr"
done
rm -rf "$OUT"
```

Run: `git update-index --add --chmod=+x tests/fixtures/capture-diagnostics.sh` after the first `git add`
(Step 9), so the script is executable on Linux.

- [ ] **Step 3: Add the fixture loader**

`tests/Typstium.Testing/Fixtures/DiagnosticFixture.cs`:

```csharp
namespace Typstium.Testing.Fixtures;

/// <summary>One captured stderr, with the absolute project root replaced by {ROOT}.</summary>
public sealed record DiagnosticFixture(TypstVersion TypstVersion, string Name, string StandardErrorTemplate)
{
    public const string RootPlaceholder = "{ROOT}";

    public string StandardErrorFor(string root) => StandardErrorTemplate.Replace(RootPlaceholder, root);

    public override string ToString() => $"typst-{TypstVersion.Major}.{TypstVersion.Minor}/{Name}";
}
```

`tests/Typstium.Testing/Fixtures/DiagnosticFixtures.cs`:

```csharp
namespace Typstium.Testing.Fixtures;

/// <summary>Loads tests/fixtures/typst-*/diagnostics/*.human.stderr, one folder per Typst version.</summary>
public static class DiagnosticFixtures
{
    private const string Suffix = ".human.stderr";
    private static readonly Lazy<IReadOnlyList<DiagnosticFixture>> Loaded = new(Load);

    public static IEnumerable<Func<DiagnosticFixture>> All() =>
        Loaded.Value.Select(fixture => (Func<DiagnosticFixture>)(() => fixture));

    public static DiagnosticFixture Get(TypstVersion version, string name) =>
        Loaded.Value.Single(fixture => fixture.TypstVersion == version && fixture.Name == name);

    private static IReadOnlyList<DiagnosticFixture> Load()
    {
        var fixtures = new List<DiagnosticFixture>();
        foreach (var versionDirectory in Directory.GetDirectories(RepositoryPaths.Fixtures, "typst-*").OrderBy(d => d, StringComparer.Ordinal))
        {
            var version = TypstVersion.Parse(Path.GetFileName(versionDirectory).Substring("typst-".Length) + ".0");
            var diagnostics = Path.Combine(versionDirectory, "diagnostics");
            foreach (var file in Directory.GetFiles(diagnostics, "*" + Suffix).OrderBy(f => f, StringComparer.Ordinal))
            {
                var fileName = Path.GetFileName(file);
                fixtures.Add(new DiagnosticFixture(version, fileName.Substring(0, fileName.Length - Suffix.Length), File.ReadAllText(file)));
            }
        }

        return fixtures;
    }
}
```

- [ ] **Step 4: Write the failing parser tests**

Expected values come from the captures. Typst 0.15 prints 0-based columns, so each expected column is
the printed column + 1. The end column is start + number of `^` − 1.

`tests/Typstium.UnitTests/Diagnostics/HumanDiagnosticParserV015Tests.cs`:

```csharp
using Typstium.Diagnostics;
using Typstium.Testing.Fixtures;

namespace Typstium.UnitTests.Diagnostics;

public class HumanDiagnosticParserV015Tests
{
    private static readonly TypstVersion V015 = new(0, 15, 0);
    private readonly HumanDiagnosticParserV015 _parser = new();

    [Test]
    [Arguments("02-unknown-variable", "unknown variable: undefined-thing", 3, 2, 16, 1, 0)]
    [Arguments("03-hint", "unknown variable: a-b", 3, 3, 5, 1, 0)]
    [Arguments("04-outside-root", "path `\"../secret.txt\"` would escape the project root", 1, 7, 21, 2, 0)]
    [Arguments("06-error-in-import", "cannot add string and integer", 2, 3, 17, 0, 1)]
    [Arguments("07-infinite-loop", "condition is always true", 1, 8, 11, 0, 0)]
    [Arguments("08-recursion", "maximum function call depth exceeded", 1, 13, 20, 0, 1)]
    [Arguments("10-multiple-errors", "expected color, gradient, or tiling, found integer", 1, 13, 13, 0, 0)]
    [Arguments("11-bad-image", "failed to decode image (Format error decoding Png: Invalid PNG signature.)", 1, 13, 28, 0, 0)]
    public async Task Parses_single_error_fixtures(string name, string message, int line, int column, int endColumn, int hints, int traces)
    {
        var fixture = DiagnosticFixtures.Get(V015, name);
        var diagnostics = _parser.Parse(fixture.StandardErrorTemplate);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
        var diagnostic = diagnostics[0];
        await Assert.That(diagnostic.IsRecognized).IsTrue();
        await Assert.That(diagnostic.Severity).IsEqualTo(TypstDiagnosticSeverity.Error);
        await Assert.That(diagnostic.Message).IsEqualTo(message);
        await Assert.That(diagnostic.Location!.Line).IsEqualTo(line);
        await Assert.That(diagnostic.Location.Column).IsEqualTo(column);
        await Assert.That(diagnostic.Location.EndColumn).IsEqualTo(endColumn);
        await Assert.That(diagnostic.Hints.Length).IsEqualTo(hints);
        await Assert.That(diagnostic.Trace.Length).IsEqualTo(traces);
    }

    [Test]
    public async Task Syntax_errors_produce_several_diagnostics()
    {
        var diagnostics = _parser.Parse(DiagnosticFixtures.Get(V015, "01-syntax").StandardErrorTemplate);
        await Assert.That(diagnostics.Select(d => d.Message)).IsEquivalentTo(new[] { "unclosed delimiter", "expected comma", "expected comma", "expected identifier" });
        await Assert.That(diagnostics[0].Location!.Column).IsEqualTo(10);
    }

    [Test]
    public async Task Warnings_are_recognized()
    {
        var diagnostic = _parser.Parse(DiagnosticFixtures.Get(V015, "05-warning-font").StandardErrorTemplate).Single();
        await Assert.That(diagnostic.Severity).IsEqualTo(TypstDiagnosticSeverity.Warning);
        await Assert.That(diagnostic.Message).IsEqualTo("unknown font family: no such font family");
        await Assert.That(diagnostic.Location!.Column).IsEqualTo(17);
        await Assert.That(diagnostic.Location.EndColumn).IsEqualTo(37);
    }

    [Test]
    public async Task Paths_and_trace_frames_are_kept_as_printed()
    {
        var diagnostic = _parser.Parse(DiagnosticFixtures.Get(V015, "06-error-in-import").StandardErrorTemplate).Single();
        await Assert.That(diagnostic.Location!.Path).IsEqualTo("{ROOT}\\sub\\helper.typ");
        var frame = diagnostic.Trace.Single();
        await Assert.That(frame.Message).IsEqualTo("while calling `greet`");
        await Assert.That(frame.Location!.Path).IsEqualTo("{ROOT}\\06-error-in-import.typ");
        await Assert.That(frame.Location.Line).IsEqualTo(2);
        await Assert.That(frame.Location.Column).IsEqualTo(2);
    }

    [Test]
    [MethodDataSource(typeof(DiagnosticFixtures), nameof(DiagnosticFixtures.All))]
    public async Task Every_fixture_parses_without_unrecognized_blocks(DiagnosticFixture fixture)
    {
        var diagnostics = _parser.Parse(fixture.StandardErrorTemplate);
        await Assert.That(diagnostics.Length).IsGreaterThan(0);
        await Assert.That(diagnostics.All(d => d.IsRecognized)).IsTrue();
    }

    [Test]
    public async Task Empty_and_noise_only_input_gives_no_diagnostics()
    {
        await Assert.That(_parser.Parse(string.Empty).Length).IsEqualTo(0);
        var noise = "downloading @preview/cmarker:0.1.10\n0 B / 139.8 KiB (  0 %),   1.4 MiB/s, ETA: 0 s\n\n";
        await Assert.That(_parser.Parse(noise).Length).IsEqualTo(0);
    }

    [Test]
    public async Task Truncated_blocks_degrade_without_throwing()
    {
        var noCaret = "error: unknown variable: x\n  ┌─ /tmp/a.typ:3:0\n  │\n3 │ #x";
        var cutLocation = "error: boom\n  ┌─ C:\\sand";
        var first = _parser.Parse(noCaret).Single();
        var second = _parser.Parse(cutLocation).Single();

        await Assert.That(first.Location!.Column).IsEqualTo(1);
        await Assert.That(first.Location.EndColumn).IsNull();
        await Assert.That(second.IsRecognized).IsTrue();
        await Assert.That(second.Location).IsNull();
    }

    [Test]
    public async Task Multi_line_spans_have_no_end_column()
    {
        var text = "error: unclosed delimiter\n  ┌─ /tmp/a.typ:1:9\n  │\n1 │ ╭ #let x = (\n2 │ │   1\n  │ ╰─^\n";
        var diagnostic = _parser.Parse(text).Single();
        await Assert.That(diagnostic.Location!.EndColumn).IsNull();
    }

    [Test]
    public async Task Unknown_text_becomes_an_unrecognized_diagnostic_with_the_raw_text()
    {
        var panic = "thread 'main' panicked at src/main.rs:10:5:\nsomething broke";
        var diagnostic = _parser.Parse(panic).Single();
        await Assert.That(diagnostic.IsRecognized).IsFalse();
        await Assert.That(diagnostic.Message).Contains("something broke");
        await Assert.That(diagnostic.RawText).Contains("panicked");
    }

    [Test]
    public async Task Unknown_lines_inside_a_block_are_kept_as_an_extra_diagnostic()
    {
        var text = "error: boom\n  ┌─ /tmp/a.typ:1:0\nan unexpected line\n";
        var diagnostics = _parser.Parse(text);
        await Assert.That(diagnostics.Length).IsEqualTo(2);
        await Assert.That(diagnostics[1].IsRecognized).IsFalse();
        await Assert.That(diagnostics[1].Message).IsEqualTo("an unexpected line");
    }
}
```

- [ ] **Step 5: Run the tests to verify they fail**

Run: `dotnet test --project tests/Typstium.UnitTests --framework net10.0 --treenode-filter "/*/*/HumanDiagnosticParserV015Tests/*"`
Expected: build FAILS with `CS0246` for `HumanDiagnosticParserV015`.

- [ ] **Step 6: Implement the raw model and the parser**

`src/Typstium/Diagnostics/RawDiagnostic.cs`:

```csharp
using System.Collections.Immutable;

namespace Typstium.Diagnostics;

/// <param name="Path">The path exactly as Typst printed it (may be a sandbox path; mapped later).</param>
/// <param name="Line">1-based.</param>
/// <param name="Column">1-based (already converted from Typst's 0-based column).</param>
/// <param name="EndColumn">1-based, inclusive; only for single-line spans.</param>
internal sealed record RawLocation(string Path, int Line, int Column, int? EndColumn);

internal sealed record RawTraceFrame(string Message, RawLocation? Location);

/// <param name="IsRecognized">False when the text did not match the expected format; Message then holds the raw text.</param>
internal sealed record RawDiagnostic(
    TypstDiagnosticSeverity Severity,
    string Message,
    RawLocation? Location,
    ImmutableArray<string> Hints,
    ImmutableArray<RawTraceFrame> Trace,
    string RawText,
    bool IsRecognized);
```

`src/Typstium/Diagnostics/IDiagnosticParser.cs`:

```csharp
using System.Collections.Immutable;

namespace Typstium.Diagnostics;

/// <summary>Parses Typst's stderr. Never throws; unrecognized text becomes unrecognized diagnostics.</summary>
internal interface IDiagnosticParser
{
    ImmutableArray<RawDiagnostic> Parse(string standardError);
}
```

`src/Typstium/Diagnostics/HumanDiagnosticParserV015.cs`:

```csharp
using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Typstium.Diagnostics;

/// <summary>
/// Parses Typst 0.15's <c>--diagnostic-format human</c> output (with <c>--color never</c>).
/// Typst 0.15 prints 1-based lines and 0-based columns; columns are converted to 1-based here.
/// </summary>
internal sealed class HumanDiagnosticParserV015 : IDiagnosticParser
{
    private const string LocationMarker = "┌─ ";

    private static readonly Regex Header = new("^(?<severity>error|warning): (?<message>.*)$", RegexOptions.CultureInvariant);
    private static readonly Regex Location = new("^(?<path>.+):(?<line>[0-9]+):(?<column>[0-9]+)$", RegexOptions.CultureInvariant);
    private static readonly Regex Hint = new("^\\s*= (?<kind>hint|note|help): (?<text>.*)$", RegexOptions.CultureInvariant);
    private static readonly Regex TraceFrame = new("^\\s+(?<message>.+?) at (?<location>.+:[0-9]+:[0-9]+)$", RegexOptions.CultureInvariant);

    public ImmutableArray<RawDiagnostic> Parse(string standardError)
    {
        var result = ImmutableArray.CreateBuilder<RawDiagnostic>();
        if (string.IsNullOrEmpty(standardError))
        {
            return result.ToImmutable();
        }

        var block = new List<string>();
        var orphans = new List<string>();
        foreach (var line in standardError.Replace("\r\n", "\n").Split('\n'))
        {
            if (Header.IsMatch(line))
            {
                FlushBlock(block, result);
                FlushUnrecognized(orphans, result);
                block.Add(line);
            }
            else if (block.Count > 0)
            {
                block.Add(line);
            }
            else if (!IsNoise(line))
            {
                orphans.Add(line);
            }
        }

        FlushBlock(block, result);
        FlushUnrecognized(orphans, result);
        return result.ToImmutable();
    }

    private static void FlushBlock(List<string> block, ImmutableArray<RawDiagnostic>.Builder result)
    {
        if (block.Count == 0)
        {
            return;
        }

        var header = Header.Match(block[0]);
        var severity = header.Groups["severity"].Value == "warning" ? TypstDiagnosticSeverity.Warning : TypstDiagnosticSeverity.Error;
        RawLocation? location = null;
        int? caretCount = null;
        var hints = ImmutableArray.CreateBuilder<string>();
        var trace = ImmutableArray.CreateBuilder<RawTraceFrame>();
        var unrecognized = new List<string>();
        var previousWasTrace = false;

        foreach (var line in block.Skip(1))
        {
            var trimmed = line.Trim();
            var isTrace = false;
            if (trimmed.Length == 0 || IsNoise(line))
            {
                // Blank lines separate sections.
            }
            else if (trimmed.StartsWith(LocationMarker, StringComparison.Ordinal))
            {
                location = ParseLocation(trimmed.Substring(LocationMarker.Length), endColumnCarets: null);
            }
            else if (IsGutterLine(line))
            {
                caretCount ??= CountCarets(line);
            }
            else if (Hint.Match(line) is { Success: true } hint)
            {
                var kind = hint.Groups["kind"].Value;
                var text = hint.Groups["text"].Value;
                hints.Add(kind == "hint" ? text : kind + ": " + text);
            }
            else if (TraceFrame.Match(line) is { Success: true } frame)
            {
                trace.Add(new RawTraceFrame(frame.Groups["message"].Value, ParseLocation(frame.Groups["location"].Value, endColumnCarets: null)));
                isTrace = true;
            }
            else if (previousWasTrace && line.StartsWith("    ", StringComparison.Ordinal))
            {
                // The source snippet printed under a trace frame.
            }
            else
            {
                unrecognized.Add(line);
            }

            previousWasTrace = isTrace;
        }

        if (location is not null && caretCount is { } carets)
        {
            location = location with { EndColumn = location.Column + carets - 1 };
        }

        var rawText = string.Join("\n", block).TrimEnd();
        result.Add(new RawDiagnostic(severity, header.Groups["message"].Value, location, hints.ToImmutable(), trace.ToImmutable(), rawText, IsRecognized: true));
        FlushUnrecognized(unrecognized, result);
        block.Clear();
    }

    private static void FlushUnrecognized(List<string> lines, ImmutableArray<RawDiagnostic>.Builder result)
    {
        var text = string.Join("\n", lines).Trim();
        lines.Clear();
        if (text.Length == 0)
        {
            return;
        }

        result.Add(new RawDiagnostic(
            TypstDiagnosticSeverity.Error, text, null, ImmutableArray<string>.Empty, ImmutableArray<RawTraceFrame>.Empty, text, IsRecognized: false));
    }

    private static RawLocation? ParseLocation(string text, int? endColumnCarets)
    {
        var match = Location.Match(text.Trim());
        if (!match.Success
            || !int.TryParse(match.Groups["line"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var line)
            || !int.TryParse(match.Groups["column"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var zeroBasedColumn))
        {
            return null;
        }

        var column = zeroBasedColumn + 1;
        return new RawLocation(match.Groups["path"].Value, line, column, endColumnCarets is { } carets ? column + carets - 1 : null);
    }

    /// <summary>Source and underline lines of the snippet: they contain the box-drawing gutter.</summary>
    private static bool IsGutterLine(string line) =>
        line.IndexOf('│') >= 0 || line.IndexOf('╭') >= 0 || line.IndexOf('╰') >= 0;

    /// <summary>The number of carets on a pure underline line ("  │   ^^^"), or null for anything else.</summary>
    private static int? CountCarets(string line)
    {
        var gutter = line.IndexOf('│');
        if (gutter < 0)
        {
            return null;
        }

        var underline = line.Substring(gutter + 1).Trim();
        if (underline.Length == 0 || underline.Any(c => c != '^'))
        {
            return null;
        }

        return underline.Length;
    }

    private static bool IsNoise(string line) =>
        line.Trim().Length == 0
        || line.StartsWith("downloading ", StringComparison.Ordinal)
        || line.IndexOf(" ETA: ", StringComparison.Ordinal) >= 0;
}
```

`CountCarets` returns `null` for source lines (they contain other characters) and for multi-line art
(`╰─^`). `caretCount ??=` keeps the first underline found.

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test --project tests/Typstium.UnitTests --treenode-filter "/*/*/HumanDiagnosticParserV015Tests/*"`
Expected: PASS. If one `[Arguments]` row disagrees on `endColumn`, count the `^` characters in that
fixture's `.human.stderr` and fix the expected value (end = column + carets − 1). Do not change the
parser to fit a miscounted row.

- [ ] **Step 8: Run all unit tests**

Run: `dotnet test --project tests/Typstium.UnitTests`
Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add tests/fixtures src tests
git update-index --chmod=+x tests/fixtures/capture-diagnostics.sh
git commit -m "Parse Typst 0.15 human diagnostics; move spike captures into versioned fixtures

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 11: Diagnostic IDs, path mapping and assembly

Turns raw diagnostics into public `TypstDiagnostic`s: stable IDs, sandbox paths mapped back to template,
virtual, package or markup files, and Typstium's added hints (spec 4.12). No sandbox path may survive
into a public diagnostic.

**Files:**
- Create: `src/Typstium/Diagnostics/DiagnosticIds.cs`, `IDiagnosticClassifier.cs`, `DiagnosticClassifierV015.cs`, `SourceMappingContext.cs`, `SourcePathMapper.cs`, `DiagnosticHints.cs`, `DiagnosticAssembler.cs` (all under `src/Typstium/Diagnostics/`)
- Test: `tests/Typstium.UnitTests/Diagnostics/DiagnosticClassifierV015Tests.cs`, `SourcePathMapperTests.cs`, `DiagnosticAssemblerTests.cs` (all under `tests/Typstium.UnitTests/Diagnostics/`)

**Interfaces:**
- Consumes: `RawDiagnostic`, `IDiagnosticParser`, `HumanDiagnosticParserV015` (Task 10); public diagnostics model (Task 4); `TypstCompileOptions` (Task 8); `TypstPath` (Task 2).
- Produces (internal, namespace `Typstium.Diagnostics`):
  - `static class DiagnosticIds`: `GenericError` (0001), `GenericWarning` (0002), `UnknownVariable` (1001), `FileNotFound` (1002), `PathEscapesRoot` (1003), `PackageNotFound` (1004), `PackageDownloadFailed` (1005), `ImageDecodeFailed` (1006), `SyntaxError` (1007), `EvaluationLimit` (1008), `UnknownFontFamily` (2001), `OutputLimitExceeded` (9001), `TimedOut` (9002), all `TypstDiagnosticId`
  - `interface IDiagnosticClassifier { TypstDiagnosticId Classify(TypstDiagnosticSeverity severity, string message); }`
  - `sealed class DiagnosticClassifierV015 : IDiagnosticClassifier`
  - `sealed record SourceMappingContext(string RootDirectory, bool IsSandbox, ImmutableHashSet<TypstPath> VirtualFiles)`
  - `sealed class SourcePathMapper(SourceMappingContext context)`: `TypstSourceFile? MapFile(string printedPath)`, `TypstLocation? Map(RawLocation? location)`
  - `static class DiagnosticHints`: `IEnumerable<string> For(TypstDiagnosticId id, TypstCompileOptions options)`, constants `SystemFontsDisabled` and `NetworkBlocked`
  - `static class DiagnosticAssembler`: `ImmutableArray<TypstDiagnostic> Assemble(string standardError, IDiagnosticParser parser, IDiagnosticClassifier classifier, SourcePathMapper mapper, TypstCompileOptions options)`

- [ ] **Step 1: Write the failing tests**

`tests/Typstium.UnitTests/Diagnostics/DiagnosticClassifierV015Tests.cs`:

```csharp
using Typstium.Diagnostics;

namespace Typstium.UnitTests.Diagnostics;

public class DiagnosticClassifierV015Tests
{
    private readonly DiagnosticClassifierV015 _classifier = new();

    [Test]
    [Arguments("unknown variable: x", "TYPST1001")]
    [Arguments("file not found (searched at /a/b.png)", "TYPST1002")]
    [Arguments("path `\"../secret.txt\"` would escape the project root", "TYPST1003")]
    [Arguments("package not found (searched for @preview/x:1.0.0)", "TYPST1004")]
    [Arguments("failed to download package (https://packages.typst.org/x)", "TYPST1005")]
    [Arguments("failed to decode image (bad)", "TYPST1006")]
    [Arguments("unclosed delimiter", "TYPST1007")]
    [Arguments("expected comma", "TYPST1007")]
    [Arguments("unexpected closing bracket", "TYPST1007")]
    [Arguments("maximum function call depth exceeded", "TYPST1008")]
    [Arguments("loop seems to be infinite", "TYPST1008")]
    [Arguments("condition is always true", "TYPST1008")]
    [Arguments("expected color, gradient, or tiling, found integer", "TYPST0001")]
    [Arguments("something new in a future Typst", "TYPST0001")]
    public async Task Classifies_errors(string message, string expectedId)
    {
        await Assert.That(_classifier.Classify(TypstDiagnosticSeverity.Error, message).Value).IsEqualTo(expectedId);
    }

    [Test]
    [Arguments("unknown font family: arial", "TYPST2001")]
    [Arguments("a warning nobody classified", "TYPST0002")]
    public async Task Classifies_warnings(string message, string expectedId)
    {
        await Assert.That(_classifier.Classify(TypstDiagnosticSeverity.Warning, message).Value).IsEqualTo(expectedId);
    }
}
```

`tests/Typstium.UnitTests/Diagnostics/SourcePathMapperTests.cs`:

```csharp
using System.Collections.Immutable;
using Typstium.Diagnostics;

namespace Typstium.UnitTests.Diagnostics;

public class SourcePathMapperTests
{
    private const string WindowsRoot = @"C:\Temp\typstium\ws-1\root";
    private const string UnixRoot = "/tmp/typstium/ws-1/root";

    private static SourcePathMapper Sandbox(string root, params string[] virtualFiles) =>
        new(new SourceMappingContext(root, IsSandbox: true, virtualFiles.Select(TypstPath.Parse).ToImmutableHashSet()));

    [Test]
    public async Task Sandbox_template_files_map_to_project_files_without_full_path()
    {
        var file = Sandbox(WindowsRoot).MapFile(@"\\?\C:\Temp\typstium\ws-1\root\sub\helper.typ");
        await Assert.That(file).IsEqualTo((TypstSourceFile)new TypstSourceFile.Project(TypstPath.Parse("sub/helper.typ"), null));
    }

    [Test]
    public async Task Virtual_files_map_to_virtual_and_windows_comparison_ignores_case()
    {
        var file = Sandbox(WindowsRoot, "data.json").MapFile(@"\\?\c:\temp\TYPSTIUM\ws-1\root\data.json");
        await Assert.That(file).IsEqualTo((TypstSourceFile)new TypstSourceFile.Virtual(TypstPath.Parse("data.json")));
    }

    [Test]
    public async Task Unix_roots_compare_ordinally()
    {
        var mapper = Sandbox(UnixRoot);
        await Assert.That(mapper.MapFile("/tmp/typstium/ws-1/root/a.typ")).IsNotNull();
        await Assert.That(mapper.MapFile("/TMP/typstium/ws-1/root/a.typ")).IsNull();
    }

    [Test]
    public async Task Packages_and_markup_are_recognized()
    {
        var mapper = Sandbox(WindowsRoot);
        await Assert.That(mapper.MapFile("@preview/cmarker:0.1.10/lib.typ"))
            .IsEqualTo((TypstSourceFile)new TypstSourceFile.Package("@preview/cmarker:0.1.10", TypstPath.Parse("lib.typ")));
        await Assert.That(mapper.MapFile(@"\\?\C:\Temp\typstium\ws-1\root\<stdin>")).IsEqualTo((TypstSourceFile)TypstSourceFile.Markup.Instance);
    }

    [Test]
    public async Task In_place_files_keep_their_full_path()
    {
        var mapper = new SourcePathMapper(new SourceMappingContext(@"C:\repo\docs", IsSandbox: false, ImmutableHashSet<TypstPath>.Empty));
        var file = mapper.MapFile(@"\\?\C:\repo\docs\spec.typ");
        await Assert.That(file).IsEqualTo((TypstSourceFile)new TypstSourceFile.Project(TypstPath.Parse("spec.typ"), @"C:\repo\docs\spec.typ"));
    }

    [Test]
    public async Task Paths_outside_the_root_are_not_mapped()
    {
        await Assert.That(Sandbox(WindowsRoot).MapFile(@"\\?\C:\Windows\win.ini")).IsNull();
    }

    [Test]
    public async Task Locations_keep_line_and_columns()
    {
        var location = Sandbox(UnixRoot).Map(new RawLocation("/tmp/typstium/ws-1/root/a.typ", 3, 2, 16));
        await Assert.That(location!.Start).IsEqualTo(new TypstPosition(3, 2));
        await Assert.That(location.End).IsEqualTo(new TypstPosition(3, 16));
    }
}
```

`tests/Typstium.UnitTests/Diagnostics/DiagnosticAssemblerTests.cs`:

```csharp
using System.Collections.Immutable;
using Typstium.Diagnostics;
using Typstium.Testing.Fixtures;

namespace Typstium.UnitTests.Diagnostics;

public class DiagnosticAssemblerTests
{
    private const string Root = @"\\?\C:\Temp\typstium\ws-1\root";
    private static readonly TypstVersion V015 = new(0, 15, 0);

    private static ImmutableArray<TypstDiagnostic> Assemble(string fixtureName, TypstCompileOptions options)
    {
        var stderr = DiagnosticFixtures.Get(V015, fixtureName).StandardErrorFor(Root);
        var mapper = new SourcePathMapper(new SourceMappingContext(@"C:\Temp\typstium\ws-1\root", IsSandbox: true, ImmutableHashSet<TypstPath>.Empty));
        return DiagnosticAssembler.Assemble(stderr, new HumanDiagnosticParserV015(), new DiagnosticClassifierV015(), mapper, options);
    }

    [Test]
    [MethodDataSource(typeof(DiagnosticFixtures), nameof(DiagnosticFixtures.All))]
    public async Task No_sandbox_path_leaks_into_public_diagnostics(DiagnosticFixture fixture)
    {
        var stderr = fixture.StandardErrorFor(Root);
        var mapper = new SourcePathMapper(new SourceMappingContext(@"C:\Temp\typstium\ws-1\root", IsSandbox: true, ImmutableHashSet<TypstPath>.Empty));
        var diagnostics = DiagnosticAssembler.Assemble(stderr, new HumanDiagnosticParserV015(), new DiagnosticClassifierV015(), mapper, TypstCompileOptions.Default);

        foreach (var diagnostic in diagnostics)
        {
            await Assert.That(diagnostic.ToString()).DoesNotContain("ws-1");
            await Assert.That(diagnostic.Location).IsNotNull();
        }
    }

    [Test]
    public async Task Unknown_font_family_gets_its_id_and_the_system_fonts_hint()
    {
        var diagnostic = Assemble("05-warning-font", TypstCompileOptions.Default).Single();
        await Assert.That(diagnostic.Id.Value).IsEqualTo("TYPST2001");
        await Assert.That(diagnostic.Hints).Contains(DiagnosticHints.SystemFontsDisabled);
        await Assert.That(diagnostic.Location!.File).IsEqualTo((TypstSourceFile)new TypstSourceFile.Project(TypstPath.Parse("05-warning-font.typ"), null));
    }

    [Test]
    public async Task No_system_fonts_hint_when_system_fonts_are_enabled()
    {
        var options = TypstCompileOptions.Default with { Fonts = TypstFontOptions.Default with { IncludeSystemFonts = true } };
        var diagnostic = Assemble("05-warning-font", options).Single();
        await Assert.That(diagnostic.Hints.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Trace_frames_are_mapped()
    {
        var diagnostic = Assemble("06-error-in-import", TypstCompileOptions.Default).Single();
        var frame = diagnostic.Trace.Single();
        await Assert.That(frame.Location!.File).IsEqualTo((TypstSourceFile)new TypstSourceFile.Project(TypstPath.Parse("06-error-in-import.typ"), null));
    }

    [Test]
    public async Task Unrecognized_text_gets_the_generic_error_id()
    {
        var mapper = new SourcePathMapper(new SourceMappingContext("/tmp/r", true, ImmutableHashSet<TypstPath>.Empty));
        var diagnostic = DiagnosticAssembler.Assemble("thread 'main' panicked", new HumanDiagnosticParserV015(), new DiagnosticClassifierV015(), mapper, TypstCompileOptions.Default).Single();
        await Assert.That(diagnostic.Id.Value).IsEqualTo("TYPST0001");
        await Assert.That(diagnostic.Message).Contains("panicked");
    }

    [Test]
    public async Task Unmappable_paths_are_reduced_to_the_file_name()
    {
        var mapper = new SourcePathMapper(new SourceMappingContext("/tmp/other", true, ImmutableHashSet<TypstPath>.Empty));
        var stderr = "error: boom\n  ┌─ /tmp/typstium/ws-9/root/a.typ:1:0\n";
        var diagnostic = DiagnosticAssembler.Assemble(stderr, new HumanDiagnosticParserV015(), new DiagnosticClassifierV015(), mapper, TypstCompileOptions.Default).Single();
        await Assert.That(diagnostic.Location).IsNull();
        await Assert.That(diagnostic.Message).IsEqualTo("boom (at a.typ:1:1)");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/Typstium.UnitTests --framework net10.0 --treenode-filter "/*/*/DiagnosticClassifierV015Tests/*"`
Expected: build FAILS with `CS0246` for `DiagnosticClassifierV015`.

- [ ] **Step 3: Implement the IDs and the classifier**

`src/Typstium/Diagnostics/DiagnosticIds.cs`:

```csharp
namespace Typstium.Diagnostics;

/// <summary>Stable diagnostic IDs. Never reuse a number.</summary>
internal static class DiagnosticIds
{
    public static TypstDiagnosticId GenericError { get; } = TypstDiagnosticId.Parse("TYPST0001");
    public static TypstDiagnosticId GenericWarning { get; } = TypstDiagnosticId.Parse("TYPST0002");
    public static TypstDiagnosticId UnknownVariable { get; } = TypstDiagnosticId.Parse("TYPST1001");
    public static TypstDiagnosticId FileNotFound { get; } = TypstDiagnosticId.Parse("TYPST1002");
    public static TypstDiagnosticId PathEscapesRoot { get; } = TypstDiagnosticId.Parse("TYPST1003");
    public static TypstDiagnosticId PackageNotFound { get; } = TypstDiagnosticId.Parse("TYPST1004");
    public static TypstDiagnosticId PackageDownloadFailed { get; } = TypstDiagnosticId.Parse("TYPST1005");
    public static TypstDiagnosticId ImageDecodeFailed { get; } = TypstDiagnosticId.Parse("TYPST1006");
    public static TypstDiagnosticId SyntaxError { get; } = TypstDiagnosticId.Parse("TYPST1007");
    public static TypstDiagnosticId EvaluationLimit { get; } = TypstDiagnosticId.Parse("TYPST1008");
    public static TypstDiagnosticId UnknownFontFamily { get; } = TypstDiagnosticId.Parse("TYPST2001");
    public static TypstDiagnosticId OutputLimitExceeded { get; } = TypstDiagnosticId.Parse("TYPST9001");
    public static TypstDiagnosticId TimedOut { get; } = TypstDiagnosticId.Parse("TYPST9002");
}
```

`src/Typstium/Diagnostics/IDiagnosticClassifier.cs`:

```csharp
namespace Typstium.Diagnostics;

internal interface IDiagnosticClassifier
{
    TypstDiagnosticId Classify(TypstDiagnosticSeverity severity, string message);
}
```

`src/Typstium/Diagnostics/DiagnosticClassifierV015.cs`:

```csharp
namespace Typstium.Diagnostics;

/// <summary>
/// Maps Typst 0.15 messages to stable IDs by prefix. If Typst rewords a message, it falls back to the
/// generic ID: less specific, never lost.
/// </summary>
internal sealed class DiagnosticClassifierV015 : IDiagnosticClassifier
{
    private static readonly (Func<string, bool> Matches, TypstDiagnosticId Id)[] ErrorRules =
    [
        (m => m.StartsWith("unknown variable:", StringComparison.Ordinal), DiagnosticIds.UnknownVariable),
        (m => m.StartsWith("file not found", StringComparison.Ordinal), DiagnosticIds.FileNotFound),
        (m => m.StartsWith("path ", StringComparison.Ordinal) && m.IndexOf("would escape the project root", StringComparison.Ordinal) >= 0, DiagnosticIds.PathEscapesRoot),
        (m => m.StartsWith("package not found", StringComparison.Ordinal), DiagnosticIds.PackageNotFound),
        (m => m.StartsWith("failed to download package", StringComparison.Ordinal), DiagnosticIds.PackageDownloadFailed),
        (m => m.StartsWith("failed to decode image", StringComparison.Ordinal), DiagnosticIds.ImageDecodeFailed),
        (IsSyntaxError, DiagnosticIds.SyntaxError),
        (m => m == "maximum function call depth exceeded" || m == "loop seems to be infinite" || m == "condition is always true", DiagnosticIds.EvaluationLimit),
    ];

    public TypstDiagnosticId Classify(TypstDiagnosticSeverity severity, string message)
    {
        if (severity == TypstDiagnosticSeverity.Warning)
        {
            return message.StartsWith("unknown font family:", StringComparison.Ordinal)
                ? DiagnosticIds.UnknownFontFamily
                : DiagnosticIds.GenericWarning;
        }

        foreach (var (matches, id) in ErrorRules)
        {
            if (matches(message))
            {
                return id;
            }
        }

        return DiagnosticIds.GenericError;
    }

    // "expected X, found Y" is a type error, not a syntax error.
    private static bool IsSyntaxError(string message) =>
        message.StartsWith("unclosed delimiter", StringComparison.Ordinal)
        || message.StartsWith("unexpected ", StringComparison.Ordinal)
        || (message.StartsWith("expected ", StringComparison.Ordinal) && message.IndexOf(", found ", StringComparison.Ordinal) < 0);
}
```

- [ ] **Step 4: Implement path mapping, hints and the assembler**

`src/Typstium/Diagnostics/SourceMappingContext.cs`:

```csharp
using System.Collections.Immutable;

namespace Typstium.Diagnostics;

/// <param name="RootDirectory">The canonical root passed to Typst as --root.</param>
/// <param name="IsSandbox">True for template and markup sources; full paths are then hidden.</param>
/// <param name="VirtualFiles">Paths written from TypstVirtualFiles (sandbox only).</param>
internal sealed record SourceMappingContext(string RootDirectory, bool IsSandbox, ImmutableHashSet<TypstPath> VirtualFiles);
```

`src/Typstium/Diagnostics/SourcePathMapper.cs`:

```csharp
using System.Text.RegularExpressions;

namespace Typstium.Diagnostics;

/// <summary>Maps paths printed by Typst back to what the caller knows. Sandbox paths never escape.</summary>
internal sealed class SourcePathMapper
{
    private const string VerbatimPrefix = @"\\?\";
    private static readonly Regex PackagePath = new("^(?<spec>@[^/]+/[^/:]+:[^/]+)/(?<path>.+)$", RegexOptions.CultureInvariant);

    private readonly SourceMappingContext _context;
    private readonly string _root;
    private readonly StringComparison _comparison;

    public SourcePathMapper(SourceMappingContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _root = Normalize(StripVerbatimPrefix(context.RootDirectory)).TrimEnd('/');
        _comparison = IsWindowsPath(_root) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    }

    public TypstSourceFile? MapFile(string printedPath)
    {
        var path = StripVerbatimPrefix(printedPath);
        if (path.StartsWith("@", StringComparison.Ordinal))
        {
            var package = PackagePath.Match(Normalize(path));
            return package.Success && TypstPath.TryParse(package.Groups["path"].Value, out var packageFile)
                ? new TypstSourceFile.Package(package.Groups["spec"].Value, packageFile)
                : null;
        }

        var normalized = Normalize(path);
        if (normalized == "<stdin>" || normalized.EndsWith("/<stdin>", StringComparison.Ordinal))
        {
            return TypstSourceFile.Markup.Instance;
        }

        if (!normalized.StartsWith(_root + "/", _comparison)
            || !TypstPath.TryParse(normalized.Substring(_root.Length + 1), out var relative))
        {
            return null;
        }

        if (!_context.IsSandbox)
        {
            return new TypstSourceFile.Project(relative, path);
        }

        return _context.VirtualFiles.Contains(relative)
            ? new TypstSourceFile.Virtual(relative)
            : new TypstSourceFile.Project(relative, FullPath: null);
    }

    public TypstLocation? Map(RawLocation? location)
    {
        if (location is null || MapFile(location.Path) is not { } file)
        {
            return null;
        }

        var end = location.EndColumn is { } endColumn ? new TypstPosition(location.Line, endColumn) : (TypstPosition?)null;
        return new TypstLocation(file, new TypstPosition(location.Line, location.Column), end);
    }

    private static string StripVerbatimPrefix(string path) =>
        path.StartsWith(VerbatimPrefix, StringComparison.Ordinal) ? path.Substring(VerbatimPrefix.Length) : path;

    private static string Normalize(string path) => path.Replace('\\', '/');

    private static bool IsWindowsPath(string path) => path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':';
}
```

`src/Typstium/Diagnostics/DiagnosticHints.cs`:

```csharp
namespace Typstium.Diagnostics;

/// <summary>Hints Typstium adds where its own defaults explain the problem.</summary>
internal static class DiagnosticHints
{
    internal const string SystemFontsDisabled =
        "system fonts are disabled by default; add a font folder to Fonts.FontPaths or set Fonts.IncludeSystemFonts.";

    internal const string NetworkBlocked =
        "network access is blocked; vendor the package into Packages.PackagePath or set Packages.NetworkAccess to Allowed.";

    public static IEnumerable<string> For(TypstDiagnosticId id, TypstCompileOptions options)
    {
        if (id.Equals(DiagnosticIds.UnknownFontFamily) && !options.Fonts.IncludeSystemFonts)
        {
            yield return SystemFontsDisabled;
        }

        if (id.Equals(DiagnosticIds.PackageDownloadFailed) && options.Packages.NetworkAccess == TypstNetworkAccess.Blocked)
        {
            yield return NetworkBlocked;
        }
    }
}
```

`src/Typstium/Diagnostics/DiagnosticAssembler.cs`:

```csharp
using System.Collections.Immutable;

namespace Typstium.Diagnostics;

internal static class DiagnosticAssembler
{
    public static ImmutableArray<TypstDiagnostic> Assemble(
        string standardError,
        IDiagnosticParser parser,
        IDiagnosticClassifier classifier,
        SourcePathMapper mapper,
        TypstCompileOptions options)
    {
        var result = ImmutableArray.CreateBuilder<TypstDiagnostic>();
        foreach (var raw in parser.Parse(standardError))
        {
            var id = raw.IsRecognized
                ? classifier.Classify(raw.Severity, raw.Message)
                : raw.Severity == TypstDiagnosticSeverity.Warning ? DiagnosticIds.GenericWarning : DiagnosticIds.GenericError;

            var location = mapper.Map(raw.Location);
            var message = raw.Location is { } printed && location is null
                ? $"{raw.Message} (at {FileName(printed.Path)}:{printed.Line}:{printed.Column})"
                : raw.Message;

            var trace = raw.Trace.Select(frame => new TypstTraceFrame(frame.Message, mapper.Map(frame.Location))).ToImmutableArray();
            var hints = raw.Hints.AddRange(DiagnosticHints.For(id, options));
            result.Add(new TypstDiagnostic(id, raw.Severity, message, location, hints, trace));
        }

        return result.ToImmutable();
    }

    // An unmappable path may be a sandbox path; only its file name is safe to show.
    private static string FileName(string printedPath)
    {
        var normalized = printedPath.Replace('\\', '/');
        return normalized.Substring(normalized.LastIndexOf('/') + 1);
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test --project tests/Typstium.UnitTests --treenode-filter "/*/*/DiagnosticAssemblerTests/*"`
Then run the classifier and mapper test classes the same way.
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src tests
git commit -m "Classify diagnostics, map sandbox paths back and add Typstium hints

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 12: Workspaces

Where `--root` points (spec 4.3, 5.1 step 4). File sources compile in place; template and markup sources
get a fresh sandbox that's deleted afterwards. Roots are **canonicalized** before Typst sees them, because
Typst prints canonical paths. GitHub's Windows runners use 8.3 temp paths (`C:\Users\RUNNER~1\…`), and
macOS temp lives behind `/var` → `/private/var`; without canonical roots, diagnostics wouldn't map back.

**Files:**
- Create: `src/Typstium/Interop/Windows/Kernel32.cs` (path functions only; Task 15 adds job objects)
- Create: `src/Typstium/Utilities/PathCanonicalizer.cs`
- Create: `src/Typstium/Workspace/WorkspaceLayout.cs`, `IWorkspace.cs`, `SandboxWorkspace.cs`, `InPlaceWorkspace.cs`, `OrphanedWorkspaceCleaner.cs`, `WorkspaceFactory.cs` (all under `src/Typstium/Workspace/`)
- Create: `tests/Typstium.Testing/Fixtures/TemporaryDirectory.cs`
- Test: `tests/Typstium.UnitTests/Workspace/SandboxWorkspaceTests.cs`, `InPlaceWorkspaceTests.cs`, `WorkspaceFactoryTests.cs` (all under `tests/Typstium.UnitTests/Workspace/`)

**Interfaces:**
- Consumes: sources and virtual files (Task 6), `TypstTemplate` (Task 5), `TypstCompileRequest`/`TypstOutput` (Tasks 7, 8), `SourceMappingContext` (Task 11).
- Produces (internal):
  - `static class Kernel32 { static string? GetLongPath(string path) }` in `Typstium.Interop.Windows`
  - `static class PathCanonicalizer { static string CanonicalizeDirectory(string existingDirectory) }`
  - `sealed record WorkspaceLayout(string RootDirectory, string MainInput, ReadOnlyMemory<byte> Stdin, string? PagedOutputDirectory, SourceMappingContext Mapping)` with `const string StdinInput = "-"`
  - `interface IWorkspace : IAsyncDisposable { WorkspaceLayout Layout { get; } }`
  - `SandboxWorkspace`: `const string Prefix = "ws-"`, `static Task<SandboxWorkspace> CreateForTemplateAsync(TypstTemplate, ImmutableArray<TypstVirtualFile>, bool needsPagedOutput, string tempRoot, CancellationToken)`, `static Task<SandboxWorkspace> CreateForMarkupAsync(string markup, ImmutableArray<TypstVirtualFile>, bool needsPagedOutput, string tempRoot, CancellationToken)`, `static string Resolve(string root, TypstPath path)`, `static Task DeleteDirectoryAsync(string path)`
  - `InPlaceWorkspace`: `static InPlaceWorkspace Create(TypstSource.FileSource source, bool needsPagedOutput, string tempRoot)`
  - `static class OrphanedWorkspaceCleaner { static int Clean(string tempRoot, TimeSpan age, DateTime utcNow) }`, which returns the number of folders deleted
  - `sealed class WorkspaceFactory(string tempRoot, TimeSpan orphanAge)`: `Task<IWorkspace> CreateAsync(TypstCompileRequest request, CancellationToken)`, `string TempRoot` (canonical), `int CleanupRuns`
- Produces (test infrastructure): `Typstium.Testing.Fixtures.TemporaryDirectory : IDisposable` with `string Path` (canonical-free, created) and `string CreateFile(string relativePath, string content)`

- [ ] **Step 1: Write the failing tests**

`tests/Typstium.Testing/Fixtures/TemporaryDirectory.cs`:

```csharp
namespace Typstium.Testing.Fixtures;

/// <summary>A unique folder under the system temp path, deleted on dispose.</summary>
public sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory(string? nameHint = null)
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "typstium-tests", (nameHint ?? "dir") + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string CreateFile(string relativePath, string content)
    {
        var full = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return full;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
```

`tests/Typstium.UnitTests/Workspace/SandboxWorkspaceTests.cs`:

```csharp
using System.Collections.Immutable;
using System.Text;
using Typstium.Testing.Fixtures;
using Typstium.Workspace;

namespace Typstium.UnitTests.Workspace;

public class SandboxWorkspaceTests
{
    private static TypstTemplate LicenseTemplate() => new(
        TypstTemplateName.Parse("license"),
        TypstPath.Parse("license.typ"),
        new TypstTemplateVersion("v1"),
        new[]
        {
            TypstTemplateFile.FromBytes(TypstPath.Parse("license.typ"), Encoding.UTF8.GetBytes("= License")),
            TypstTemplateFile.FromBytes(TypstPath.Parse("assets/logo.svg"), Encoding.UTF8.GetBytes("<svg/>")),
            TypstTemplateFile.FromBytes(TypstPath.Parse("data.json"), Encoding.UTF8.GetBytes("{\"sample\":true}")),
        });

    [Test]
    public async Task Template_sandbox_contains_bundle_and_virtual_files_with_virtual_files_winning()
    {
        using var temp = new TemporaryDirectory("sandbox");
        var data = TypstVirtualFile.FromText(TypstPath.Parse("data.json"), "{\"real\":true}");

        var workspace = await SandboxWorkspace.CreateForTemplateAsync(LicenseTemplate(), ImmutableArray.Create(data), needsPagedOutput: false, temp.Path, CancellationToken.None);
        try
        {
            var root = workspace.Layout.RootDirectory;
            await Assert.That(File.ReadAllText(Path.Combine(root, "assets", "logo.svg"))).IsEqualTo("<svg/>");
            await Assert.That(File.ReadAllText(Path.Combine(root, "data.json"))).IsEqualTo("{\"real\":true}");
            await Assert.That(workspace.Layout.MainInput).IsEqualTo(Path.Combine(root, "license.typ"));
            await Assert.That(workspace.Layout.PagedOutputDirectory).IsNull();
            await Assert.That(workspace.Layout.Mapping.IsSandbox).IsTrue();
            await Assert.That(workspace.Layout.Mapping.VirtualFiles.Contains(TypstPath.Parse("data.json"))).IsTrue();
        }
        finally
        {
            await workspace.DisposeAsync();
        }

        await Assert.That(Directory.GetDirectories(temp.Path).Length).IsEqualTo(0);
    }

    [Test]
    public async Task Markup_sandbox_sends_the_markup_over_stdin_and_creates_an_output_folder_for_paged_output()
    {
        using var temp = new TemporaryDirectory("sandbox");
        var workspace = await SandboxWorkspace.CreateForMarkupAsync("= Hi å", ImmutableArray<TypstVirtualFile>.Empty, needsPagedOutput: true, temp.Path, CancellationToken.None);
        try
        {
            await Assert.That(workspace.Layout.MainInput).IsEqualTo(WorkspaceLayout.StdinInput);
            await Assert.That(Encoding.UTF8.GetString(workspace.Layout.Stdin.ToArray())).IsEqualTo("= Hi å");
            await Assert.That(Directory.Exists(workspace.Layout.PagedOutputDirectory!)).IsTrue();
        }
        finally
        {
            await workspace.DisposeAsync();
        }
    }
}
```

`tests/Typstium.UnitTests/Workspace/InPlaceWorkspaceTests.cs`:

```csharp
using Typstium.Testing.Fixtures;
using Typstium.Workspace;

namespace Typstium.UnitTests.Workspace;

public class InPlaceWorkspaceTests
{
    [Test]
    public async Task Root_defaults_to_the_file_folder()
    {
        using var temp = new TemporaryDirectory("inplace");
        var file = temp.CreateFile("docs/spec.typ", "= Spec");
        var workspace = InPlaceWorkspace.Create((TypstSource.FileSource)TypstSource.File(file), needsPagedOutput: false, temp.Path);

        await Assert.That(Path.GetFileName(workspace.Layout.RootDirectory)).IsEqualTo("docs");
        await Assert.That(Path.GetFileName(workspace.Layout.MainInput)).IsEqualTo("spec.typ");
        await Assert.That(workspace.Layout.Mapping.IsSandbox).IsFalse();
        await workspace.DisposeAsync();
    }

    [Test]
    public async Task Files_outside_the_explicit_root_are_rejected()
    {
        using var temp = new TemporaryDirectory("inplace");
        var file = temp.CreateFile("a/spec.typ", "= Spec");
        Directory.CreateDirectory(Path.Combine(temp.Path, "b"));
        var source = (TypstSource.FileSource)TypstSource.File(file, Path.Combine(temp.Path, "b"));

        var exception = Assert.Throws<ArgumentException>(() => InPlaceWorkspace.Create(source, false, temp.Path));
        await Assert.That(exception!.Message).Contains("outside the project root");
    }

    [Test]
    public async Task Missing_files_are_rejected()
    {
        using var temp = new TemporaryDirectory("inplace");
        var source = (TypstSource.FileSource)TypstSource.File(Path.Combine(temp.Path, "missing.typ"));
        var exception = Assert.Throws<ArgumentException>(() => InPlaceWorkspace.Create(source, false, temp.Path));
        await Assert.That(exception!.Message).Contains("does not exist");
    }

    [Test]
    public async Task Paged_output_gets_a_temporary_folder_that_is_deleted()
    {
        using var temp = new TemporaryDirectory("inplace");
        var file = temp.CreateFile("spec.typ", "= Spec");
        var workspace = InPlaceWorkspace.Create((TypstSource.FileSource)TypstSource.File(file), needsPagedOutput: true, temp.Path);
        var output = workspace.Layout.PagedOutputDirectory!;
        await Assert.That(Directory.Exists(output)).IsTrue();
        await workspace.DisposeAsync();
        await Assert.That(Directory.Exists(output)).IsFalse();
    }
}
```

`tests/Typstium.UnitTests/Workspace/WorkspaceFactoryTests.cs`. It pins Review Focus 4 (cold start
under parallel load: cleanup once, fresh sandboxes kept):

```csharp
using Typstium.Testing.Fakes;
using Typstium.Testing.Fixtures;
using Typstium.Workspace;

namespace Typstium.UnitTests.Workspace;

public class WorkspaceFactoryTests
{
    [Test]
    public async Task Unknown_templates_are_a_caller_error()
    {
        using var temp = new TemporaryDirectory("factory");
        var factory = new WorkspaceFactory(temp.Path, TimeSpan.FromHours(1));
        var request = new TypstCompileRequest(TypstSource.Template(new InMemoryTemplateProvider(), TypstTemplateName.Parse("missing")));

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => factory.CreateAsync(request, CancellationToken.None));
        await Assert.That(exception.Message).Contains("missing");
    }

    [Test]
    public async Task Cleaner_deletes_only_old_workspace_folders()
    {
        using var temp = new TemporaryDirectory("cleaner");
        var old = Directory.CreateDirectory(Path.Combine(temp.Path, "ws-old"));
        var fresh = Directory.CreateDirectory(Path.Combine(temp.Path, "ws-fresh"));
        var unrelated = Directory.CreateDirectory(Path.Combine(temp.Path, "keep-me"));
        old.LastWriteTimeUtc = DateTime.UtcNow.AddHours(-2);
        unrelated.LastWriteTimeUtc = DateTime.UtcNow.AddHours(-2);

        var deleted = OrphanedWorkspaceCleaner.Clean(temp.Path, TimeSpan.FromHours(1), DateTime.UtcNow);

        await Assert.That(deleted).IsEqualTo(1);
        await Assert.That(Directory.Exists(old.FullName)).IsFalse();
        await Assert.That(Directory.Exists(fresh.FullName)).IsTrue();
        await Assert.That(Directory.Exists(unrelated.FullName)).IsTrue();
    }

    [Test]
    public async Task Parallel_cold_start_cleans_up_once_and_keeps_every_fresh_sandbox()
    {
        using var temp = new TemporaryDirectory("factory");
        var stale = Directory.CreateDirectory(Path.Combine(temp.Path, "ws-stale"));
        stale.LastWriteTimeUtc = DateTime.UtcNow.AddHours(-2);
        var factory = new WorkspaceFactory(temp.Path, TimeSpan.FromHours(1));
        var request = new TypstCompileRequest(TypstSource.Markup("= Hi"));

        var workspaces = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => factory.CreateAsync(request, CancellationToken.None))));
        try
        {
            await Assert.That(factory.CleanupRuns).IsEqualTo(1);
            await Assert.That(Directory.Exists(stale.FullName)).IsFalse();
            await Assert.That(workspaces.Select(w => w.Layout.RootDirectory).Distinct().Count()).IsEqualTo(20);
            await Assert.That(workspaces.All(w => Directory.Exists(w.Layout.RootDirectory))).IsTrue();
        }
        finally
        {
            foreach (var workspace in workspaces)
            {
                await workspace.DisposeAsync();
            }
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/Typstium.UnitTests --framework net10.0 --treenode-filter "/*/*/SandboxWorkspaceTests/*"`
Expected: build FAILS with `CS0246` for `SandboxWorkspace`.

- [ ] **Step 3: Implement path canonicalization**

`src/Typstium/Interop/Windows/Kernel32.cs`:

```csharp
using System.Runtime.InteropServices;

namespace Typstium.Interop.Windows;

/// <summary>P/Invoke declarations for kernel32.dll. Windows only; callers check the platform.</summary>
internal static class Kernel32
{
    private const string LibraryName = "kernel32.dll";

    [DllImport(LibraryName, CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    private static extern int GetLongPathNameW(string shortPath, [Out] char[]? longPath, int bufferLength);

    /// <summary>Expands 8.3 short names (e.g. RUNNER~1). Returns null if the path does not exist.</summary>
    internal static string? GetLongPath(string path)
    {
        var length = GetLongPathNameW(path, null, 0);
        if (length == 0)
        {
            return null;
        }

        var buffer = new char[length];
        var written = GetLongPathNameW(path, buffer, buffer.Length);
        return written == 0 || written >= buffer.Length ? null : new string(buffer, 0, written);
    }
}
```

This one declaration marshals a `string`. That's supported by `DllImport` under Native AOT; the
job-object declarations added in Task 15 stay blittable.

`src/Typstium/Utilities/PathCanonicalizer.cs`:

```csharp
using System.Runtime.InteropServices;
using Typstium.Interop.Windows;

namespace Typstium.Utilities;

/// <summary>
/// Produces the form of a directory path that Typst prints in diagnostics: 8.3 names expanded on
/// Windows, symlinks resolved on Unix (net8.0+). The directory must exist.
/// </summary>
internal static class PathCanonicalizer
{
    public static string CanonicalizeDirectory(string existingDirectory)
    {
        var full = Path.GetFullPath(existingDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Kernel32.GetLongPath(full) ?? full;
        }

#if NET8_0_OR_GREATER
        return ResolveSymbolicLinks(full);
#else
        return full;
#endif
    }

#if NET8_0_OR_GREATER
    private static string ResolveSymbolicLinks(string full)
    {
        var current = Path.GetPathRoot(full)!;
        foreach (var segment in full.Substring(current.Length).Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(current, segment);
            var info = new DirectoryInfo(candidate);
            current = info.LinkTarget is not null ? info.ResolveLinkTarget(returnFinalTarget: true)!.FullName : candidate;
        }

        return current;
    }
#endif
}
```

- [ ] **Step 4: Implement the workspaces**

`src/Typstium/Workspace/WorkspaceLayout.cs`:

```csharp
using Typstium.Diagnostics;

namespace Typstium.Workspace;

/// <param name="RootDirectory">Canonical --root.</param>
/// <param name="MainInput">Absolute path of the main file, or "-" when the markup comes over stdin.</param>
/// <param name="Stdin">Bytes for stdin (markup sources), otherwise empty.</param>
/// <param name="PagedOutputDirectory">Folder for {0p} page files (PNG/SVG), otherwise null.</param>
internal sealed record WorkspaceLayout(
    string RootDirectory,
    string MainInput,
    ReadOnlyMemory<byte> Stdin,
    string? PagedOutputDirectory,
    SourceMappingContext Mapping)
{
    internal const string StdinInput = "-";
}
```

`src/Typstium/Workspace/IWorkspace.cs`:

```csharp
namespace Typstium.Workspace;

/// <summary>The folders for one compile. Disposing removes anything Typstium created.</summary>
internal interface IWorkspace : IAsyncDisposable
{
    WorkspaceLayout Layout { get; }
}
```

`src/Typstium/Workspace/SandboxWorkspace.cs`:

```csharp
using System.Collections.Immutable;
using System.Text;
using Typstium.Diagnostics;
using Typstium.Utilities;

namespace Typstium.Workspace;

/// <summary>A fresh folder per compile holding only the template bundle and virtual files.</summary>
internal sealed class SandboxWorkspace : IWorkspace
{
    internal const string Prefix = "ws-";
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string _workspaceDirectory;

    private SandboxWorkspace(string workspaceDirectory, WorkspaceLayout layout)
    {
        _workspaceDirectory = workspaceDirectory;
        Layout = layout;
    }

    public WorkspaceLayout Layout { get; }

    public static async Task<SandboxWorkspace> CreateForTemplateAsync(
        TypstTemplate template, ImmutableArray<TypstVirtualFile> files, bool needsPagedOutput, string tempRoot, CancellationToken cancellationToken)
    {
        var (directory, root, output) = CreateDirectories(tempRoot, needsPagedOutput);
        try
        {
            foreach (var file in template.Files)
            {
                var source = await file.OpenReadAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    using var target = OpenForWrite(Resolve(root, file.Path));
                    await source.CopyToAsync(target, 81920, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    source.Dispose();
                }
            }

            await WriteVirtualFilesAsync(root, files, cancellationToken).ConfigureAwait(false);
            var layout = new WorkspaceLayout(root, Resolve(root, template.MainFile), ReadOnlyMemory<byte>.Empty, output, Mapping(root, files));
            return new SandboxWorkspace(directory, layout);
        }
        catch
        {
            await DeleteDirectoryAsync(directory).ConfigureAwait(false);
            throw;
        }
    }

    public static async Task<SandboxWorkspace> CreateForMarkupAsync(
        string markup, ImmutableArray<TypstVirtualFile> files, bool needsPagedOutput, string tempRoot, CancellationToken cancellationToken)
    {
        var (directory, root, output) = CreateDirectories(tempRoot, needsPagedOutput);
        try
        {
            await WriteVirtualFilesAsync(root, files, cancellationToken).ConfigureAwait(false);
            var layout = new WorkspaceLayout(root, WorkspaceLayout.StdinInput, Utf8NoBom.GetBytes(markup), output, Mapping(root, files));
            return new SandboxWorkspace(directory, layout);
        }
        catch
        {
            await DeleteDirectoryAsync(directory).ConfigureAwait(false);
            throw;
        }
    }

    public ValueTask DisposeAsync() => new(DeleteDirectoryAsync(_workspaceDirectory));

    internal static string Resolve(string root, TypstPath path) =>
        Path.Combine(new[] { root }.Concat(path.Segments).ToArray());

    /// <summary>Deletes a folder, retrying briefly: Windows can hold handles for a moment after a process exits.</summary>
    internal static async Task DeleteDirectoryAsync(string path)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }

                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                await Task.Delay(50).ConfigureAwait(false);
            }
        }

        // Left for OrphanedWorkspaceCleaner.
    }

    internal static (string Directory, string Root, string? Output) CreateDirectories(string tempRoot, bool needsPagedOutput)
    {
        var directory = Path.Combine(tempRoot, Prefix + Guid.NewGuid().ToString("N"));
        var root = Path.Combine(directory, "root");
        System.IO.Directory.CreateDirectory(root);
        string? output = null;
        if (needsPagedOutput)
        {
            output = Path.Combine(directory, "out");
            System.IO.Directory.CreateDirectory(output);
        }

        return (directory, root, output);
    }

    private static async Task WriteVirtualFilesAsync(string root, ImmutableArray<TypstVirtualFile> files, CancellationToken cancellationToken)
    {
        foreach (var file in files)
        {
            var segment = ByteMemory.AsSegment(file.Content);
            using var target = OpenForWrite(Resolve(root, file.Path));
            await target.WriteAsync(segment.Array!, segment.Offset, segment.Count, cancellationToken).ConfigureAwait(false);
        }
    }

    private static FileStream OpenForWrite(string path)
    {
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
    }

    private static SourceMappingContext Mapping(string root, ImmutableArray<TypstVirtualFile> files) =>
        new(root, IsSandbox: true, files.Select(file => file.Path).ToImmutableHashSet());
}
```

The tuple element named `Directory` shadows `System.IO.Directory` inside `CreateDirectories`, which is
why that method writes `System.IO.Directory` in full.

`src/Typstium/Workspace/InPlaceWorkspace.cs`:

```csharp
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Typstium.Diagnostics;
using Typstium.Utilities;

namespace Typstium.Workspace;

/// <summary>A file source compiled where it is. Only paged output needs a temporary folder.</summary>
internal sealed class InPlaceWorkspace : IWorkspace
{
    private readonly string? _temporaryDirectory;

    private InPlaceWorkspace(string? temporaryDirectory, WorkspaceLayout layout)
    {
        _temporaryDirectory = temporaryDirectory;
        Layout = layout;
    }

    public WorkspaceLayout Layout { get; }

    public static InPlaceWorkspace Create(TypstSource.FileSource source, bool needsPagedOutput, string tempRoot)
    {
        var mainFile = Path.GetFullPath(source.Path);
        if (!File.Exists(mainFile))
        {
            throw new ArgumentException($"The Typst file '{mainFile}' does not exist.", "request");
        }

        var rootDirectory = source.Root is null ? Path.GetDirectoryName(mainFile)! : Path.GetFullPath(source.Root);
        if (!Directory.Exists(rootDirectory))
        {
            throw new ArgumentException($"The project root '{rootDirectory}' does not exist.", "request");
        }

        var root = PathCanonicalizer.CanonicalizeDirectory(rootDirectory);
        var main = Path.Combine(PathCanonicalizer.CanonicalizeDirectory(Path.GetDirectoryName(mainFile)!), Path.GetFileName(mainFile));
        var comparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!main.StartsWith(root + Path.DirectorySeparatorChar, comparison))
        {
            throw new ArgumentException($"The file '{mainFile}' is outside the project root '{rootDirectory}'.", "request");
        }

        string? temporaryDirectory = null;
        string? output = null;
        if (needsPagedOutput)
        {
            (temporaryDirectory, _, output) = SandboxWorkspace.CreateDirectories(tempRoot, needsPagedOutput: true);
        }

        var mapping = new SourceMappingContext(root, IsSandbox: false, ImmutableHashSet<TypstPath>.Empty);
        return new InPlaceWorkspace(temporaryDirectory, new WorkspaceLayout(root, main, ReadOnlyMemory<byte>.Empty, output, mapping));
    }

    public ValueTask DisposeAsync() =>
        _temporaryDirectory is null ? default : new ValueTask(SandboxWorkspace.DeleteDirectoryAsync(_temporaryDirectory));
}
```

For a paged in-place compile, `CreateDirectories` also creates an unused `root/` folder inside the
temporary directory. That's harmless, and it's deleted together with `out/`.

`src/Typstium/Workspace/OrphanedWorkspaceCleaner.cs`:

```csharp
namespace Typstium.Workspace;

/// <summary>Removes sandbox folders left behind when a host was killed hard.</summary>
internal static class OrphanedWorkspaceCleaner
{
    /// <returns>The number of folders deleted.</returns>
    public static int Clean(string tempRoot, TimeSpan age, DateTime utcNow)
    {
        if (!Directory.Exists(tempRoot))
        {
            return 0;
        }

        var deleted = 0;
        foreach (var directory in new DirectoryInfo(tempRoot).EnumerateDirectories(SandboxWorkspace.Prefix + "*"))
        {
            if (utcNow - directory.LastWriteTimeUtc < age)
            {
                continue;
            }

            try
            {
                directory.Delete(recursive: true);
                deleted++;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // In use or locked; try again on a later start.
            }
        }

        return deleted;
    }
}
```

`src/Typstium/Workspace/WorkspaceFactory.cs`:

```csharp
using Typstium.Utilities;

namespace Typstium.Workspace;

/// <summary>Creates the workspace for a request. The source kind decides in place vs sandbox.</summary>
internal sealed class WorkspaceFactory
{
    private readonly Lazy<string> _tempRoot;
    private readonly TimeSpan _orphanAge;
    private int _cleanupStarted;
    private int _cleanupRuns;

    public WorkspaceFactory(string tempRoot, TimeSpan orphanAge)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tempRoot);
        _orphanAge = orphanAge;
        _tempRoot = new Lazy<string>(() =>
        {
            Directory.CreateDirectory(tempRoot);
            return PathCanonicalizer.CanonicalizeDirectory(tempRoot);
        });
    }

    public string TempRoot => _tempRoot.Value;

    public int CleanupRuns => Volatile.Read(ref _cleanupRuns);

    public async Task<IWorkspace> CreateAsync(TypstCompileRequest request, CancellationToken cancellationToken)
    {
        CleanUpOnce();
        var paged = request.Output.IsPaged;
        switch (request.Source)
        {
            case TypstSource.FileSource file:
                return InPlaceWorkspace.Create(file, paged, TempRoot);

            case TypstSource.TemplateSource source:
                var template = await source.Provider.GetTemplateAsync(source.Name, cancellationToken).ConfigureAwait(false)
                    ?? throw new ArgumentException($"The template '{source.Name}' was not found by {source.Provider.GetType().Name}.", nameof(request));
                return await SandboxWorkspace.CreateForTemplateAsync(template, source.Files, paged, TempRoot, cancellationToken).ConfigureAwait(false);

            case TypstSource.MarkupSource markup:
                return await SandboxWorkspace.CreateForMarkupAsync(markup.Text, markup.Files, paged, TempRoot, cancellationToken).ConfigureAwait(false);

            default:
                throw new ArgumentException($"Unsupported source kind '{request.Source.GetType().Name}'.", nameof(request));
        }
    }

    private void CleanUpOnce()
    {
        if (Interlocked.Exchange(ref _cleanupStarted, 1) != 0)
        {
            return;
        }

        Interlocked.Increment(ref _cleanupRuns);
        OrphanedWorkspaceCleaner.Clean(TempRoot, _orphanAge, DateTime.UtcNow);
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test --project tests/Typstium.UnitTests --treenode-filter "/*/*/WorkspaceFactoryTests/*"`
Then run `SandboxWorkspaceTests` and `InPlaceWorkspaceTests` the same way.
Expected: PASS on every framework.

- [ ] **Step 6: Commit**

```bash
git add src tests
git commit -m "Add in-place and sandbox workspaces with canonical roots and orphan cleanup

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 13: Typst capabilities and `typst info` parsing

The named capability facts and the parser for `typst info --format json` (spec 5.7). The profile factory
that runs Typst comes in Task 16.

**Files:**
- Create: `src/Typstium/Capabilities/TypstInfo.cs`, `src/Typstium/Capabilities/TypstInfoParser.cs`, `src/Typstium/Capabilities/TypstCapabilities.cs`
- Test: `tests/Typstium.UnitTests/Capabilities/TypstInfoParserTests.cs`

**Interfaces:**
- Consumes: `TypstVersion` (Task 2), `PdfStandard` (Task 7), `TypstProcessOutputException` (Task 4).
- Produces (internal, namespace `Typstium.Capabilities`):
  - `sealed record TypstInfo(TypstVersion Version, ImmutableHashSet<string> Features)`
  - `static class TypstInfoParser`: `TypstInfo Parse(string json)` and `TypstVersion ParseVersionOutput(string text)` (for `typst --version`, e.g. `typst 0.14.2 (abc123)`); both throw `TypstProcessOutputException` on unexpected input
  - `sealed record TypstCapabilities(TypstVersion Version, bool SupportsHtml, ImmutableHashSet<PdfStandard> PdfStandards)`

- [ ] **Step 1: Write the failing tests**

`tests/Typstium.UnitTests/Capabilities/TypstInfoParserTests.cs`. The JSON is the spike's real
`typst info --format json` output:

```csharp
using Typstium.Capabilities;

namespace Typstium.UnitTests.Capabilities;

public class TypstInfoParserTests
{
    private const string Typst0151Info =
        "{\"version\":\"0.15.1\",\"build\":{\"commit\":\"9dfd3a08500b7896045f907433cf7b4b02434fad\",\"platform\":{\"os\":\"windows\",\"arch\":\"x86_64\"},\"settings\":{\"self-update\":true,\"http-server\":true}},\"features\":{\"html\":false,\"bundle\":false,\"a11y-extras\":false},\"fonts\":{\"paths\":[],\"system\":true,\"embedded\":true},\"packages\":{\"package-path\":\"C:\\\\Users\\\\x\\\\AppData\\\\Roaming\\\\typst/packages\",\"package-cache-path\":\"C:\\\\Users\\\\x\\\\AppData\\\\Local\\\\typst/packages\"}}";

    [Test]
    public async Task Parses_version_and_available_features()
    {
        var info = TypstInfoParser.Parse(Typst0151Info);
        await Assert.That(info.Version).IsEqualTo(new TypstVersion(0, 15, 1));
        await Assert.That(info.Features.Contains("html")).IsTrue();
        await Assert.That(info.Features.Contains("bundle")).IsTrue();
    }

    [Test]
    public async Task Missing_features_object_means_no_features()
    {
        var info = TypstInfoParser.Parse("{\"version\":\"0.16.0\"}");
        await Assert.That(info.Features.Count).IsEqualTo(0);
    }

    [Test]
    [Arguments("not json")]
    [Arguments("{}")]
    [Arguments("{\"version\":\"banana\"}")]
    public async Task Unexpected_json_is_a_process_output_error(string json)
    {
        var exception = Assert.Throws<TypstProcessOutputException>(() => TypstInfoParser.Parse(json));
        await Assert.That(exception).IsNotNull();
    }

    [Test]
    [Arguments("typst 0.14.2 (abcdef12)", 0, 14, 2)]
    [Arguments("typst 0.15.1 (9dfd3a08)\n", 0, 15, 1)]
    public async Task Parses_version_output(string text, int major, int minor, int patch)
    {
        await Assert.That(TypstInfoParser.ParseVersionOutput(text)).IsEqualTo(new TypstVersion(major, minor, patch));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/Typstium.UnitTests --framework net10.0 --treenode-filter "/*/*/TypstInfoParserTests/*"`
Expected: build FAILS with `CS0246` for `TypstInfoParser`.

- [ ] **Step 3: Implement**

`src/Typstium/Capabilities/TypstInfo.cs`:

```csharp
using System.Collections.Immutable;

namespace Typstium.Capabilities;

/// <param name="Features">Feature names Typst knows (enabled or not), e.g. "html".</param>
internal sealed record TypstInfo(TypstVersion Version, ImmutableHashSet<string> Features);
```

`src/Typstium/Capabilities/TypstCapabilities.cs`:

```csharp
using System.Collections.Immutable;

namespace Typstium.Capabilities;

/// <summary>
/// Named facts about an installed Typst. Pipeline components ask these questions; only
/// TypstCliProfileFactory compares version numbers (spec 5.7).
/// </summary>
/// <param name="Version">For TypstInvocation and telemetry only, never for decisions outside the factory.</param>
internal sealed record TypstCapabilities(TypstVersion Version, bool SupportsHtml, ImmutableHashSet<PdfStandard> PdfStandards);
```

`src/Typstium/Capabilities/TypstInfoParser.cs`:

```csharp
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Typstium.Capabilities;

internal static class TypstInfoParser
{
    private static readonly Regex VersionOutput = new("^typst (?<version>\\S+)", RegexOptions.CultureInvariant);

    public static TypstInfo Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("version", out var versionElement)
                || versionElement.ValueKind != JsonValueKind.String
                || !TypstVersion.TryParse(versionElement.GetString(), out var version))
            {
                throw new TypstProcessOutputException("'typst info --format json' did not report a version.");
            }

            var features = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
            if (root.TryGetProperty("features", out var featuresElement) && featuresElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var feature in featuresElement.EnumerateObject())
                {
                    features.Add(feature.Name);
                }
            }

            return new TypstInfo(version, features.ToImmutable());
        }
        catch (JsonException exception)
        {
            throw new TypstProcessOutputException("'typst info --format json' returned invalid JSON.", exception);
        }
    }

    public static TypstVersion ParseVersionOutput(string text)
    {
        var match = VersionOutput.Match(text.Trim());
        if (!match.Success || !TypstVersion.TryParse(match.Groups["version"].Value, out var version))
        {
            throw new TypstProcessOutputException($"Could not read a version from 'typst --version' output: '{text.Trim()}'.");
        }

        return version;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --project tests/Typstium.UnitTests --treenode-filter "/*/*/TypstInfoParserTests/*"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src tests
git commit -m "Parse typst info output into capability facts

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 14: Command line and environment for Typst 0.15

Turns a request plus its workspace into Typst arguments and environment changes (spec 4.8, 5.2, 5.3).

**Files:**
- Create: `src/Typstium/CommandLine/CompileArgumentsInput.cs`, `ICompileArgumentsBuilder.cs`, `CompileArgumentsBuilderV015.cs`, `CommandLineLimits.cs`, `EnvironmentChanges.cs`, `ProcessEnvironmentBuilder.cs` (all under `src/Typstium/CommandLine/`)
- Create: `tests/Typstium.UnitTests/Testing/TestCapabilities.cs`
- Test: `tests/Typstium.UnitTests/CommandLine/CompileArgumentsBuilderV015Tests.cs`, `ProcessEnvironmentBuilderTests.cs`, `CommandLineLimitsTests.cs` (all under `tests/Typstium.UnitTests/CommandLine/`)

**Interfaces:**
- Consumes: `TypstArguments` (Task 3), request/options/output (Tasks 7, 8), `WorkspaceLayout` (Task 12), `TypstCapabilities` (Task 13).
- Produces (internal, namespace `Typstium.CommandLine`):
  - `sealed record CompileArgumentsInput(TypstCompileRequest Request, WorkspaceLayout Layout, TypstCapabilities Capabilities)`
  - `interface ICompileArgumentsBuilder { TypstArguments Build(CompileArgumentsInput input); }`
  - `sealed class CompileArgumentsBuilderV015 : ICompileArgumentsBuilder`
  - `static class CommandLineLimits`: `const int MaxLength = 32000`, `void EnsureFits(string executable, TypstArguments arguments)` throws `ArgumentException` (param `request`)
  - `sealed record EnvironmentChanges(ImmutableDictionary<string, string> Set, ImmutableArray<string> Remove)`: `static None`, `void ApplyTo(ProcessStartInfo startInfo)`
  - `static class ProcessEnvironmentBuilder`: `const string DeadProxy = "http://127.0.0.1:9"`, `IEnumerable<string> CurrentVariableNames()`, `EnvironmentChanges Build(IEnumerable<string> inheritedNames, TypstCompileOptions options)`, `EnvironmentChanges BuildForProbe(IEnumerable<string> inheritedNames)`
- Produces (unit test infrastructure): `Typstium.UnitTests.Testing.TestCapabilities.V015` (html supported) and `.V015WithoutHtml`

- [ ] **Step 1: Write the failing tests**

`tests/Typstium.UnitTests/Testing/TestCapabilities.cs`:

```csharp
using System.Collections.Immutable;
using Typstium.Capabilities;

namespace Typstium.UnitTests.Testing;

internal static class TestCapabilities
{
    public static TypstCapabilities V015 { get; } =
        new(new TypstVersion(0, 15, 1), SupportsHtml: true, PdfStandard.KnownInTypst015.ToImmutableHashSet());

    public static TypstCapabilities V015WithoutHtml { get; } = V015 with { SupportsHtml = false };
}
```

`tests/Typstium.UnitTests/CommandLine/CompileArgumentsBuilderV015Tests.cs`:

```csharp
using System.Collections.Immutable;
using Typstium.CommandLine;
using Typstium.Diagnostics;
using Typstium.UnitTests.Testing;
using Typstium.Workspace;

namespace Typstium.UnitTests.CommandLine;

public class CompileArgumentsBuilderV015Tests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "typstium", "ws-1", "root");
    private static readonly string Out = Path.Combine(Path.GetTempPath(), "typstium", "ws-1", "out");

    private static WorkspaceLayout Layout(string mainInput = "-", string? pagedOutput = null) =>
        new(Root, mainInput, ReadOnlyMemory<byte>.Empty, pagedOutput, new SourceMappingContext(Root, true, ImmutableHashSet<TypstPath>.Empty));

    private static IReadOnlyList<string> Build(TypstCompileRequest request, WorkspaceLayout? layout = null) =>
        new CompileArgumentsBuilderV015().Build(new CompileArgumentsInput(request, layout ?? Layout(), TestCapabilities.V015)).ToValues();

    [Test]
    public async Task Default_markup_pdf_compile()
    {
        var arguments = Build(new TypstCompileRequest(TypstSource.Markup("= Hi")));
        await Assert.That(arguments).IsEquivalentTo(new[]
        {
            "--color", "never", "compile", "--root", Root, "--diagnostic-format", "human",
            "--ignore-system-fonts", "--format", "pdf", "-", "-",
        });
    }

    [Test]
    public async Task Fonts_packages_jobs_timestamp_and_inputs()
    {
        var options = TypstCompileOptions.Default with
        {
            Fonts = TypstFontOptions.Default with { FontPaths = ImmutableArray.Create("fonts-a", "fonts-b"), IncludeEmbeddedFonts = false },
            Packages = TypstPackageOptions.Default with { PackagePath = "vendor", PackageCachePath = "cache" },
            Jobs = 2,
            CreationTimestamp = TypstCreationTimestamp.Fixed(DateTimeOffset.FromUnixTimeSeconds(1700000000)),
        };
        var request = new TypstCompileRequest(TypstSource.Markup("x"))
        {
            Options = options,
            Inputs = ImmutableDictionary<string, string>.Empty.Add("z", "last").Add("a", "first"),
        };

        var arguments = Build(request, Layout(mainInput: Path.Combine(Root, "main.typ")));

        var expectedFontPath = Path.GetFullPath("fonts-a") + Path.PathSeparator + Path.GetFullPath("fonts-b");
        await Assert.That(arguments).IsEquivalentTo(new[]
        {
            "--color", "never", "compile", "--root", Root, "--diagnostic-format", "human",
            "--font-path", expectedFontPath, "--ignore-system-fonts", "--ignore-embedded-fonts",
            "--package-path", Path.GetFullPath("vendor"), "--package-cache-path", Path.GetFullPath("cache"),
            "--jobs", "2", "--creation-timestamp", "1700000000",
            "--format", "pdf",
            "--input", "a=first", "--input", "z=last",
            Path.Combine(Root, "main.typ"), "-",
        });
    }

    [Test]
    public async Task Inputs_are_secret_in_the_safe_rendering()
    {
        var request = new TypstCompileRequest(TypstSource.Markup("x")) { Inputs = ImmutableDictionary<string, string>.Empty.Add("license", "{\"id\":1}") };
        var arguments = new CompileArgumentsBuilderV015().Build(new CompileArgumentsInput(request, Layout(), TestCapabilities.V015));
        await Assert.That(arguments.RenderSafe()).DoesNotContain("\"id\"");
        await Assert.That(arguments.RenderSafe()).Contains("--input [REDACTED]");
    }

    [Test]
    public async Task Png_writes_zero_padded_pages_to_the_output_folder()
    {
        var request = new TypstCompileRequest(TypstSource.Markup("x"))
        {
            Output = TypstOutput.Png(300, TypstPageSelection.Of(TypstPageRange.Between(2, 3))),
        };
        var arguments = Build(request, Layout(pagedOutput: Out));

        await Assert.That(arguments.Skip(8)).IsEquivalentTo(new[]
        {
            "--format", "png", "--ppi", "300", "--pages", "2-3", "-", Path.Combine(Out, "{0p}.png"),
        });
    }

    [Test]
    public async Task Pdf_standards_tags_and_html_feature()
    {
        var pdf = new TypstCompileRequest(TypstSource.Markup("x"))
        {
            Output = TypstOutput.Pdf(new TypstPdfOptions { Standards = ImmutableArray.Create(PdfStandard.A2b, PdfStandard.UA1), Tagged = false }),
        };
        var html = new TypstCompileRequest(TypstSource.Markup("x")) { Output = TypstOutput.Html() };

        await Assert.That(Build(pdf).Skip(8)).IsEquivalentTo(new[] { "--format", "pdf", "--pdf-standard", "a-2b,ua-1", "--no-pdf-tags", "-", "-" });
        await Assert.That(Build(html).Skip(8)).IsEquivalentTo(new[] { "--format", "html", "--features", "html", "-", "-" });
    }

    [Test]
    public async Task Extra_arguments_and_customization_come_last()
    {
        var options = TypstCompileOptions.Default with
        {
            ExtraGlobalArguments = TypstArguments.Of("--cert", "ca.pem"),
            ExtraArguments = TypstArguments.Of("--timings", "t.json"),
            ArgumentCustomization = args => args.Add("--custom"),
        };
        var arguments = Build(new TypstCompileRequest(TypstSource.Markup("x")) { Options = options });

        await Assert.That(arguments.Take(5)).IsEquivalentTo(new[] { "--color", "never", "--cert", "ca.pem", "compile" });
        await Assert.That(arguments.Skip(arguments.Count - 3)).IsEquivalentTo(new[] { "--timings", "t.json", "--custom" });
    }

    [Test]
    public async Task Customization_returning_null_is_an_error()
    {
        var options = TypstCompileOptions.Default with { ArgumentCustomization = _ => null! };
        var exception = Assert.Throws<InvalidOperationException>(() => Build(new TypstCompileRequest(TypstSource.Markup("x")) { Options = options }));
        await Assert.That(exception).IsNotNull();
    }
}
```

`tests/Typstium.UnitTests/CommandLine/ProcessEnvironmentBuilderTests.cs`:

```csharp
using System.Diagnostics;
using Typstium.CommandLine;

namespace Typstium.UnitTests.CommandLine;

public class ProcessEnvironmentBuilderTests
{
    private static readonly string[] Inherited =
        ["PATH", "TYPST_FONT_PATHS", "typst_root", "https_proxy", "NO_PROXY", "SOURCE_DATE_EPOCH", "HOME"];

    [Test]
    public async Task Typst_variables_are_removed_and_the_network_is_blocked_by_default()
    {
        var changes = ProcessEnvironmentBuilder.Build(Inherited, TypstCompileOptions.Default);

        await Assert.That(changes.Remove).IsEquivalentTo(new[] { "TYPST_FONT_PATHS", "typst_root", "https_proxy", "NO_PROXY" });
        await Assert.That(changes.Set["HTTPS_PROXY"]).IsEqualTo(ProcessEnvironmentBuilder.DeadProxy);
        await Assert.That(changes.Set["HTTP_PROXY"]).IsEqualTo(ProcessEnvironmentBuilder.DeadProxy);
        await Assert.That(changes.Set["ALL_PROXY"]).IsEqualTo(ProcessEnvironmentBuilder.DeadProxy);
    }

    [Test]
    public async Task Allowed_network_keeps_proxies_and_now_removes_source_date_epoch()
    {
        var options = TypstCompileOptions.Default with
        {
            Packages = TypstPackageOptions.Default with { NetworkAccess = TypstNetworkAccess.Allowed },
            CreationTimestamp = TypstCreationTimestamp.Now,
        };
        var changes = ProcessEnvironmentBuilder.Build(Inherited, options);

        await Assert.That(changes.Remove).IsEquivalentTo(new[] { "TYPST_FONT_PATHS", "typst_root", "SOURCE_DATE_EPOCH" });
        await Assert.That(changes.Set.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Probe_environment_only_removes_typst_variables()
    {
        var changes = ProcessEnvironmentBuilder.BuildForProbe(Inherited);
        await Assert.That(changes.Remove).IsEquivalentTo(new[] { "TYPST_FONT_PATHS", "typst_root" });
        await Assert.That(changes.Set.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ApplyTo_removes_then_sets()
    {
        var startInfo = new ProcessStartInfo("x");
        startInfo.Environment["TYPST_ROOT"] = "/somewhere";
        var changes = ProcessEnvironmentBuilder.Build(new[] { "TYPST_ROOT" }, TypstCompileOptions.Default);
        changes.ApplyTo(startInfo);

        await Assert.That(startInfo.Environment.ContainsKey("TYPST_ROOT")).IsFalse();
        await Assert.That(startInfo.Environment["HTTPS_PROXY"]).IsEqualTo(ProcessEnvironmentBuilder.DeadProxy);
    }
}
```

`tests/Typstium.UnitTests/CommandLine/CommandLineLimitsTests.cs`:

```csharp
using Typstium.CommandLine;

namespace Typstium.UnitTests.CommandLine;

public class CommandLineLimitsTests
{
    [Test]
    public async Task Long_command_lines_are_rejected_with_advice()
    {
        var arguments = TypstArguments.Of("compile").AddSwitchSecret("--input", "data=" + new string('x', CommandLineLimits.MaxLength));
        var exception = Assert.Throws<ArgumentException>(() => CommandLineLimits.EnsureFits("typst", arguments));
        await Assert.That(exception!.Message).Contains("virtual file");
        await Assert.That(exception.ParamName).IsEqualTo("request");
    }

    [Test]
    public async Task Normal_command_lines_fit()
    {
        CommandLineLimits.EnsureFits("typst", TypstArguments.Of("compile", "main.typ", "-"));
        await Assert.That(CommandLineLimits.MaxLength).IsEqualTo(32000);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/Typstium.UnitTests --framework net10.0 --treenode-filter "/*/*/CompileArgumentsBuilderV015Tests/*"`
Expected: build FAILS with `CS0246` for `CompileArgumentsBuilderV015`.

- [ ] **Step 3: Implement the argument builder and limits**

`src/Typstium/CommandLine/CompileArgumentsInput.cs`:

```csharp
using Typstium.Capabilities;
using Typstium.Workspace;

namespace Typstium.CommandLine;

internal sealed record CompileArgumentsInput(TypstCompileRequest Request, WorkspaceLayout Layout, TypstCapabilities Capabilities);
```

`src/Typstium/CommandLine/ICompileArgumentsBuilder.cs`:

```csharp
namespace Typstium.CommandLine;

/// <summary>Version-specific: chosen by TypstCliProfileFactory (spec 5.7).</summary>
internal interface ICompileArgumentsBuilder
{
    TypstArguments Build(CompileArgumentsInput input);
}
```

`src/Typstium/CommandLine/CompileArgumentsBuilderV015.cs`:

```csharp
using System.Globalization;
using Typstium.Workspace;

namespace Typstium.CommandLine;

/// <summary>
/// Builds `typst compile` arguments for Typst 0.15 in a fixed order: global flags, subcommand, root,
/// diagnostics, fonts, packages, jobs, timestamp, format and output options, inputs, input, output,
/// extra arguments, then the customization hook.
/// </summary>
internal sealed class CompileArgumentsBuilderV015 : ICompileArgumentsBuilder
{
    public TypstArguments Build(CompileArgumentsInput input)
    {
        var request = input.Request;
        var options = request.Options;
        var layout = input.Layout;

        var arguments = TypstArguments.Empty
            .AddSwitch("--color", "never")
            .AddRange(options.ExtraGlobalArguments)
            .Add("compile")
            .AddSwitch("--root", layout.RootDirectory)
            .AddSwitch("--diagnostic-format", "human");

        arguments = AddFonts(arguments, options.Fonts);
        arguments = AddPackages(arguments, options.Packages);

        if (options.Jobs is { } jobs)
        {
            arguments = arguments.AddSwitch("--jobs", jobs.ToString(CultureInfo.InvariantCulture));
        }

        if (options.CreationTimestamp is TypstCreationTimestamp.FixedTimestamp timestamp)
        {
            arguments = arguments.AddSwitch("--creation-timestamp", timestamp.Value.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
        }

        arguments = arguments.AddSwitch("--format", request.Output.FileExtension);
        arguments = AddOutputOptions(arguments, request.Output);

        foreach (var pair in request.Inputs.OrderBy(input => input.Key, StringComparer.Ordinal))
        {
            arguments = arguments.AddSwitchSecret("--input", pair.Key + "=" + pair.Value);
        }

        arguments = arguments
            .Add(layout.MainInput)
            .Add(OutputTarget(request.Output, layout))
            .AddRange(options.ExtraArguments);

        if (options.ArgumentCustomization is { } customize)
        {
            arguments = customize(arguments) ?? throw new InvalidOperationException("ArgumentCustomization returned null.");
        }

        return arguments;
    }

    private static TypstArguments AddFonts(TypstArguments arguments, TypstFontOptions fonts)
    {
        if (fonts.FontPaths.Length > 0)
        {
            var paths = string.Join(Path.PathSeparator.ToString(), fonts.FontPaths.Select(Path.GetFullPath));
            arguments = arguments.AddSwitch("--font-path", paths);
        }

        if (!fonts.IncludeSystemFonts)
        {
            arguments = arguments.Add("--ignore-system-fonts");
        }

        if (!fonts.IncludeEmbeddedFonts)
        {
            arguments = arguments.Add("--ignore-embedded-fonts");
        }

        return arguments;
    }

    private static TypstArguments AddPackages(TypstArguments arguments, TypstPackageOptions packages)
    {
        if (packages.PackagePath is { } packagePath)
        {
            arguments = arguments.AddSwitch("--package-path", Path.GetFullPath(packagePath));
        }

        if (packages.PackageCachePath is { } cachePath)
        {
            arguments = arguments.AddSwitch("--package-cache-path", Path.GetFullPath(cachePath));
        }

        return arguments;
    }

    private static TypstArguments AddOutputOptions(TypstArguments arguments, TypstOutput output) => output switch
    {
        TypstOutput.PdfOutput pdf => AddPages(AddPdfOptions(arguments, pdf.Options), pdf.Options.Pages),
        TypstOutput.PngOutput png => AddPages(arguments.AddSwitch("--ppi", png.Ppi.ToString(CultureInfo.InvariantCulture)), png.Pages),
        TypstOutput.SvgOutput svg => AddPages(arguments, svg.Pages),
        TypstOutput.HtmlOutput => arguments.AddSwitch("--features", "html"),
        _ => throw new ArgumentException($"Unsupported output '{output.GetType().Name}'.", nameof(output)),
    };

    private static TypstArguments AddPdfOptions(TypstArguments arguments, TypstPdfOptions pdf)
    {
        if (pdf.Standards.Length > 0)
        {
            arguments = arguments.AddSwitch("--pdf-standard", string.Join(",", pdf.Standards.Select(standard => standard.CliValue)));
        }

        return pdf.Tagged ? arguments : arguments.Add("--no-pdf-tags");
    }

    private static TypstArguments AddPages(TypstArguments arguments, TypstPageSelection pages) =>
        pages.IsAll ? arguments : arguments.AddSwitch("--pages", pages.ToCliValue());

    private static string OutputTarget(TypstOutput output, WorkspaceLayout layout)
    {
        if (!output.IsPaged)
        {
            return "-";
        }

        var directory = layout.PagedOutputDirectory
            ?? throw new InvalidOperationException("Paged output needs an output folder in the workspace.");
        return Path.Combine(directory, "{0p}." + output.FileExtension);
    }
}
```

`src/Typstium/CommandLine/CommandLineLimits.cs`:

```csharp
namespace Typstium.CommandLine;

/// <summary>
/// Windows limits a command line to 32,767 characters. The same limit applies on every platform so a
/// request that works on a Linux build agent also works on a Windows server.
/// </summary>
internal static class CommandLineLimits
{
    public const int MaxLength = 32000;

    public static void EnsureFits(string executable, TypstArguments arguments)
    {
        var length = WindowsArgumentEscaper.Escape(executable).Length + 1 + arguments.Render().Length;
        if (length > MaxLength)
        {
            throw new ArgumentException(
                $"The Typst command line would be {length} characters, over the {MaxLength} limit. Large data such as JSON inputs should be passed as a virtual file (a template or markup source with WithJson) instead of WithJsonInput.",
                "request");
        }
    }
}
```

- [ ] **Step 4: Implement the environment builder**

`src/Typstium/CommandLine/EnvironmentChanges.cs`:

```csharp
using System.Collections.Immutable;
using System.Diagnostics;

namespace Typstium.CommandLine;

/// <summary>Changes applied to the inherited environment of the Typst process: removals first, then sets.</summary>
internal sealed record EnvironmentChanges(ImmutableDictionary<string, string> Set, ImmutableArray<string> Remove)
{
    public static EnvironmentChanges None { get; } = new(ImmutableDictionary<string, string>.Empty, ImmutableArray<string>.Empty);

    public void ApplyTo(ProcessStartInfo startInfo)
    {
        foreach (var name in Remove)
        {
            startInfo.Environment.Remove(name);
        }

        foreach (var pair in Set)
        {
            startInfo.Environment[pair.Key] = pair.Value;
        }
    }
}
```

`src/Typstium/CommandLine/ProcessEnvironmentBuilder.cs`:

```csharp
using System.Collections;
using System.Collections.Immutable;

namespace Typstium.CommandLine;

internal static class ProcessEnvironmentBuilder
{
    /// <summary>A closed local port: any download attempt fails fast (verified in spike 0).</summary>
    internal const string DeadProxy = "http://127.0.0.1:9";

    private static readonly string[] ProxyVariables = ["HTTPS_PROXY", "HTTP_PROXY", "ALL_PROXY", "NO_PROXY"];
    private static readonly string[] BlockingProxyVariables = ["HTTPS_PROXY", "HTTP_PROXY", "ALL_PROXY"];

    public static IEnumerable<string> CurrentVariableNames() =>
        Environment.GetEnvironmentVariables().Keys.Cast<object>().Select(key => (string)key).ToArray();

    public static EnvironmentChanges Build(IEnumerable<string> inheritedNames, TypstCompileOptions options)
    {
        var names = inheritedNames.ToArray();
        var remove = ImmutableArray.CreateBuilder<string>();
        var set = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.OrdinalIgnoreCase);

        remove.AddRange(names.Where(IsTypstVariable));

        if (options.CreationTimestamp is TypstCreationTimestamp.NowTimestamp)
        {
            remove.AddRange(names.Where(name => string.Equals(name, "SOURCE_DATE_EPOCH", StringComparison.OrdinalIgnoreCase)));
        }

        if (options.Packages.NetworkAccess == TypstNetworkAccess.Blocked)
        {
            // Remove every spelling (Unix tools read lowercase too), then set the uppercase dead proxy.
            remove.AddRange(names.Where(name => ProxyVariables.Contains(name, StringComparer.OrdinalIgnoreCase)));
            foreach (var variable in BlockingProxyVariables)
            {
                set[variable] = DeadProxy;
            }
        }

        return new EnvironmentChanges(set.ToImmutable(), remove.ToImmutable());
    }

    /// <summary>For `typst info` and `typst --version`: only host Typst settings are removed.</summary>
    public static EnvironmentChanges BuildForProbe(IEnumerable<string> inheritedNames) =>
        new(ImmutableDictionary<string, string>.Empty, inheritedNames.Where(IsTypstVariable).ToImmutableArray());

    private static bool IsTypstVariable(string name) => name.StartsWith("TYPST_", StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test --project tests/Typstium.UnitTests --treenode-filter "/*/*/CompileArgumentsBuilderV015Tests/*"`
Then run `ProcessEnvironmentBuilderTests` and `CommandLineLimitsTests` the same way.
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src tests
git commit -m "Build Typst 0.15 compile arguments and the scrubbed process environment

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 15: Process runner and Windows job objects

Runs one Typst process with stdin, a bounded stdout, a capped stderr, a timeout, caller cancellation and
containment (spec 5.4). P/Invoke stays in `Typstium.Interop.Windows` behind `SafeHandle`-owning types
(spec D20).

**Files:**
- Modify: `src/Typstium/Interop/Windows/Kernel32.cs` (add job-object functions and structs)
- Create: `src/Typstium/Interop/Windows/SafeJobObjectHandle.cs`
- Create: `src/Typstium/Processes/ProcessSpec.cs`, `ProcessOutcome.cs`, `ITypstProcessRunner.cs`, `StreamReading.cs`, `IProcessContainment.cs`, `NoProcessContainment.cs`, `WindowsJobObject.cs`, `ProcessContainment.cs`, `TypstProcessRunner.cs` (all under `src/Typstium/Processes/`)
- Create: `tests/Typstium.UnitTests/Testing/ShellCommands.cs`
- Test: `tests/Typstium.UnitTests/Processes/TypstProcessRunnerTests.cs`, `tests/Typstium.UnitTests/Processes/WindowsJobObjectTests.cs`

**Interfaces:**
- Consumes: `TypstArguments`, `WindowsArgumentEscaper` (Task 3), `EnvironmentChanges` (Task 14), process exceptions (Task 4), `ByteMemory` (Task 7).
- Produces (internal, namespace `Typstium.Processes`):
  - `sealed record ProcessSpec(string Executable, TypstArguments Arguments, EnvironmentChanges Environment, string WorkingDirectory, ReadOnlyMemory<byte> Stdin, TimeSpan Timeout, long MaxStdoutBytes, long MaxStderrBytes, long? MemoryLimitBytes)` with `const long DefaultMaxStderrBytes = 1 MiB`
  - `abstract record ProcessOutcome { TimeSpan Duration; string StandardError }` with nested `Exited(int ExitCode, ReadOnlyMemory<byte> StandardOutput, …)`, `TimedOut(…)`, `OutputLimitExceeded(long LimitBytes, …)`
  - `interface ITypstProcessRunner { Task<ProcessOutcome> RunAsync(ProcessSpec spec, CancellationToken cancellationToken); }`
  - `sealed class TypstProcessRunner : ITypstProcessRunner`, with a public parameterless constructor and an internal `(Func<long?, IProcessContainment>)` constructor
  - `interface IProcessContainment : IDisposable { void Attach(Process process); void Terminate(); }`
  - `static class StreamReading`: `ReadBoundedAsync`, `ReadTextCappedAsync`, `const string TruncationMarker`

- [ ] **Step 1: Write the failing tests**

`tests/Typstium.UnitTests/Testing/ShellCommands.cs`:

```csharp
using System.Runtime.InteropServices;
using Typstium.CommandLine;
using Typstium.Processes;

namespace Typstium.UnitTests.Testing;

/// <summary>Cross-platform child processes for runner tests: cmd.exe on Windows, /bin/sh elsewhere.</summary>
internal static class ShellCommands
{
    public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public static string Shell => IsWindows ? Path.Combine(Environment.SystemDirectory, "cmd.exe") : "/bin/sh";

    public static TypstArguments Run(string windowsCommand, string unixCommand) =>
        IsWindows ? TypstArguments.Of("/d", "/c", windowsCommand) : TypstArguments.Of("-c", unixCommand);

    public static TypstArguments Sleep30() => Run("ping -n 30 127.0.0.1 >nul", "sleep 30");

    public static ProcessSpec Spec(TypstArguments arguments, ReadOnlyMemory<byte> stdin = default, TimeSpan? timeout = null, long maxStdoutBytes = 1 << 20) =>
        new(Shell, arguments, EnvironmentChanges.None, Path.GetTempPath(), stdin, timeout ?? TimeSpan.FromSeconds(30), maxStdoutBytes, ProcessSpec.DefaultMaxStderrBytes, null);
}
```

`tests/Typstium.UnitTests/Processes/TypstProcessRunnerTests.cs`:

```csharp
using System.Diagnostics;
using System.Text;
using Typstium.Processes;
using Typstium.UnitTests.Testing;

namespace Typstium.UnitTests.Processes;

public class TypstProcessRunnerTests
{
    private readonly TypstProcessRunner _runner = new();

    [Test]
    public async Task Exit_code_stdout_and_stderr_are_captured()
    {
        var outcome = await _runner.RunAsync(ShellCommands.Spec(ShellCommands.Run("echo hello& echo oops 1>&2& exit 3", "echo hello; echo oops 1>&2; exit 3")), CancellationToken.None);

        var exited = (ProcessOutcome.Exited)outcome;
        await Assert.That(exited.ExitCode).IsEqualTo(3);
        await Assert.That(Encoding.UTF8.GetString(exited.StandardOutput.ToArray()).Trim()).IsEqualTo("hello");
        await Assert.That(exited.StandardError).Contains("oops");
    }

    [Test]
    public async Task Stdin_reaches_the_process()
    {
        var spec = ShellCommands.Spec(ShellCommands.Run("more", "cat"), stdin: Encoding.UTF8.GetBytes("from stdin\n"));
        var outcome = (ProcessOutcome.Exited)await _runner.RunAsync(spec, CancellationToken.None);
        await Assert.That(Encoding.UTF8.GetString(outcome.StandardOutput.ToArray())).Contains("from stdin");
    }

    [Test]
    public async Task Timeout_kills_the_process_and_reports_timed_out()
    {
        var stopwatch = Stopwatch.StartNew();
        var outcome = await _runner.RunAsync(ShellCommands.Spec(ShellCommands.Sleep30(), timeout: TimeSpan.FromMilliseconds(500)), CancellationToken.None);
        await Assert.That(outcome is ProcessOutcome.TimedOut).IsTrue();
        await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task Output_limit_kills_the_process()
    {
        var endless = ShellCommands.Run("for /L %i in (1,0,2) do @echo yyyyyyyyyyyyyyyyyyyyyyyyyyyyyy", "yes");
        var outcome = await _runner.RunAsync(ShellCommands.Spec(endless, maxStdoutBytes: 64 * 1024), CancellationToken.None);
        var exceeded = outcome as ProcessOutcome.OutputLimitExceeded;
        await Assert.That(exceeded).IsNotNull();
        await Assert.That(exceeded!.LimitBytes).IsEqualTo(64L * 1024);
    }

    [Test]
    public async Task Caller_cancellation_throws_after_killing_the_process()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        var stopwatch = Stopwatch.StartNew();
        await Assert.ThrowsAsync<OperationCanceledException>(() => _runner.RunAsync(ShellCommands.Spec(ShellCommands.Sleep30()), cancellation.Token));
        await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task A_missing_executable_is_a_start_failure()
    {
        var spec = ShellCommands.Spec(TypstArguments.Of("compile")) with { Executable = Path.Combine(Path.GetTempPath(), "no-such-typst-" + Guid.NewGuid().ToString("N")) };
        var exception = await Assert.ThrowsAsync<TypstProcessStartException>(() => _runner.RunAsync(spec, CancellationToken.None));
        await Assert.That(exception.ExecutablePath).IsEqualTo(spec.Executable);
    }

    [Test]
    public async Task Stderr_is_capped_with_a_marker()
    {
        var text = await StreamReading.ReadTextCappedAsync(new MemoryStream(Encoding.UTF8.GetBytes(new string('e', 100))), maxBytes: 10);
        await Assert.That(text).StartsWith("eeeeeeeeee");
        await Assert.That(text).Contains(StreamReading.TruncationMarker);
    }
}
```

`tests/Typstium.UnitTests/Processes/WindowsJobObjectTests.cs`:

```csharp
using System.Diagnostics;
using Typstium.Processes;
using Typstium.UnitTests.Testing;

namespace Typstium.UnitTests.Processes;

public class WindowsJobObjectTests
{
    [Test]
    public async Task Disposing_the_job_kills_its_processes()
    {
        if (!ShellCommands.IsWindows)
        {
            Skip.Test("Job objects are Windows-only.");
        }

        using var process = Process.Start(new ProcessStartInfo(ShellCommands.Shell, "/d /c ping -n 30 127.0.0.1 >nul") { UseShellExecute = false, CreateNoWindow = true })!;
        var job = WindowsJobObject.Create(memoryLimitBytes: 256L * 1024 * 1024);
        job.Attach(process);

        job.Dispose();

        var exited = process.WaitForExit(5000);
        await Assert.That(exited).IsTrue();
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/Typstium.UnitTests --framework net10.0 --treenode-filter "/*/*/TypstProcessRunnerTests/*"`
Expected: build FAILS with `CS0246` for `TypstProcessRunner`.

- [ ] **Step 3: Add the job-object interop**

Replace `src/Typstium/Interop/Windows/Kernel32.cs` with the version from Task 12 plus the job-object
declarations:

```csharp
using System.Runtime.InteropServices;

namespace Typstium.Interop.Windows;

/// <summary>P/Invoke declarations for kernel32.dll. Windows only; callers check the platform.</summary>
internal static class Kernel32
{
    internal const uint JobObjectLimitProcessMemory = 0x00000100;
    internal const uint JobObjectLimitKillOnJobClose = 0x00002000;

    private const string LibraryName = "kernel32.dll";

    internal enum JobObjectInfoClass
    {
        ExtendedLimitInformation = 9,
    }

    [DllImport(LibraryName, SetLastError = true, ExactSpelling = true)]
    internal static extern SafeJobObjectHandle CreateJobObjectW(IntPtr jobAttributes, IntPtr name);

    [DllImport(LibraryName, SetLastError = true, ExactSpelling = true)]
    internal static extern int SetInformationJobObject(SafeJobObjectHandle job, JobObjectInfoClass infoClass, ref JobObjectExtendedLimitInformation info, int length);

    [DllImport(LibraryName, SetLastError = true, ExactSpelling = true)]
    internal static extern int AssignProcessToJobObject(SafeJobObjectHandle job, IntPtr process);

    [DllImport(LibraryName, SetLastError = true, ExactSpelling = true)]
    internal static extern int TerminateJobObject(SafeJobObjectHandle job, uint exitCode);

    [DllImport(LibraryName, SetLastError = true, ExactSpelling = true)]
    internal static extern int CloseHandle(IntPtr handle);

    [DllImport(LibraryName, CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    private static extern int GetLongPathNameW(string shortPath, [Out] char[]? longPath, int bufferLength);

    /// <summary>Expands 8.3 short names (e.g. RUNNER~1). Returns null if the path does not exist.</summary>
    internal static string? GetLongPath(string path)
    {
        var length = GetLongPathNameW(path, null, 0);
        if (length == 0)
        {
            return null;
        }

        var buffer = new char[length];
        var written = GetLongPathNameW(path, buffer, buffer.Length);
        return written == 0 || written >= buffer.Length ? null : new string(buffer, 0, written);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct JobObjectBasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct JobObjectExtendedLimitInformation
    {
        public JobObjectBasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }
}
```

`src/Typstium/Interop/Windows/SafeJobObjectHandle.cs`:

```csharp
using Microsoft.Win32.SafeHandles;

namespace Typstium.Interop.Windows;

/// <summary>Owns a job object handle. Closing the last handle kills the job's processes (kill-on-close).</summary>
internal sealed class SafeJobObjectHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafeJobObjectHandle()
        : base(ownsHandle: true)
    {
    }

    protected override bool ReleaseHandle() => Kernel32.CloseHandle(handle) != 0;
}
```

- [ ] **Step 4: Implement containment**

`src/Typstium/Processes/IProcessContainment.cs`:

```csharp
using System.Diagnostics;

namespace Typstium.Processes;

/// <summary>Keeps a process (and anything it starts) from outliving the compile.</summary>
internal interface IProcessContainment : IDisposable
{
    void Attach(Process process);

    /// <summary>Kills every contained process, including grandchildren that hold our pipes.</summary>
    void Terminate();
}
```

`src/Typstium/Processes/NoProcessContainment.cs`:

```csharp
using System.Diagnostics;

namespace Typstium.Processes;

/// <summary>Non-Windows: no job objects; the runner kills the process tree itself.</summary>
internal sealed class NoProcessContainment : IProcessContainment
{
    public static NoProcessContainment Instance { get; } = new();

    public void Attach(Process process)
    {
    }

    public void Terminate()
    {
    }

    public void Dispose()
    {
    }
}
```

`src/Typstium/Processes/WindowsJobObject.cs`:

```csharp
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Typstium.Interop.Windows;

namespace Typstium.Processes;

/// <summary>A Windows job object with kill-on-close and an optional per-process memory limit.</summary>
internal sealed class WindowsJobObject : IProcessContainment
{
    private readonly SafeJobObjectHandle _handle;

    private WindowsJobObject(SafeJobObjectHandle handle) => _handle = handle;

    public static WindowsJobObject Create(long? memoryLimitBytes)
    {
        var handle = Kernel32.CreateJobObjectW(IntPtr.Zero, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw new Win32Exception(error);
        }

        var info = new Kernel32.JobObjectExtendedLimitInformation();
        info.BasicLimitInformation.LimitFlags = Kernel32.JobObjectLimitKillOnJobClose;
        if (memoryLimitBytes is { } limit)
        {
            info.BasicLimitInformation.LimitFlags |= Kernel32.JobObjectLimitProcessMemory;
            info.ProcessMemoryLimit = new UIntPtr((ulong)limit);
        }

        var size = Marshal.SizeOf<Kernel32.JobObjectExtendedLimitInformation>();
        if (Kernel32.SetInformationJobObject(handle, Kernel32.JobObjectInfoClass.ExtendedLimitInformation, ref info, size) == 0)
        {
            var error = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw new Win32Exception(error);
        }

        return new WindowsJobObject(handle);
    }

    public void Attach(Process process)
    {
        if (Kernel32.AssignProcessToJobObject(_handle, process.Handle) == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    public void Terminate()
    {
        if (!_handle.IsClosed)
        {
            Kernel32.TerminateJobObject(_handle, exitCode: 1);
        }
    }

    public void Dispose() => _handle.Dispose();
}
```

`src/Typstium/Processes/ProcessContainment.cs`:

```csharp
using System.Runtime.InteropServices;

namespace Typstium.Processes;

internal static class ProcessContainment
{
    public static IProcessContainment Create(long? memoryLimitBytes) =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? WindowsJobObject.Create(memoryLimitBytes)
            : NoProcessContainment.Instance;
}
```

- [ ] **Step 5: Implement the runner**

`src/Typstium/Processes/ProcessSpec.cs`:

```csharp
using Typstium.CommandLine;

namespace Typstium.Processes;

internal sealed record ProcessSpec(
    string Executable,
    TypstArguments Arguments,
    EnvironmentChanges Environment,
    string WorkingDirectory,
    ReadOnlyMemory<byte> Stdin,
    TimeSpan Timeout,
    long MaxStdoutBytes,
    long MaxStderrBytes,
    long? MemoryLimitBytes)
{
    internal const long DefaultMaxStderrBytes = 1024 * 1024;
}
```

`src/Typstium/Processes/ProcessOutcome.cs`:

```csharp
namespace Typstium.Processes;

/// <summary>How one process run ended. Caller cancellation is not an outcome: it throws.</summary>
internal abstract record ProcessOutcome
{
    private ProcessOutcome(TimeSpan duration, string standardError)
    {
        Duration = duration;
        StandardError = standardError;
    }

    /// <summary>Wall-clock time from process start to exit or kill.</summary>
    public TimeSpan Duration { get; }

    /// <summary>UTF-8 decoded, capped stderr: the diagnostics.</summary>
    public string StandardError { get; }

    /// <summary>The process exited on its own.</summary>
    internal sealed record Exited : ProcessOutcome
    {
        public Exited(int exitCode, ReadOnlyMemory<byte> standardOutput, TimeSpan duration, string standardError)
            : base(duration, standardError)
        {
            ExitCode = exitCode;
            StandardOutput = standardOutput;
        }

        public int ExitCode { get; }

        /// <summary>The document for PDF and HTML output (written to "-").</summary>
        public ReadOnlyMemory<byte> StandardOutput { get; }
    }

    /// <summary>Killed by our timeout.</summary>
    internal sealed record TimedOut : ProcessOutcome
    {
        public TimedOut(TimeSpan duration, string standardError)
            : base(duration, standardError)
        {
        }
    }

    /// <summary>Killed because stdout passed the limit.</summary>
    internal sealed record OutputLimitExceeded : ProcessOutcome
    {
        public OutputLimitExceeded(long limitBytes, TimeSpan duration, string standardError)
            : base(duration, standardError)
        {
            LimitBytes = limitBytes;
        }

        public long LimitBytes { get; }
    }
}
```

`src/Typstium/Processes/ITypstProcessRunner.cs`:

```csharp
namespace Typstium.Processes;

/// <summary>The seam between the pipeline and real processes; faked in unit tests.</summary>
internal interface ITypstProcessRunner
{
    Task<ProcessOutcome> RunAsync(ProcessSpec spec, CancellationToken cancellationToken);
}
```

`src/Typstium/Processes/StreamReading.cs`:

```csharp
using System.Text;

namespace Typstium.Processes;

internal static class StreamReading
{
    internal const string TruncationMarker = "[stderr truncated by Typstium]";
    private const int ChunkSize = 81920;

    /// <summary>Reads to the end, or stops and calls <paramref name="onLimitExceeded"/> once the limit is passed.</summary>
    public static async Task<(ReadOnlyMemory<byte> Content, bool LimitExceeded)> ReadBoundedAsync(Stream stream, long maxBytes, Action onLimitExceeded)
    {
        var buffer = new MemoryStream();
        var chunk = new byte[ChunkSize];
        while (true)
        {
            var read = await stream.ReadAsync(chunk, 0, chunk.Length).ConfigureAwait(false);
            if (read == 0)
            {
                return (new ReadOnlyMemory<byte>(buffer.GetBuffer(), 0, (int)buffer.Length), false);
            }

            if (buffer.Length + read > maxBytes)
            {
                onLimitExceeded();
                return (ReadOnlyMemory<byte>.Empty, true);
            }

            buffer.Write(chunk, 0, read);
        }
    }

    /// <summary>Drains the stream completely (so the child never blocks) but keeps at most maxBytes.</summary>
    public static async Task<string> ReadTextCappedAsync(Stream stream, long maxBytes)
    {
        var buffer = new MemoryStream();
        var chunk = new byte[ChunkSize];
        var truncated = false;
        while (true)
        {
            var read = await stream.ReadAsync(chunk, 0, chunk.Length).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            var room = (int)Math.Max(0, Math.Min(read, maxBytes - buffer.Length));
            buffer.Write(chunk, 0, room);
            truncated |= room < read;
        }

        var text = Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
        return truncated ? text + "\n" + TruncationMarker : text;
    }
}
```

`src/Typstium/Processes/TypstProcessRunner.cs`:

```csharp
using System.ComponentModel;
using System.Diagnostics;
using Typstium.CommandLine;
using Typstium.Utilities;

namespace Typstium.Processes;

internal sealed class TypstProcessRunner : ITypstProcessRunner
{
    private readonly Func<long?, IProcessContainment> _createContainment;

    public TypstProcessRunner()
        : this(ProcessContainment.Create)
    {
    }

    internal TypstProcessRunner(Func<long?, IProcessContainment> createContainment) => _createContainment = createContainment;

    public async Task<ProcessOutcome> RunAsync(ProcessSpec spec, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(spec);
        cancellationToken.ThrowIfCancellationRequested();

        IProcessContainment containment;
        try
        {
            containment = _createContainment(spec.MemoryLimitBytes);
        }
        catch (Win32Exception exception)
        {
            throw new TypstProcessStartException(spec.Executable, exception);
        }

        using (containment)
        using (var process = new Process { StartInfo = CreateStartInfo(spec) })
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                process.Start();
            }
            catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or FileNotFoundException)
            {
                throw new TypstProcessStartException(spec.Executable, exception);
            }

            try
            {
                containment.Attach(process);
            }
            catch (Win32Exception exception)
            {
                Kill(process, containment);
                throw new TypstProcessStartException(spec.Executable, exception);
            }

            using var timeout = new CancellationTokenSource();
            if (spec.Timeout != Timeout.InfiniteTimeSpan)
            {
                timeout.CancelAfter(spec.Timeout);
            }

            using var outputLimit = new CancellationTokenSource();
            using var kill = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token, outputLimit.Token);
            using var registration = kill.Token.Register(() => Kill(process, containment));

            var stdout = StreamReading.ReadBoundedAsync(process.StandardOutput.BaseStream, spec.MaxStdoutBytes, outputLimit.Cancel);
            var stderr = StreamReading.ReadTextCappedAsync(process.StandardError.BaseStream, spec.MaxStderrBytes);
            var stdin = WriteStdinAsync(process, spec.Stdin);

            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            var (output, limitExceeded) = await stdout.ConfigureAwait(false);
            var errorText = await stderr.ConfigureAwait(false);
            await IgnorePipeErrorsAsync(stdin).ConfigureAwait(false);
            stopwatch.Stop();

            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException("The Typst compile was cancelled.", cancellationToken);
            }

            if (limitExceeded)
            {
                return new ProcessOutcome.OutputLimitExceeded(spec.MaxStdoutBytes, stopwatch.Elapsed, errorText);
            }

            if (timeout.IsCancellationRequested)
            {
                return new ProcessOutcome.TimedOut(stopwatch.Elapsed, errorText);
            }

            return new ProcessOutcome.Exited(process.ExitCode, output, stopwatch.Elapsed, errorText);
        }
    }

    private static ProcessStartInfo CreateStartInfo(ProcessSpec spec)
    {
        var startInfo = new ProcessStartInfo(spec.Executable)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = spec.WorkingDirectory,
        };

#if NET8_0_OR_GREATER
        foreach (var argument in spec.Arguments)
        {
            startInfo.ArgumentList.Add(argument.Value);
        }
#else
        startInfo.Arguments = WindowsArgumentEscaper.Join(spec.Arguments.ToValues());
#endif

        spec.Environment.ApplyTo(startInfo);
        return startInfo;
    }

    private static async Task WriteStdinAsync(Process process, ReadOnlyMemory<byte> stdin)
    {
        var stream = process.StandardInput.BaseStream;
        try
        {
            if (!stdin.IsEmpty)
            {
                var segment = ByteMemory.AsSegment(stdin);
                await stream.WriteAsync(segment.Array!, segment.Offset, segment.Count).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            process.StandardInput.Close();
        }
    }

    private static async Task IgnorePipeErrorsAsync(Task stdin)
    {
        try
        {
            await stdin.ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
            // The process exited before reading all of stdin (e.g. it was killed).
        }
    }

    /// <summary>
    /// Kills the process and anything it started. On netstandard2.0 Process.Kill(true) is not available,
    /// so the job object terminates grandchildren that may still hold our pipes open.
    /// </summary>
    private static void Kill(Process process, IProcessContainment containment)
    {
        try
        {
#if NET8_0_OR_GREATER
            process.Kill(entireProcessTree: true);
#else
            process.Kill();
#endif
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // Already exited.
        }

        containment.Terminate();
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test --project tests/Typstium.UnitTests --treenode-filter "/*/*/TypstProcessRunnerTests/*"`
Then: `dotnet test --project tests/Typstium.UnitTests --treenode-filter "/*/*/WindowsJobObjectTests/*"`
Expected: PASS on `net10.0` and, on Windows, `net472`. The `net472` timeout test passes only because
`Kill` terminates the job (cmd.exe's `ping` child holds the stderr pipe).

- [ ] **Step 7: Commit**

```bash
git add src tests
git commit -m "Add process runner with limits, cancellation and Windows job-object containment

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 16: CLI profiles and the profile factory

The only code that compares Typst versions (spec 5.7). It runs `typst info` once per executable, builds
`TypstCapabilities` and picks the version-specific components.

**Files:**
- Create: `src/Typstium/Executable/TypstExecutable.cs`
- Create: `src/Typstium/Capabilities/TypstCliProfile.cs`, `src/Typstium/Capabilities/TypstCliProfileFactory.cs`
- Create: `tests/Typstium.UnitTests/Testing/FakeProcessRunner.cs`
- Test: `tests/Typstium.UnitTests/Capabilities/TypstCliProfileFactoryTests.cs`

**Interfaces:**
- Consumes: `TypstInfoParser`, `TypstInfo`, `TypstCapabilities` (Task 13), `ITypstProcessRunner`, `ProcessSpec`, `ProcessOutcome` (Task 15), `ProcessEnvironmentBuilder` (Task 14), `CompileArgumentsBuilderV015` (Task 14), `HumanDiagnosticParserV015` (Task 10), `DiagnosticClassifierV015` (Task 11).
- Produces:
  - `public sealed record TypstExecutable(string Path, string FoundBy)`
  - `internal sealed record TypstCliProfile(TypstCapabilities Capabilities, ICompileArgumentsBuilder ArgumentsBuilder, IDiagnosticParser DiagnosticParser, IDiagnosticClassifier DiagnosticClassifier)`
  - `internal sealed class TypstCliProfileFactory(ITypstProcessRunner runner)`: `static TypstVersion MinimumVersion` (0.15.0), `Task<TypstCliProfile> GetProfileAsync(TypstExecutable, CancellationToken)`, `int ProbeCount`, `internal static TypstCliProfile CreateProfile(TypstInfo info)`
  - Unit test fake `Typstium.UnitTests.Testing.FakeProcessRunner : ITypstProcessRunner`:
    - constructor `(Func<ProcessSpec, ProcessOutcome> respond)`, `TimeSpan Delay { init; }`, `IReadOnlyList<ProcessSpec> Specs`
    - static `Exited(int, string stdout = "", string stderr = "")`, `IsInfo(ProcessSpec)`, `InfoJson(string version, bool html = true)`, `int MaxObservedConcurrency`

- [ ] **Step 1: Write the failing tests**

`tests/Typstium.UnitTests/Testing/FakeProcessRunner.cs`:

```csharp
using System.Collections.Concurrent;
using System.Text;
using Typstium.Processes;

namespace Typstium.UnitTests.Testing;

/// <summary>Answers process runs from a callback and records every spec.</summary>
internal sealed class FakeProcessRunner : ITypstProcessRunner
{
    private readonly Func<ProcessSpec, ProcessOutcome> _respond;
    private readonly ConcurrentQueue<ProcessSpec> _specs = new();
    private int _concurrency;
    private int _maxConcurrency;

    public FakeProcessRunner(Func<ProcessSpec, ProcessOutcome> respond) => _respond = respond;

    public TimeSpan Delay { get; init; }

    public IReadOnlyList<ProcessSpec> Specs => _specs.ToArray();

    public static ProcessOutcome Exited(int exitCode, string stdout = "", string stderr = "") =>
        new ProcessOutcome.Exited(exitCode, Encoding.UTF8.GetBytes(stdout), TimeSpan.FromMilliseconds(5), stderr);

    public static bool IsInfo(ProcessSpec spec) => spec.Arguments.IndexOf("info") >= 0;

    public static string InfoJson(string version, bool html = true) =>
        "{\"version\":\"" + version + "\",\"features\":{" + (html ? "\"html\":false" : string.Empty) + "}}";

    public int MaxObservedConcurrency => Volatile.Read(ref _maxConcurrency);

    public async Task<ProcessOutcome> RunAsync(ProcessSpec spec, CancellationToken cancellationToken)
    {
        _specs.Enqueue(spec);
        var current = Interlocked.Increment(ref _concurrency);
        int observed;
        while (current > (observed = Volatile.Read(ref _maxConcurrency)) && Interlocked.CompareExchange(ref _maxConcurrency, current, observed) != observed)
        {
        }

        try
        {
            if (Delay > TimeSpan.Zero)
            {
                await Task.Delay(Delay, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return _respond(spec);
        }
        finally
        {
            Interlocked.Decrement(ref _concurrency);
        }
    }
}
```

`tests/Typstium.UnitTests/Capabilities/TypstCliProfileFactoryTests.cs`:

```csharp
using System.Collections.Immutable;
using Typstium.Capabilities;
using Typstium.CommandLine;
using Typstium.Diagnostics;
using Typstium.Testing.Fixtures;
using Typstium.UnitTests.Testing;

namespace Typstium.UnitTests.Capabilities;

public class TypstCliProfileFactoryTests
{
    private static TypstExecutable Executable(TemporaryDirectory temp) => new(temp.CreateFile("typst.exe", "fake"), "Test");

    [Test]
    public async Task Probes_once_even_under_parallel_cold_start()
    {
        using var temp = new TemporaryDirectory("profile");
        var runner = new FakeProcessRunner(_ => FakeProcessRunner.Exited(0, FakeProcessRunner.InfoJson("0.15.1"))) { Delay = TimeSpan.FromMilliseconds(50) };
        var factory = new TypstCliProfileFactory(runner);
        var executable = Executable(temp);

        var profiles = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => factory.GetProfileAsync(executable, CancellationToken.None))));

        await Assert.That(factory.ProbeCount).IsEqualTo(1);
        await Assert.That(profiles.Distinct().Count()).IsEqualTo(1);
        await Assert.That(profiles[0].Capabilities.Version).IsEqualTo(new TypstVersion(0, 15, 1));
        await Assert.That(runner.Specs[0].Arguments.ToValues()).IsEquivalentTo(new[] { "--color", "never", "info", "--format", "json" });
    }

    [Test]
    public async Task Typst_without_info_command_is_reported_as_too_old()
    {
        using var temp = new TemporaryDirectory("profile");
        var runner = new FakeProcessRunner(spec => FakeProcessRunner.IsInfo(spec)
            ? FakeProcessRunner.Exited(2, stderr: "error: unrecognized subcommand 'info'")
            : FakeProcessRunner.Exited(0, "typst 0.14.2 (abcdef12)"));
        var factory = new TypstCliProfileFactory(runner);

        var exception = await Assert.ThrowsAsync<TypstFeatureNotSupportedException>(() => factory.GetProfileAsync(Executable(temp), CancellationToken.None));
        await Assert.That(exception.InstalledVersion).IsEqualTo(new TypstVersion(0, 14, 2));
        await Assert.That(exception.RequiredVersion).IsEqualTo(TypstCliProfileFactory.MinimumVersion);
    }

    [Test]
    public async Task Failures_are_not_cached()
    {
        using var temp = new TemporaryDirectory("profile");
        var calls = 0;
        var runner = new FakeProcessRunner(_ => Interlocked.Increment(ref calls) == 1
            ? FakeProcessRunner.Exited(101, stderr: "panicked")
            : FakeProcessRunner.Exited(0, FakeProcessRunner.InfoJson("0.15.1")));
        var factory = new TypstCliProfileFactory(runner);
        var executable = Executable(temp);

        await Assert.ThrowsAsync<TypstProcessCrashedException>(() => factory.GetProfileAsync(executable, CancellationToken.None));
        var profile = await factory.GetProfileAsync(executable, CancellationToken.None);
        await Assert.That(profile.Capabilities.Version).IsEqualTo(new TypstVersion(0, 15, 1));
    }

    [Test]
    public async Task Version_table_uses_the_newest_profile_for_unknown_newer_versions()
    {
        var info = new TypstInfo(new TypstVersion(0, 17, 3), ImmutableHashSet.Create("html"));
        var profile = TypstCliProfileFactory.CreateProfile(info);

        await Assert.That(profile.ArgumentsBuilder is CompileArgumentsBuilderV015).IsTrue();
        await Assert.That(profile.DiagnosticParser is HumanDiagnosticParserV015).IsTrue();
        await Assert.That(profile.Capabilities.SupportsHtml).IsTrue();
    }

    [Test]
    public async Task Versions_below_the_minimum_are_rejected()
    {
        var info = new TypstInfo(new TypstVersion(0, 14, 9), ImmutableHashSet<string>.Empty);
        var exception = Assert.Throws<TypstFeatureNotSupportedException>(() => TypstCliProfileFactory.CreateProfile(info));
        await Assert.That(exception).IsNotNull();
    }

    [Test]
    public async Task Html_support_follows_the_feature_list()
    {
        var withoutHtml = TypstCliProfileFactory.CreateProfile(new TypstInfo(new TypstVersion(0, 15, 1), ImmutableHashSet<string>.Empty));
        await Assert.That(withoutHtml.Capabilities.SupportsHtml).IsFalse();
        await Assert.That(withoutHtml.Capabilities.PdfStandards.Contains(PdfStandard.A2b)).IsTrue();
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/Typstium.UnitTests --framework net10.0 --treenode-filter "/*/*/TypstCliProfileFactoryTests/*"`
Expected: build FAILS with `CS0246` for `TypstCliProfileFactory`.

- [ ] **Step 3: Implement**

`src/Typstium/Executable/TypstExecutable.cs`:

```csharp
namespace Typstium;

/// <param name="Path">Fully qualified path; never resolved through the current directory.</param>
/// <param name="FoundBy">The locator that found it, e.g. "WinGet" or "PATH".</param>
public sealed record TypstExecutable(string Path, string FoundBy);
```

`src/Typstium/Capabilities/TypstCliProfile.cs`:

```csharp
using Typstium.CommandLine;
using Typstium.Diagnostics;

namespace Typstium.Capabilities;

/// <summary>The version-specific components for one Typst executable, chosen once (spec 5.7).</summary>
internal sealed record TypstCliProfile(
    TypstCapabilities Capabilities,
    ICompileArgumentsBuilder ArgumentsBuilder,
    IDiagnosticParser DiagnosticParser,
    IDiagnosticClassifier DiagnosticClassifier);
```

`src/Typstium/Capabilities/TypstCliProfileFactory.cs`:

```csharp
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text;
using Typstium.CommandLine;
using Typstium.Diagnostics;
using Typstium.Processes;

namespace Typstium.Capabilities;

/// <summary>
/// The only code that compares Typst versions. Probes each executable once with `typst info` and picks
/// the profile for its version range; newer unknown versions get the newest profile.
/// </summary>
internal sealed class TypstCliProfileFactory
{
    internal static readonly TypstVersion MinimumVersion = new(0, 15, 0);

    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(30);

    // One row per supported range, oldest first. Add a row when a Typst release needs different components.
    private static readonly (TypstVersion From, Func<TypstInfo, TypstCliProfile> Create)[] Profiles =
    [
        (new TypstVersion(0, 15, 0), CreateV015),
    ];

    private readonly ITypstProcessRunner _runner;
    private readonly ConcurrentDictionary<ProfileKey, Lazy<Task<TypstCliProfile>>> _cache = new();
    private int _probeCount;

    public TypstCliProfileFactory(ITypstProcessRunner runner) => _runner = runner ?? throw new ArgumentNullException(nameof(runner));

    public int ProbeCount => Volatile.Read(ref _probeCount);

    public async Task<TypstCliProfile> GetProfileAsync(TypstExecutable executable, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(executable);
        var key = ProfileKey.For(executable.Path);
        var entry = _cache.GetOrAdd(key, _ => new Lazy<Task<TypstCliProfile>>(() => ProbeAsync(executable)));
        try
        {
            // The probe is shared, so one caller's cancellation must not cancel it for others.
            return await entry.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ((ICollection<KeyValuePair<ProfileKey, Lazy<Task<TypstCliProfile>>>>)_cache).Remove(new KeyValuePair<ProfileKey, Lazy<Task<TypstCliProfile>>>(key, entry));
            throw;
        }
    }

    internal static TypstCliProfile CreateProfile(TypstInfo info)
    {
        if (info.Version < MinimumVersion)
        {
            throw new TypstFeatureNotSupportedException("Typstium", info.Version, MinimumVersion);
        }

        var row = Profiles.Last(profile => info.Version >= profile.From);
        return row.Create(info);
    }

    private static TypstCliProfile CreateV015(TypstInfo info) => new(
        new TypstCapabilities(info.Version, info.Features.Contains("html"), PdfStandard.KnownInTypst015.ToImmutableHashSet()),
        new CompileArgumentsBuilderV015(),
        new HumanDiagnosticParserV015(),
        new DiagnosticClassifierV015());

    private async Task<TypstCliProfile> ProbeAsync(TypstExecutable executable)
    {
        Interlocked.Increment(ref _probeCount);
        var outcome = await RunAsync(executable, TypstArguments.Of("--color", "never", "info", "--format", "json")).ConfigureAwait(false);
        switch (outcome)
        {
            case ProcessOutcome.Exited { ExitCode: 0 } info:
                return CreateProfile(TypstInfoParser.Parse(Utf8(info.StandardOutput)));

            case ProcessOutcome.Exited { ExitCode: 2 }:
                // Typst older than 0.15 has no `info` subcommand.
                var versionOutcome = await RunAsync(executable, TypstArguments.Of("--version")).ConfigureAwait(false);
                if (versionOutcome is ProcessOutcome.Exited { ExitCode: 0 } version)
                {
                    throw new TypstFeatureNotSupportedException("Typstium", TypstInfoParser.ParseVersionOutput(Utf8(version.StandardOutput)), MinimumVersion);
                }

                throw new TypstProcessOutputException("'typst info' is not supported and 'typst --version' failed.");

            case ProcessOutcome.Exited crashed:
                throw new TypstProcessCrashedException(crashed.ExitCode, crashed.StandardError);

            default:
                throw new TypstProcessOutputException($"'typst info' did not finish within {ProbeTimeout.TotalSeconds:0} seconds.");
        }
    }

    private Task<ProcessOutcome> RunAsync(TypstExecutable executable, TypstArguments arguments) =>
        _runner.RunAsync(
            new ProcessSpec(
                executable.Path,
                arguments,
                ProcessEnvironmentBuilder.BuildForProbe(ProcessEnvironmentBuilder.CurrentVariableNames()),
                Path.GetTempPath(),
                ReadOnlyMemory<byte>.Empty,
                ProbeTimeout,
                MaxStdoutBytes: 1024 * 1024,
                ProcessSpec.DefaultMaxStderrBytes,
                MemoryLimitBytes: null),
            CancellationToken.None);

    private static string Utf8(ReadOnlyMemory<byte> bytes) => Encoding.UTF8.GetString(bytes.ToArray());

    /// <summary>Identifies an executable file version: a replaced binary gets a new probe.</summary>
    private readonly record struct ProfileKey(string Path, long Length, DateTime LastWriteUtc)
    {
        public static ProfileKey For(string path)
        {
            var file = new FileInfo(path);
            return file.Exists ? new ProfileKey(file.FullName, file.Length, file.LastWriteTimeUtc) : new ProfileKey(path, -1, DateTime.MinValue);
        }
    }
}
```

`Task<TResult>.WaitAsync(CancellationToken)` comes from Polyfill on `netstandard2.0` (verified in Polyfill 11.4.3, `Polyfill_Task.cs`).

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --project tests/Typstium.UnitTests --treenode-filter "/*/*/TypstCliProfileFactoryTests/*"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src tests
git commit -m "Add CLI profiles chosen once per executable by the profile factory

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 17: Locating the Typst executable

An ordered chain of locators behind one resolver (spec 4.10). It always returns a fully qualified path,
and on failure the exception lists every place searched.

**Files:**
- Create: `src/Typstium/Executable/ITypstExecutableResolver.cs`, `ITypstExecutableLocator.cs`, `TypstLocatorResult.cs`, `TypstLocatorSearch.cs`, `ILocatorEnvironment.cs`, `SystemLocatorEnvironment.cs`, `Locators.cs`, `TypstExecutableLocators.cs`, `TypstExecutableResolver.cs` (all under `src/Typstium/Executable/`)
- Create: `src/Typstium/Exceptions/TypstExecutableNotFoundException.cs`
- Create: `tests/Typstium.UnitTests/Testing/FakeLocatorEnvironment.cs`
- Test: `tests/Typstium.UnitTests/Executable/LocatorTests.cs`, `tests/Typstium.UnitTests/Executable/TypstExecutableResolverTests.cs`

**Interfaces:**
- Consumes: `TypstExecutable` (Task 16), `TypstException` (Task 4).
- Produces (public):
  - `interface ITypstExecutableResolver { ValueTask<TypstExecutable> ResolveAsync(CancellationToken cancellationToken); }`
  - `interface ITypstExecutableLocator { string Name { get; } ValueTask<TypstLocatorResult> LocateAsync(CancellationToken cancellationToken); }`
  - `abstract record TypstLocatorResult`: `static Found(string path)`, `static NotFound(IEnumerable<string> searchedLocations)`, nested `FoundResult(string Path)` and `NotFoundResult(ImmutableArray<string> SearchedLocations)`
  - `sealed record TypstLocatorSearch(string LocatorName, ImmutableArray<string> SearchedLocations)`
  - `sealed class TypstExecutableNotFoundException : TypstException { ImmutableArray<TypstLocatorSearch> Searches }`
  - `sealed class TypstExecutableResolver : ITypstExecutableResolver`: constructor `(IEnumerable<ITypstExecutableLocator>)`, `static CreateDefault()`, `static CreateDefault(string explicitPath)`
  - `static class TypstExecutableLocators`: `ExplicitPath(string)`, `EnvironmentVariable()`, `EnvironmentVariable(string)`, `BundledRuntime()`, `WinGet()`, `WellKnownLocations()`, `SearchPath()`
- Produces (internal, namespace `Typstium.Executable`):
  - `interface ILocatorEnvironment`: `IsWindows`, `GetVariable`, `FileExists`, `EnumerateDirectories(string path, string searchPattern)`, `BaseDirectory`, `RuntimeIdentifier`, `HomeDirectory`, `EnsureExecutable`
  - `SystemLocatorEnvironment.Instance`
  - locator classes `ExplicitPathLocator`, `EnvironmentVariableLocator`, `BundledRuntimeLocator`, `WinGetLocator`, `WellKnownLocationsLocator`, `SearchPathLocator`, each with a `(…, ILocatorEnvironment)` constructor
- Produces (unit test infrastructure): `Typstium.UnitTests.Testing.FakeLocatorEnvironment`

- [ ] **Step 1: Write the failing tests**

`tests/Typstium.UnitTests/Testing/FakeLocatorEnvironment.cs`:

```csharp
using System.Text.RegularExpressions;
using Typstium.Executable;

namespace Typstium.UnitTests.Testing;

/// <summary>An in-memory file system and environment for locator tests. Paths use the host separator.</summary>
internal sealed class FakeLocatorEnvironment : ILocatorEnvironment
{
    public static string Root { get; } = Path.Combine(Path.GetTempPath(), "fake-fs");

    public bool IsWindows { get; init; } = true;

    public Dictionary<string, string> Variables { get; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string BaseDirectory { get; init; } = Path.Combine(Root, "app");

    public string RuntimeIdentifier { get; init; } = "win-x64";

    public string? HomeDirectory { get; init; } = Path.Combine(Root, "home");

    public List<string> MadeExecutable { get; } = new();

    public string AddFile(params string[] segments)
    {
        var path = Path.Combine(segments);
        Files.Add(path);
        return path;
    }

    public string? GetVariable(string name) => Variables.TryGetValue(name, out var value) ? value : null;

    public bool FileExists(string path) => Files.Contains(path);

    public IEnumerable<string> EnumerateDirectories(string path, string searchPattern)
    {
        var prefix = path.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var pattern = new Regex("^" + Regex.Escape(searchPattern).Replace("\\*", ".*") + "$", RegexOptions.IgnoreCase);
        return Files
            .Where(file => file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(file => file.Substring(prefix.Length).Split(Path.DirectorySeparatorChar))
            .Where(parts => parts.Length > 1 && pattern.IsMatch(parts[0]))
            .Select(parts => prefix + parts[0])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public void EnsureExecutable(string path) => MadeExecutable.Add(path);
}
```

`tests/Typstium.UnitTests/Executable/LocatorTests.cs`:

```csharp
using Typstium.Executable;
using Typstium.UnitTests.Testing;

namespace Typstium.UnitTests.Executable;

public class LocatorTests
{
    private static string Root => FakeLocatorEnvironment.Root;

    [Test]
    public async Task Explicit_path_is_found_when_it_exists()
    {
        var environment = new FakeLocatorEnvironment();
        var typst = environment.AddFile(Root, "tools", "typst.exe");

        var found = await new ExplicitPathLocator(typst, environment).LocateAsync(CancellationToken.None);
        var missing = await new ExplicitPathLocator(Path.Combine(Root, "nope.exe"), environment).LocateAsync(CancellationToken.None);

        await Assert.That(found).IsEqualTo(TypstLocatorResult.Found(typst));
        await Assert.That(((TypstLocatorResult.NotFoundResult)missing).SearchedLocations.Single()).IsEqualTo(Path.Combine(Root, "nope.exe"));
    }

    [Test]
    public async Task Environment_variable_reports_when_it_is_not_set()
    {
        var environment = new FakeLocatorEnvironment();
        var result = (TypstLocatorResult.NotFoundResult)await new EnvironmentVariableLocator("TYPST_PATH", environment).LocateAsync(CancellationToken.None);
        await Assert.That(result.SearchedLocations.Single()).Contains("not set");

        var typst = environment.AddFile(Root, "env", "typst.exe");
        environment.Variables["TYPST_PATH"] = typst;
        var found = await new EnvironmentVariableLocator("TYPST_PATH", environment).LocateAsync(CancellationToken.None);
        await Assert.That(found).IsEqualTo(TypstLocatorResult.Found(typst));
    }

    [Test]
    public async Task Bundled_runtime_supports_the_classic_asp_net_bin_folder_and_sets_the_execute_bit()
    {
        var environment = new FakeLocatorEnvironment { IsWindows = false, RuntimeIdentifier = "linux-x64" };
        var typst = environment.AddFile(environment.BaseDirectory, "bin", "typst", "linux-x64", "typst");

        var found = await new BundledRuntimeLocator(environment).LocateAsync(CancellationToken.None);

        await Assert.That(found).IsEqualTo(TypstLocatorResult.Found(typst));
        await Assert.That(environment.MadeExecutable).Contains(typst);
    }

    [Test]
    public async Task WinGet_finds_links_and_package_folders()
    {
        var local = Path.Combine(Root, "LocalAppData");
        var environment = new FakeLocatorEnvironment();
        environment.Variables["LOCALAPPDATA"] = local;
        var packaged = environment.AddFile(local, "Microsoft", "WinGet", "Packages", "Typst.Typst_Microsoft.Winget.Source_8wekyb3d8bbwe", "typst-x86_64-pc-windows-msvc", "typst.exe");

        var found = await new WinGetLocator(environment).LocateAsync(CancellationToken.None);
        await Assert.That(found).IsEqualTo(TypstLocatorResult.Found(packaged));

        var link = environment.AddFile(local, "Microsoft", "WinGet", "Links", "typst.exe");
        var preferred = await new WinGetLocator(environment).LocateAsync(CancellationToken.None);
        await Assert.That(preferred).IsEqualTo(TypstLocatorResult.Found(link));
    }

    [Test]
    public async Task WinGet_is_windows_only()
    {
        var result = await new WinGetLocator(new FakeLocatorEnvironment { IsWindows = false }).LocateAsync(CancellationToken.None);
        await Assert.That(result is TypstLocatorResult.NotFoundResult).IsTrue();
    }

    [Test]
    public async Task Search_path_never_uses_relative_entries()
    {
        var environment = new FakeLocatorEnvironment();
        var tools = Path.Combine(Root, "tools");
        environment.Variables["PATH"] = string.Join(Path.PathSeparator.ToString(), ".", "relative" + Path.DirectorySeparatorChar + "bin", "", tools);
        environment.AddFile(".", "typst.exe");
        var typst = environment.AddFile(tools, "typst.exe");

        var found = await new SearchPathLocator(environment).LocateAsync(CancellationToken.None);
        await Assert.That(found).IsEqualTo(TypstLocatorResult.Found(typst));
    }
}
```

`tests/Typstium.UnitTests/Executable/TypstExecutableResolverTests.cs`:

```csharp
using Typstium.Executable;
using Typstium.UnitTests.Testing;

namespace Typstium.UnitTests.Executable;

public class TypstExecutableResolverTests
{
    private sealed class CountingLocator(string name, TypstLocatorResult result) : ITypstExecutableLocator
    {
        public int Calls;

        public string Name => name;

        public ValueTask<TypstLocatorResult> LocateAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            return new ValueTask<TypstLocatorResult>(result);
        }
    }

    [Test]
    public async Task First_hit_wins_and_is_cached()
    {
        var first = new CountingLocator("TYPST_PATH", TypstLocatorResult.Found("/opt/a/typst"));
        var second = new CountingLocator("PATH", TypstLocatorResult.Found("/usr/bin/typst"));
        var resolver = new TypstExecutableResolver(new ITypstExecutableLocator[] { first, second });

        var resolved = await resolver.ResolveAsync(CancellationToken.None);
        await resolver.ResolveAsync(CancellationToken.None);

        await Assert.That(resolved).IsEqualTo(new TypstExecutable("/opt/a/typst", "TYPST_PATH"));
        await Assert.That(first.Calls).IsEqualTo(1);
        await Assert.That(second.Calls).IsEqualTo(0);
    }

    [Test]
    public async Task Not_found_lists_every_location_and_how_to_fix_it()
    {
        var resolver = new TypstExecutableResolver(new ITypstExecutableLocator[]
        {
            new CountingLocator("TYPST_PATH", TypstLocatorResult.NotFound(new[] { "%TYPST_PATH% (not set)" })),
            new CountingLocator("PATH", TypstLocatorResult.NotFound(new[] { "/usr/bin/typst", "/bin/typst" })),
        });

        var exception = await Assert.ThrowsAsync<TypstExecutableNotFoundException>(() => resolver.ResolveAsync(CancellationToken.None).AsTask());

        await Assert.That(exception.Searches.Length).IsEqualTo(2);
        await Assert.That(exception.Message).Contains("/bin/typst");
        await Assert.That(exception.Message).Contains("winget install Typst.Typst");
    }

    [Test]
    public async Task Failures_are_not_cached()
    {
        var environment = new FakeLocatorEnvironment();
        var resolver = new TypstExecutableResolver(new ITypstExecutableLocator[] { new EnvironmentVariableLocator("TYPST_PATH", environment) });

        await Assert.ThrowsAsync<TypstExecutableNotFoundException>(() => resolver.ResolveAsync(CancellationToken.None).AsTask());
        var typst = environment.AddFile(FakeLocatorEnvironment.Root, "late", "typst.exe");
        environment.Variables["TYPST_PATH"] = typst;

        var resolved = await resolver.ResolveAsync(CancellationToken.None);
        await Assert.That(resolved.Path).IsEqualTo(typst);
    }

    [Test]
    public async Task Default_order_is_explicit_env_bundled_winget_wellknown_path()
    {
        var names = TypstExecutableResolver.DefaultLocators("C:\\typst.exe").Select(locator => locator.Name);
        await Assert.That(names).IsEquivalentTo(new[] { "ExplicitPath", "TYPST_PATH", "BundledRuntime", "WinGet", "WellKnownLocations", "PATH" });
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/Typstium.UnitTests --framework net10.0 --treenode-filter "/*/*/LocatorTests/*"`
Expected: build FAILS with `CS0246` for `ExplicitPathLocator`.

- [ ] **Step 3: Implement the public contracts**

`src/Typstium/Executable/ITypstExecutableResolver.cs`:

```csharp
namespace Typstium;

public interface ITypstExecutableResolver
{
    /// <exception cref="TypstExecutableNotFoundException">No locator found Typst.</exception>
    ValueTask<TypstExecutable> ResolveAsync(CancellationToken cancellationToken);
}
```

`src/Typstium/Executable/ITypstExecutableLocator.cs`:

```csharp
namespace Typstium;

/// <summary>One place to look for Typst, e.g. TYPST_PATH or WinGet. Composed by TypstExecutableResolver.</summary>
public interface ITypstExecutableLocator
{
    string Name { get; }

    ValueTask<TypstLocatorResult> LocateAsync(CancellationToken cancellationToken);
}
```

`src/Typstium/Executable/TypstLocatorResult.cs`:

```csharp
using System.Collections.Immutable;

namespace Typstium;

public abstract record TypstLocatorResult
{
    private TypstLocatorResult()
    {
    }

    public static TypstLocatorResult Found(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new FoundResult(path);
    }

    public static TypstLocatorResult NotFound(IEnumerable<string> searchedLocations)
    {
        ArgumentNullException.ThrowIfNull(searchedLocations);
        return new NotFoundResult(searchedLocations.ToImmutableArray());
    }

    public sealed record FoundResult(string Path) : TypstLocatorResult;

    public sealed record NotFoundResult(ImmutableArray<string> SearchedLocations) : TypstLocatorResult;
}
```

`src/Typstium/Executable/TypstLocatorSearch.cs`:

```csharp
using System.Collections.Immutable;

namespace Typstium;

public sealed record TypstLocatorSearch(string LocatorName, ImmutableArray<string> SearchedLocations);
```

`src/Typstium/Exceptions/TypstExecutableNotFoundException.cs`:

```csharp
using System.Collections.Immutable;
using System.Text;

namespace Typstium;

public sealed class TypstExecutableNotFoundException : TypstException
{
    public TypstExecutableNotFoundException(ImmutableArray<TypstLocatorSearch> searches)
        : base(BuildMessage(searches))
    {
        Searches = searches;
    }

    public ImmutableArray<TypstLocatorSearch> Searches { get; }

    private static string BuildMessage(ImmutableArray<TypstLocatorSearch> searches)
    {
        var builder = new StringBuilder("Typst was not found. Install it (for example 'winget install Typst.Typst'), set TYPST_PATH, or pass an explicit path.");
        builder.AppendLine().Append("Searched:");
        foreach (var search in searches)
        {
            foreach (var location in search.SearchedLocations)
            {
                builder.AppendLine().Append("  ").Append(search.LocatorName).Append(": ").Append(location);
            }
        }

        return builder.ToString();
    }
}
```

- [ ] **Step 4: Implement the environment seam and locators**

`src/Typstium/Executable/ILocatorEnvironment.cs`:

```csharp
namespace Typstium.Executable;

/// <summary>The machine as locators see it; faked in unit tests.</summary>
internal interface ILocatorEnvironment
{
    bool IsWindows { get; }

    string BaseDirectory { get; }

    /// <summary>e.g. win-x64, linux-arm64, osx-arm64.</summary>
    string RuntimeIdentifier { get; }

    string? HomeDirectory { get; }

    string? GetVariable(string name);

    bool FileExists(string path);

    /// <returns>Full paths of matching child directories; empty when <paramref name="path"/> is missing.</returns>
    IEnumerable<string> EnumerateDirectories(string path, string searchPattern);

    /// <summary>Restores the execute bit NuGet extraction drops on Unix. No-op on Windows.</summary>
    void EnsureExecutable(string path);
}
```

`src/Typstium/Executable/SystemLocatorEnvironment.cs`:

```csharp
using System.Runtime.InteropServices;

namespace Typstium.Executable;

internal sealed class SystemLocatorEnvironment : ILocatorEnvironment
{
    public static SystemLocatorEnvironment Instance { get; } = new();

    public bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public string BaseDirectory => AppContext.BaseDirectory;

    public string RuntimeIdentifier
    {
        get
        {
            var os = IsWindows ? "win" : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx" : "linux";
            var architecture = RuntimeInformation.OSArchitecture switch
            {
                Architecture.X64 => "x64",
                Architecture.Arm64 => "arm64",
                Architecture.Arm => "arm",
                Architecture.X86 => "x86",
                var other => other.ToString().ToLowerInvariant(),
            };
            return os + "-" + architecture;
        }
    }

    public string? HomeDirectory
    {
        get
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return string.IsNullOrEmpty(home) ? Environment.GetEnvironmentVariable("HOME") : home;
        }
    }

    public string? GetVariable(string name) => Environment.GetEnvironmentVariable(name);

    public bool FileExists(string path) => File.Exists(path);

    public IEnumerable<string> EnumerateDirectories(string path, string searchPattern)
    {
        try
        {
            return Directory.Exists(path) ? Directory.GetDirectories(path, searchPattern) : Array.Empty<string>();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    public void EnsureExecutable(string path)
    {
#if NET8_0_OR_GREATER
        if (IsWindows)
        {
            return;
        }

        try
        {
            var mode = File.GetUnixFileMode(path);
            const UnixFileMode execute = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            if ((mode & UnixFileMode.UserExecute) == 0)
            {
                File.SetUnixFileMode(path, mode | execute);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Read-only deployment: starting the process will report the problem.
        }
#endif
    }
}
```

`src/Typstium/Executable/Locators.cs`:

```csharp
namespace Typstium.Executable;

internal sealed class ExplicitPathLocator(string path, ILocatorEnvironment environment) : ITypstExecutableLocator
{
    public string Name => "ExplicitPath";

    public ValueTask<TypstLocatorResult> LocateAsync(CancellationToken cancellationToken)
    {
        var full = Path.GetFullPath(path);
        return new(environment.FileExists(full) ? TypstLocatorResult.Found(full) : TypstLocatorResult.NotFound(new[] { full }));
    }
}

internal sealed class EnvironmentVariableLocator(string variable, ILocatorEnvironment environment) : ITypstExecutableLocator
{
    public string Name => variable;

    public ValueTask<TypstLocatorResult> LocateAsync(CancellationToken cancellationToken)
    {
        var value = environment.GetVariable(variable);
        if (string.IsNullOrWhiteSpace(value))
        {
            return new(TypstLocatorResult.NotFound(new[] { $"%{variable}% (not set)" }));
        }

        var full = Path.GetFullPath(value);
        return new(environment.FileExists(full) ? TypstLocatorResult.Found(full) : TypstLocatorResult.NotFound(new[] { full }));
    }
}

/// <summary>typst/&lt;rid&gt;/ next to the app, as copied by the Typstium runtime packages.</summary>
internal sealed class BundledRuntimeLocator(ILocatorEnvironment environment) : ITypstExecutableLocator
{
    public string Name => "BundledRuntime";

    public ValueTask<TypstLocatorResult> LocateAsync(CancellationToken cancellationToken)
    {
        var executable = environment.IsWindows ? "typst.exe" : "typst";
        var candidates = new[]
        {
            Path.Combine(environment.BaseDirectory, "typst", environment.RuntimeIdentifier, executable),
            Path.Combine(environment.BaseDirectory, "bin", "typst", environment.RuntimeIdentifier, executable),
        };

        foreach (var candidate in candidates)
        {
            if (environment.FileExists(candidate))
            {
                environment.EnsureExecutable(candidate);
                return new(TypstLocatorResult.Found(candidate));
            }
        }

        return new(TypstLocatorResult.NotFound(candidates));
    }
}

internal sealed class WinGetLocator(ILocatorEnvironment environment) : ITypstExecutableLocator
{
    public string Name => "WinGet";

    public ValueTask<TypstLocatorResult> LocateAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsWindows)
        {
            return new(TypstLocatorResult.NotFound(new[] { "(WinGet is Windows only)" }));
        }

        var searched = new List<string>();
        var roots = new[]
        {
            environment.GetVariable("LOCALAPPDATA") is { Length: > 0 } local ? Path.Combine(local, "Microsoft", "WinGet") : null,
            environment.GetVariable("ProgramFiles") is { Length: > 0 } programFiles ? Path.Combine(programFiles, "WinGet") : null,
        };

        foreach (var root in roots.OfType<string>())
        {
            var link = Path.Combine(root, "Links", "typst.exe");
            searched.Add(link);
            if (environment.FileExists(link))
            {
                return new(TypstLocatorResult.Found(link));
            }
        }

        foreach (var root in roots.OfType<string>())
        {
            var packages = Path.Combine(root, "Packages");
            searched.Add(Path.Combine(packages, "Typst.Typst_*", "typst-*", "typst.exe"));
            foreach (var package in environment.EnumerateDirectories(packages, "Typst.Typst_*"))
            {
                foreach (var build in environment.EnumerateDirectories(package, "typst-*"))
                {
                    var candidate = Path.Combine(build, "typst.exe");
                    if (environment.FileExists(candidate))
                    {
                        return new(TypstLocatorResult.Found(candidate));
                    }
                }
            }
        }

        return new(TypstLocatorResult.NotFound(searched));
    }
}

internal sealed class WellKnownLocationsLocator(ILocatorEnvironment environment) : ITypstExecutableLocator
{
    public string Name => "WellKnownLocations";

    public ValueTask<TypstLocatorResult> LocateAsync(CancellationToken cancellationToken)
    {
        var candidates = new List<string>();
        var home = environment.HomeDirectory;
        if (environment.IsWindows)
        {
            if (home is not null)
            {
                candidates.Add(Path.Combine(home, ".cargo", "bin", "typst.exe"));
                candidates.Add(Path.Combine(home, "scoop", "shims", "typst.exe"));
            }
        }
        else
        {
            if (home is not null)
            {
                candidates.Add(Path.Combine(home, ".cargo", "bin", "typst"));
            }

            candidates.Add("/opt/homebrew/bin/typst");
            candidates.Add("/usr/local/bin/typst");
        }

        var found = candidates.FirstOrDefault(environment.FileExists);
        return new(found is not null ? TypstLocatorResult.Found(found) : TypstLocatorResult.NotFound(candidates));
    }
}

/// <summary>
/// PATH, absolute entries only: never the current directory. On Windows only typst.exe is accepted, because
/// .cmd/.bat shims can't be started without a shell.
/// </summary>
internal sealed class SearchPathLocator(ILocatorEnvironment environment) : ITypstExecutableLocator
{
    public string Name => "PATH";

    public ValueTask<TypstLocatorResult> LocateAsync(CancellationToken cancellationToken)
    {
        var executable = environment.IsWindows ? "typst.exe" : "typst";
        var searched = new List<string>();
        var entries = (environment.GetVariable("PATH") ?? string.Empty).Split(Path.PathSeparator);
        foreach (var entry in entries)
        {
            var directory = entry.Trim().Trim('"');
            if (directory.Length == 0 || !Path.IsPathRooted(directory))
            {
                continue;
            }

            var candidate = Path.Combine(directory, executable);
            searched.Add(candidate);
            if (environment.FileExists(candidate))
            {
                return new(TypstLocatorResult.Found(candidate));
            }
        }

        return new(TypstLocatorResult.NotFound(searched.Count > 0 ? searched : new List<string> { "(PATH has no absolute entries)" }));
    }
}
```

`Path.IsPathRooted` returns true for `\foo` on Windows (rooted but drive-relative). That's acceptable:
it's never the current directory. The fake-environment test uses absolute temp paths.

`src/Typstium/Executable/TypstExecutableLocators.cs`:

```csharp
using Typstium.Executable;

namespace Typstium;

/// <summary>Factories for the built-in locators, to compose a custom TypstExecutableResolver.</summary>
public static class TypstExecutableLocators
{
    public static ITypstExecutableLocator ExplicitPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new ExplicitPathLocator(path, SystemLocatorEnvironment.Instance);
    }

    public static ITypstExecutableLocator EnvironmentVariable() => EnvironmentVariable("TYPST_PATH");

    public static ITypstExecutableLocator EnvironmentVariable(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new EnvironmentVariableLocator(name, SystemLocatorEnvironment.Instance);
    }

    public static ITypstExecutableLocator BundledRuntime() => new BundledRuntimeLocator(SystemLocatorEnvironment.Instance);

    public static ITypstExecutableLocator WinGet() => new WinGetLocator(SystemLocatorEnvironment.Instance);

    public static ITypstExecutableLocator WellKnownLocations() => new WellKnownLocationsLocator(SystemLocatorEnvironment.Instance);

    public static ITypstExecutableLocator SearchPath() => new SearchPathLocator(SystemLocatorEnvironment.Instance);
}
```

`src/Typstium/Executable/TypstExecutableResolver.cs`:

```csharp
using System.Collections.Immutable;

namespace Typstium;

/// <summary>Tries locators in order; the first hit is cached for the resolver's lifetime. Failures are not cached.</summary>
public sealed class TypstExecutableResolver : ITypstExecutableResolver
{
    private readonly ImmutableArray<ITypstExecutableLocator> _locators;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private TypstExecutable? _resolved;

    public TypstExecutableResolver(IEnumerable<ITypstExecutableLocator> locators)
    {
        ArgumentNullException.ThrowIfNull(locators);
        _locators = locators.ToImmutableArray();
        if (_locators.IsEmpty || _locators.Any(locator => locator is null))
        {
            throw new ArgumentException("At least one locator is required, and none may be null.", nameof(locators));
        }
    }

    /// <summary>TYPST_PATH → bundled runtime → WinGet → well-known locations → PATH.</summary>
    public static TypstExecutableResolver CreateDefault() => new(DefaultLocators(explicitPath: null));

    /// <summary>The explicit path first, then the default chain.</summary>
    public static TypstExecutableResolver CreateDefault(string explicitPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(explicitPath);
        return new TypstExecutableResolver(DefaultLocators(explicitPath));
    }

    public async ValueTask<TypstExecutable> ResolveAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _resolved) is { } cached)
        {
            return cached;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_resolved is { } resolved)
            {
                return resolved;
            }

            var searches = ImmutableArray.CreateBuilder<TypstLocatorSearch>();
            foreach (var locator in _locators)
            {
                var result = await locator.LocateAsync(cancellationToken).ConfigureAwait(false);
                switch (result)
                {
                    case TypstLocatorResult.FoundResult found:
                        var executable = new TypstExecutable(found.Path, locator.Name);
                        Volatile.Write(ref _resolved, executable);
                        return executable;
                    case TypstLocatorResult.NotFoundResult notFound:
                        searches.Add(new TypstLocatorSearch(locator.Name, notFound.SearchedLocations));
                        break;
                }
            }

            throw new TypstExecutableNotFoundException(searches.ToImmutable());
        }
        finally
        {
            _gate.Release();
        }
    }

    internal static IEnumerable<ITypstExecutableLocator> DefaultLocators(string? explicitPath)
    {
        if (explicitPath is not null)
        {
            yield return TypstExecutableLocators.ExplicitPath(explicitPath);
        }

        yield return TypstExecutableLocators.EnvironmentVariable();
        yield return TypstExecutableLocators.BundledRuntime();
        yield return TypstExecutableLocators.WinGet();
        yield return TypstExecutableLocators.WellKnownLocations();
        yield return TypstExecutableLocators.SearchPath();
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test --project tests/Typstium.UnitTests --treenode-filter "/*/*/LocatorTests/*"`
Then: `dotnet test --project tests/Typstium.UnitTests --treenode-filter "/*/*/TypstExecutableResolverTests/*"`
Expected: PASS on every framework and OS.

- [ ] **Step 6: Commit**

```bash
git add src tests
git commit -m "Locate Typst through an ordered chain of locators

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 18: The CLI compiler pipeline

`CliTypstCompiler` orchestrates the ten steps (spec 5.1) and maps outcomes to results or exceptions
(spec 5.5). It pins Review Focus 3 (input over the command-line limit) and Review Focus 4 (parallel cold
start).

**Files:**
- Create: `src/Typstium/CliTypstCompilerSettings.cs`, `src/Typstium/CliTypstCompiler.cs`, `src/Typstium/TypstCompiler.cs`
- Create: `src/Typstium/Compilation/RequestValidator.cs`, `CapabilityValidator.cs`, `DocumentCollector.cs`, `OutcomeMapper.cs`, `TypstTelemetry.cs` (all under `src/Typstium/Compilation/`)
- Create: `tests/Typstium.Testing/Fakes/FixedExecutableResolver.cs`
- Test: `tests/Typstium.UnitTests/Compilation/CliTypstCompilerTests.cs`

**Interfaces:**
- Consumes: everything from Tasks 2–17.
- Produces:
  - `public sealed record CliTypstCompilerSettings { Default; int MaxConcurrency; string TempRoot; TimeSpan OrphanedWorkspaceAge }`
  - `public sealed class CliTypstCompiler : ITypstCompiler`: public constructor `(ITypstExecutableResolver, CliTypstCompilerSettings)`, internal constructor `(ITypstExecutableResolver, CliTypstCompilerSettings, ITypstProcessRunner)`
  - `public static class TypstCompiler { ITypstCompiler CreateDefault(); ITypstCompiler CreateDefault(CliTypstCompilerSettings settings); }`
  - internal `RequestValidator.Validate(TypstCompileRequest)`, `CapabilityValidator.Validate(TypstCompileRequest, TypstCapabilities)`, `DocumentCollector.Collect(TypstOutput, ReadOnlyMemory<byte>, WorkspaceLayout)`, `OutcomeMapper.Map(...)`, `TypstTelemetry` (`ActivitySource` named `Typstium`, activity `typst.compile`)
  - Test fake `Typstium.Testing.Fakes.FixedExecutableResolver(string path) : ITypstExecutableResolver`

- [ ] **Step 1: Write the failing tests**

`tests/Typstium.Testing/Fakes/FixedExecutableResolver.cs`:

```csharp
namespace Typstium.Testing.Fakes;

public sealed class FixedExecutableResolver(string path) : ITypstExecutableResolver
{
    public ValueTask<TypstExecutable> ResolveAsync(CancellationToken cancellationToken) =>
        new(new TypstExecutable(path, "Fixed"));
}
```

`tests/Typstium.UnitTests/Compilation/CliTypstCompilerTests.cs`:

```csharp
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Typstium.CommandLine;
using Typstium.Processes;
using Typstium.Testing.Fakes;
using Typstium.Testing.Fixtures;
using Typstium.UnitTests.Testing;

namespace Typstium.UnitTests.Compilation;

public class CliTypstCompilerTests
{
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.7 fake");

    private sealed class Harness : IDisposable
    {
        public Harness(Func<ProcessSpec, ProcessOutcome> compile, bool html = true, TimeSpan delay = default, int maxConcurrency = 4)
        {
            Temp = new TemporaryDirectory("compiler");
            Runner = new FakeProcessRunner(spec => FakeProcessRunner.IsInfo(spec)
                ? FakeProcessRunner.Exited(0, FakeProcessRunner.InfoJson("0.15.1", html))
                : compile(spec)) { Delay = delay };
            TempRoot = Path.Combine(Temp.Path, "tmp");
            var settings = CliTypstCompilerSettings.Default with { TempRoot = TempRoot, MaxConcurrency = maxConcurrency };
            Compiler = new CliTypstCompiler(new FixedExecutableResolver(Path.Combine(Temp.Path, "typst.exe")), settings, Runner);
        }

        public TemporaryDirectory Temp { get; }

        public FakeProcessRunner Runner { get; }

        public string TempRoot { get; }

        public CliTypstCompiler Compiler { get; }

        public IEnumerable<ProcessSpec> CompileSpecs => Runner.Specs.Where(spec => !FakeProcessRunner.IsInfo(spec));

        public bool NoWorkspacesLeft => !Directory.Exists(TempRoot) || Directory.GetDirectories(TempRoot, "ws-*").Length == 0;

        public void Dispose() => Temp.Dispose();
    }

    /// <summary>The root as Typst prints it: \\?\-prefixed on Windows.</summary>
    private static string PrintedRoot(ProcessSpec spec) =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"\\?\" + spec.WorkingDirectory : spec.WorkingDirectory;

    [Test]
    public async Task Successful_pdf_compile()
    {
        using var harness = new Harness(_ => FakeProcessRunner.Exited(0, Encoding.ASCII.GetString(Pdf)));
        var request = TypstCompileRequest.ForMarkup("= Hi").WithInput("secret", "customer-data").Build();

        var result = await harness.Compiler.CompileAsync(request);

        await Assert.That(result.Status).IsEqualTo(TypstCompileStatus.Succeeded);
        await Assert.That(((TypstDocument.SingleFile)result.Document!).Content.ToArray()).IsEquivalentTo(Pdf);
        await Assert.That(result.Invocation.ExecutableFoundBy).IsEqualTo("Fixed");
        await Assert.That(result.Invocation.TypstVersion).IsEqualTo(new TypstVersion(0, 15, 1));
        await Assert.That(result.Invocation.CommandLine).Contains("compile");
        await Assert.That(result.Invocation.CommandLine).DoesNotContain("customer-data");
        await Assert.That(harness.NoWorkspacesLeft).IsTrue();
    }

    [Test]
    public async Task Markup_goes_over_stdin_with_a_blocked_network()
    {
        using var harness = new Harness(_ => FakeProcessRunner.Exited(0, "%PDF"));
        await harness.Compiler.CompileMarkupAsync("= Hi");

        var spec = harness.CompileSpecs.Single();
        await Assert.That(Encoding.UTF8.GetString(spec.Stdin.ToArray())).IsEqualTo("= Hi");
        await Assert.That(spec.Environment.Set["HTTPS_PROXY"]).IsEqualTo(ProcessEnvironmentBuilder.DeadProxy);
        await Assert.That(spec.Timeout).IsEqualTo(TimeSpan.FromSeconds(60));
    }

    [Test]
    public async Task Template_failures_become_results_with_mapped_diagnostics()
    {
        var fixture = DiagnosticFixtures.Get(new TypstVersion(0, 15, 0), "02-unknown-variable");
        using var harness = new Harness(spec => FakeProcessRunner.Exited(1, stderr: fixture.StandardErrorFor(PrintedRoot(spec))));
        var templates = new InMemoryTemplateProvider().Add("broken", "02-unknown-variable.typ", ("02-unknown-variable.typ", "#undefined-thing"));

        var result = await harness.Compiler.CompileTemplateAsync(templates, "broken");

        await Assert.That(result.Status).IsEqualTo(TypstCompileStatus.Failed);
        var diagnostic = result.Diagnostics.Single();
        await Assert.That(diagnostic.Id.Value).IsEqualTo("TYPST1001");
        await Assert.That(diagnostic.Location!.File).IsEqualTo((TypstSourceFile)new TypstSourceFile.Project(TypstPath.Parse("02-unknown-variable.typ"), null));
        await Assert.That(diagnostic.ToString()).DoesNotContain(harness.TempRoot);
    }

    [Test]
    public async Task Exit_code_1_without_diagnostics_still_explains_itself()
    {
        using var harness = new Harness(_ => FakeProcessRunner.Exited(1));
        var result = await harness.Compiler.CompileMarkupAsync("x");
        await Assert.That(result.Status).IsEqualTo(TypstCompileStatus.Failed);
        await Assert.That(result.Errors.Single().Id.Value).IsEqualTo("TYPST0001");
    }

    [Test]
    public async Task Exit_code_2_is_a_typstium_bug()
    {
        using var harness = new Harness(_ => FakeProcessRunner.Exited(2, stderr: "error: unexpected argument '--bogus'"));
        var request = TypstCompileRequest.ForMarkup("x").WithInput("k", "secret-value").Build();
        var exception = await Assert.ThrowsAsync<TypstInvocationException>(() => harness.Compiler.CompileAsync(request));
        await Assert.That(exception.CommandLine).DoesNotContain("secret-value");
        await Assert.That(harness.NoWorkspacesLeft).IsTrue();
    }

    [Test]
    public async Task Other_exit_codes_are_crashes()
    {
        using var harness = new Harness(_ => FakeProcessRunner.Exited(101, stderr: "thread 'main' panicked"));
        var exception = await Assert.ThrowsAsync<TypstProcessCrashedException>(() => harness.Compiler.CompileMarkupAsync("x"));
        await Assert.That(exception.ExitCode).IsEqualTo(101);
    }

    [Test]
    public async Task Timeouts_and_output_limits_are_statuses()
    {
        using var timedOut = new Harness(_ => new ProcessOutcome.TimedOut(TimeSpan.FromSeconds(60), string.Empty));
        using var tooBig = new Harness(_ => new ProcessOutcome.OutputLimitExceeded(1024, TimeSpan.FromSeconds(1), string.Empty));

        var timeoutResult = await timedOut.Compiler.CompileMarkupAsync("x");
        var limitResult = await tooBig.Compiler.CompileMarkupAsync("x");

        await Assert.That(timeoutResult.Status).IsEqualTo(TypstCompileStatus.TimedOut);
        await Assert.That(timeoutResult.Diagnostics.Last().Id.Value).IsEqualTo("TYPST9002");
        await Assert.That(limitResult.Status).IsEqualTo(TypstCompileStatus.OutputLimitExceeded);
        await Assert.That(limitResult.Diagnostics.Last().Id.Value).IsEqualTo("TYPST9001");
    }

    [Test]
    public async Task Success_without_output_is_unreadable_output()
    {
        using var harness = new Harness(_ => FakeProcessRunner.Exited(0));
        await Assert.ThrowsAsync<TypstProcessOutputException>(() => harness.Compiler.CompileMarkupAsync("x"));
    }

    [Test]
    public async Task Png_pages_are_collected_in_order()
    {
        using var harness = new Harness(spec =>
        {
            var pattern = spec.Arguments.ToValues().Single(value => value.IndexOf("{0p}", StringComparison.Ordinal) >= 0);
            File.WriteAllBytes(pattern.Replace("{0p}", "2"), new byte[] { 2 });
            File.WriteAllBytes(pattern.Replace("{0p}", "1"), new byte[] { 1 });
            return FakeProcessRunner.Exited(0);
        });

        var result = await harness.Compiler.CompileMarkupAsync("x", TypstOutput.Png());

        var pages = ((TypstDocument.Paged)result.Document!).Pages;
        await Assert.That(pages.Select(page => page.Span[0])).IsEquivalentTo(new byte[] { 1, 2 });
    }

    [Test]
    public async Task Html_needs_the_html_capability()
    {
        using var harness = new Harness(_ => FakeProcessRunner.Exited(0, "<html/>"), html: false);
        await Assert.ThrowsAsync<TypstFeatureNotSupportedException>(() => harness.Compiler.CompileMarkupAsync("x", TypstOutput.Html()));
        await Assert.That(harness.CompileSpecs.Count()).IsEqualTo(0);
    }

    [Test]
    public async Task Inputs_over_the_command_line_limit_fail_before_typst_runs()
    {
        using var harness = new Harness(_ => FakeProcessRunner.Exited(0, "%PDF"));
        var request = TypstCompileRequest.ForMarkup("x").WithInput("data", new string('x', 40_000)).Build();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => harness.Compiler.CompileAsync(request));

        await Assert.That(exception.Message).Contains("virtual file");
        await Assert.That(harness.CompileSpecs.Count()).IsEqualTo(0);
        await Assert.That(harness.NoWorkspacesLeft).IsTrue();
    }

    [Test]
    public async Task Parallel_cold_start_probes_once_and_respects_max_concurrency()
    {
        using var harness = new Harness(_ => FakeProcessRunner.Exited(0, "%PDF"), delay: TimeSpan.FromMilliseconds(30), maxConcurrency: 3);

        var results = await Task.WhenAll(Enumerable.Range(0, 30).Select(i => Task.Run(() => harness.Compiler.CompileMarkupAsync("= " + i))));

        await Assert.That(results.All(result => result.IsSuccess)).IsTrue();
        await Assert.That(harness.Runner.Specs.Count(FakeProcessRunner.IsInfo)).IsEqualTo(1);
        await Assert.That(harness.Runner.MaxObservedConcurrency).IsLessThanOrEqualTo(3);
        await Assert.That(harness.NoWorkspacesLeft).IsTrue();
    }

    [Test]
    public async Task Cancellation_propagates_and_cleans_up()
    {
        using var harness = new Harness(_ => FakeProcessRunner.Exited(0, "%PDF"), delay: TimeSpan.FromSeconds(30));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAsync<OperationCanceledException>(() => harness.Compiler.CompileMarkupAsync("x", cancellation.Token));
        await Assert.That(harness.NoWorkspacesLeft).IsTrue();
    }

    [Test]
    public async Task Each_compile_emits_an_activity()
    {
        var stopped = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Typstium",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => { lock (stopped) { stopped.Add(activity); } },
        };
        ActivitySource.AddActivityListener(listener);
        using var harness = new Harness(_ => FakeProcessRunner.Exited(0, "%PDF"));

        await harness.Compiler.CompileMarkupAsync("x");

        Activity activity;
        lock (stopped)
        {
            activity = stopped.Single(a => a.OperationName == "typst.compile" && (string?)a.GetTagItem("typst.source.kind") == "markup");
        }

        await Assert.That((string?)activity.GetTagItem("typst.status")).IsEqualTo("Succeeded");
    }
}
```

The activity test filters by `source.kind == "markup"`, because other tests may run in parallel and emit
activities of their own.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/Typstium.UnitTests --framework net10.0 --treenode-filter "/*/*/CliTypstCompilerTests/*"`
Expected: build FAILS with `CS0246` for `CliTypstCompiler`.

- [ ] **Step 3: Implement the settings and the small pipeline components**

`src/Typstium/CliTypstCompilerSettings.cs`:

```csharp
namespace Typstium;

/// <summary>Settings of the compiler itself, as opposed to per-request options.</summary>
public sealed record CliTypstCompilerSettings
{
    private int _maxConcurrency = Environment.ProcessorCount;
    private string _tempRoot = Path.Combine(Path.GetTempPath(), "typstium");
    private TimeSpan _orphanedWorkspaceAge = TimeSpan.FromHours(1);

    public static CliTypstCompilerSettings Default { get; } = new();

    /// <summary>The maximum number of Typst processes running at once.</summary>
    public int MaxConcurrency
    {
        get => _maxConcurrency;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _maxConcurrency = value;
        }
    }

    /// <summary>Where sandbox folders are created.</summary>
    public string TempRoot
    {
        get => _tempRoot;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _tempRoot = value;
        }
    }

    /// <summary>Sandbox folders older than this are deleted on the first compile (left behind by hard kills).</summary>
    public TimeSpan OrphanedWorkspaceAge
    {
        get => _orphanedWorkspaceAge;
        init
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The age must be positive.");
            }

            _orphanedWorkspaceAge = value;
        }
    }
}
```

`src/Typstium/Compilation/RequestValidator.cs`:

```csharp
namespace Typstium.Compilation;

/// <summary>Step 1: caller errors become ArgumentException before any I/O.</summary>
internal static class RequestValidator
{
    public static void Validate(TypstCompileRequest request)
    {
        if (request.Source is not (TypstSource.FileSource or TypstSource.TemplateSource or TypstSource.MarkupSource))
        {
            throw new ArgumentException($"Unsupported source kind '{request.Source.GetType().Name}'.", nameof(request));
        }

        if (request.Output is not (TypstOutput.PdfOutput or TypstOutput.PngOutput or TypstOutput.SvgOutput or TypstOutput.HtmlOutput))
        {
            throw new ArgumentException($"Unsupported output kind '{request.Output.GetType().Name}'.", nameof(request));
        }
    }
}
```

`src/Typstium/Compilation/CapabilityValidator.cs`:

```csharp
using Typstium.Capabilities;

namespace Typstium.Compilation;

/// <summary>Step 3: refuses requests the installed Typst can't serve, before starting a compile.</summary>
internal static class CapabilityValidator
{
    public static void Validate(TypstCompileRequest request, TypstCapabilities capabilities)
    {
        switch (request.Output)
        {
            case TypstOutput.HtmlOutput when !capabilities.SupportsHtml:
                throw new TypstFeatureNotSupportedException("HTML export", capabilities.Version, requiredVersion: null);

            case TypstOutput.PdfOutput pdf:
                foreach (var standard in pdf.Options.Standards)
                {
                    if (!capabilities.PdfStandards.Contains(standard))
                    {
                        throw new TypstFeatureNotSupportedException($"PDF standard '{standard}'", capabilities.Version, requiredVersion: null);
                    }
                }

                break;
        }
    }
}
```

`src/Typstium/Compilation/DocumentCollector.cs`:

```csharp
using System.Collections.Immutable;
using Typstium.Workspace;

namespace Typstium.Compilation;

/// <summary>Step 10: the document from stdout, or the page files in page order.</summary>
internal static class DocumentCollector
{
    public static TypstDocument Collect(TypstOutput output, ReadOnlyMemory<byte> standardOutput, WorkspaceLayout layout)
    {
        if (!output.IsPaged)
        {
            if (standardOutput.IsEmpty)
            {
                throw new TypstProcessOutputException("Typst exited successfully but wrote no document to stdout.");
            }

            return TypstDocument.CreateSingleFile(output.Format, standardOutput);
        }

        var directory = layout.PagedOutputDirectory
            ?? throw new InvalidOperationException("Paged output needs an output folder in the workspace.");

        // {0p} pads every page number to the same width, so ordinal order is page order.
        var files = Directory.GetFiles(directory, "*." + output.FileExtension).OrderBy(file => file, StringComparer.Ordinal).ToArray();
        if (files.Length == 0)
        {
            throw new TypstProcessOutputException($"Typst exited successfully but wrote no {output.FileExtension} pages.");
        }

        return TypstDocument.CreatePaged(output.Format, files.Select(file => (ReadOnlyMemory<byte>)File.ReadAllBytes(file)).ToImmutableArray());
    }
}
```

`src/Typstium/Compilation/OutcomeMapper.cs`:

```csharp
using System.Collections.Immutable;
using Typstium.Diagnostics;
using Typstium.Processes;
using Typstium.Workspace;

namespace Typstium.Compilation;

/// <summary>Step 9: process outcome → result status, or an exception when Typstium or the environment is at fault.</summary>
internal static class OutcomeMapper
{
    public static TypstCompileResult Map(
        ProcessOutcome outcome,
        ImmutableArray<TypstDiagnostic> diagnostics,
        TypstCompileRequest request,
        WorkspaceLayout layout,
        string commandLine,
        TypstInvocation invocation)
    {
        switch (outcome)
        {
            case ProcessOutcome.TimedOut:
                return TypstCompileResult.CreateUnsuccessful(
                    TypstCompileStatus.TimedOut,
                    diagnostics.Add(Diagnostic(DiagnosticIds.TimedOut, $"Typst did not finish within {request.Options.Timeout.TotalSeconds:0.#} seconds.")),
                    invocation);

            case ProcessOutcome.OutputLimitExceeded exceeded:
                return OutputLimitExceeded(diagnostics, exceeded.LimitBytes, invocation);

            case ProcessOutcome.Exited { ExitCode: 0 } exited:
                var document = DocumentCollector.Collect(request.Output, exited.StandardOutput, layout);
                if (document is TypstDocument.Paged paged && paged.Pages.Sum(page => (long)page.Length) > request.Options.MaxOutputBytes)
                {
                    return OutputLimitExceeded(diagnostics, request.Options.MaxOutputBytes, invocation);
                }

                return TypstCompileResult.CreateSucceeded(document, diagnostics, invocation);

            case ProcessOutcome.Exited { ExitCode: 1 }:
                if (!diagnostics.Any(diagnostic => diagnostic.Severity == TypstDiagnosticSeverity.Error))
                {
                    diagnostics = diagnostics.Add(Diagnostic(DiagnosticIds.GenericError, "Typst reported a failure without an error message."));
                }

                return TypstCompileResult.CreateUnsuccessful(TypstCompileStatus.Failed, diagnostics, invocation);

            case ProcessOutcome.Exited { ExitCode: 2 } invalid:
                throw new TypstInvocationException(commandLine, invalid.ExitCode, invalid.StandardError);

            case ProcessOutcome.Exited crashed:
                throw new TypstProcessCrashedException(crashed.ExitCode, crashed.StandardError);

            default:
                throw new InvalidOperationException($"Unknown process outcome '{outcome.GetType().Name}'.");
        }
    }

    private static TypstCompileResult OutputLimitExceeded(ImmutableArray<TypstDiagnostic> diagnostics, long limitBytes, TypstInvocation invocation) =>
        TypstCompileResult.CreateUnsuccessful(
            TypstCompileStatus.OutputLimitExceeded,
            diagnostics.Add(Diagnostic(DiagnosticIds.OutputLimitExceeded, $"The output exceeded the limit of {limitBytes} bytes (MaxOutputBytes).")),
            invocation);

    private static TypstDiagnostic Diagnostic(TypstDiagnosticId id, string message) =>
        new(id, TypstDiagnosticSeverity.Error, message, location: null, ImmutableArray<string>.Empty, ImmutableArray<TypstTraceFrame>.Empty);
}
```

`src/Typstium/Compilation/TypstTelemetry.cs`:

```csharp
using System.Diagnostics;

namespace Typstium.Compilation;

/// <summary>OpenTelemetry-friendly tracing: one "typst.compile" activity per compile.</summary>
internal static class TypstTelemetry
{
    internal const string SourceName = "Typstium";

    internal static readonly ActivitySource Source = new(SourceName, typeof(TypstTelemetry).Assembly.GetName().Version?.ToString());

    public static Activity? StartCompile(TypstCompileRequest request)
    {
        var activity = Source.StartActivity("typst.compile");
        if (activity is null)
        {
            return null;
        }

        activity.SetTag("typst.source.kind", request.Source switch
        {
            TypstSource.FileSource => "file",
            TypstSource.TemplateSource => "template",
            TypstSource.MarkupSource => "markup",
            _ => "unknown",
        });
        if (request.Source is TypstSource.TemplateSource template)
        {
            activity.SetTag("typst.template.name", template.Name.Value);
        }

        activity.SetTag("typst.output.format", request.Output.FileExtension);
        return activity;
    }

    public static void Complete(Activity? activity, TypstCompileResult result)
    {
        if (activity is null)
        {
            return;
        }

        activity.SetTag("typst.version", result.Invocation.TypstVersion.ToString());
        activity.SetTag("typst.status", result.Status.ToString());
        activity.SetTag("typst.process.duration_ms", result.Invocation.ProcessDuration.TotalMilliseconds);
        activity.SetTag("typst.diagnostics.errors", result.Errors.Count());
        activity.SetTag("typst.diagnostics.warnings", result.Warnings.Count());
        if (!result.IsSuccess)
        {
            activity.SetStatus(ActivityStatusCode.Error, result.Status.ToString());
        }
    }

    public static void Fail(Activity? activity, Exception exception) =>
        activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name + ": " + exception.Message);
}
```

- [ ] **Step 4: Implement the compiler**

`src/Typstium/CliTypstCompiler.cs`:

```csharp
using System.Diagnostics;
using Typstium.Capabilities;
using Typstium.CommandLine;
using Typstium.Compilation;
using Typstium.Diagnostics;
using Typstium.Processes;
using Typstium.Workspace;

namespace Typstium;

/// <summary>Compiles by running the official Typst CLI, one process per compile (spec 5).</summary>
public sealed class CliTypstCompiler : ITypstCompiler
{
    private readonly ITypstExecutableResolver _resolver;
    private readonly ITypstProcessRunner _runner;
    private readonly TypstCliProfileFactory _profiles;
    private readonly WorkspaceFactory _workspaces;
    private readonly SemaphoreSlim _concurrency;

    public CliTypstCompiler(ITypstExecutableResolver resolver, CliTypstCompilerSettings settings)
        : this(resolver, settings, new TypstProcessRunner())
    {
    }

    internal CliTypstCompiler(ITypstExecutableResolver resolver, CliTypstCompilerSettings settings, ITypstProcessRunner runner)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        ArgumentNullException.ThrowIfNull(settings);
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _profiles = new TypstCliProfileFactory(runner);
        _workspaces = new WorkspaceFactory(settings.TempRoot, settings.OrphanedWorkspaceAge);
        _concurrency = new SemaphoreSlim(settings.MaxConcurrency, settings.MaxConcurrency);
    }

    public async Task<TypstCompileResult> CompileAsync(TypstCompileRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequestValidator.Validate(request);
        var total = Stopwatch.StartNew();
        using var activity = TypstTelemetry.StartCompile(request);
        try
        {
            var result = await CompileCoreAsync(request, total, cancellationToken).ConfigureAwait(false);
            TypstTelemetry.Complete(activity, result);
            return result;
        }
        catch (Exception exception)
        {
            TypstTelemetry.Fail(activity, exception);
            throw;
        }
    }

    private async Task<TypstCompileResult> CompileCoreAsync(TypstCompileRequest request, Stopwatch total, CancellationToken cancellationToken)
    {
        var executable = await _resolver.ResolveAsync(cancellationToken).ConfigureAwait(false);
        var profile = await _profiles.GetProfileAsync(executable, cancellationToken).ConfigureAwait(false);
        CapabilityValidator.Validate(request, profile.Capabilities);

        var workspace = await _workspaces.CreateAsync(request, cancellationToken).ConfigureAwait(false);
        try
        {
            var layout = workspace.Layout;
            var arguments = profile.ArgumentsBuilder.Build(new CompileArgumentsInput(request, layout, profile.Capabilities));
            CommandLineLimits.EnsureFits(executable.Path, arguments);

            var spec = new ProcessSpec(
                executable.Path,
                arguments,
                ProcessEnvironmentBuilder.Build(ProcessEnvironmentBuilder.CurrentVariableNames(), request.Options),
                layout.RootDirectory,
                layout.Stdin,
                request.Options.Timeout,
                request.Options.MaxOutputBytes,
                ProcessSpec.DefaultMaxStderrBytes,
                request.Options.MemoryLimitBytes);

            ProcessOutcome outcome;
            await _concurrency.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                outcome = await _runner.RunAsync(spec, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _concurrency.Release();
            }

            var diagnostics = DiagnosticAssembler.Assemble(
                outcome.StandardError, profile.DiagnosticParser, profile.DiagnosticClassifier, new SourcePathMapper(layout.Mapping), request.Options);
            var commandLine = WindowsArgumentEscaper.Escape(executable.Path) + " " + arguments.RenderSafe();
            var invocation = new TypstInvocation(executable.Path, executable.FoundBy, profile.Capabilities.Version, commandLine, outcome.Duration, total.Elapsed);
            return OutcomeMapper.Map(outcome, diagnostics, request, layout, commandLine, invocation);
        }
        finally
        {
            await workspace.DisposeAsync().ConfigureAwait(false);
        }
    }
}
```

`src/Typstium/TypstCompiler.cs`:

```csharp
namespace Typstium;

/// <summary>The obvious default setup, for scripts and apps without a DI container.</summary>
public static class TypstCompiler
{
    public static ITypstCompiler CreateDefault() => CreateDefault(CliTypstCompilerSettings.Default);

    public static ITypstCompiler CreateDefault(CliTypstCompilerSettings settings) =>
        new CliTypstCompiler(TypstExecutableResolver.CreateDefault(), settings);
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test --project tests/Typstium.UnitTests --treenode-filter "/*/*/CliTypstCompilerTests/*"`
Expected: PASS on all frameworks.

- [ ] **Step 6: Run the whole unit test suite**

Run: `dotnet test --project tests/Typstium.UnitTests`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src tests
git commit -m "Add CliTypstCompiler pipeline with outcome mapping and telemetry

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 19: Directory and embedded-resource template providers

The two built-in providers (spec 4.9): one folder per template, main file `<name>.typ` or `main.typ`, and
the version is a SHA-256 over the sorted (path, content) pairs.

**Files:**
- Create: `src/Typstium/Templates/TemplateBundleRules.cs`, `TemplateHasher.cs`, `DirectoryTemplateProvider.cs`, `EmbeddedResourceTemplateProvider.cs` (all under `src/Typstium/Templates/`)
- Create: `tests/Typstium.UnitTests/TestTemplates/greeting/greeting.typ`, `tests/Typstium.UnitTests/TestTemplates/greeting/assets/logo.svg`
- Modify: `tests/Typstium.UnitTests/Typstium.UnitTests.csproj` (embed `TestTemplates`)
- Test: `tests/Typstium.UnitTests/Templates/DirectoryTemplateProviderTests.cs`, `tests/Typstium.UnitTests/Templates/EmbeddedResourceTemplateProviderTests.cs`

**Interfaces:**
- Consumes: `ITemplateProvider`, `TypstTemplate`, `TypstTemplateFile` (Task 5), `TypstTemplateName`, `TypstTemplateVersion`, `TypstPath` (Task 2).
- Produces:
  - `public sealed class DirectoryTemplateProvider(string rootDirectory) : ITemplateProvider` with `string RootDirectory`
  - `public sealed class EmbeddedResourceTemplateProvider(Assembly assembly, string resourcePrefix) : ITemplateProvider`
  - internal `TemplateBundleRules.SelectMainFile(TypstTemplateName, IEnumerable<TypstPath>)` and `TemplateHasher.ComputeAsync(IEnumerable<TypstTemplateFile>, CancellationToken)`

- [ ] **Step 1: Add the embedded test templates**

`tests/Typstium.UnitTests/TestTemplates/greeting/greeting.typ`:

```typ
#let data = json("/data.json")
= Hello #data.name
#image("/assets/logo.svg", width: 2cm)
```

`tests/Typstium.UnitTests/TestTemplates/greeting/assets/logo.svg`:

```xml
<svg xmlns="http://www.w3.org/2000/svg" width="10" height="10"><rect width="10" height="10" fill="#14365d"/></svg>
```

Add to `tests/Typstium.UnitTests/Typstium.UnitTests.csproj`. The `LogicalName` keeps `/` separators, the
documented pattern for embedded templates (spec 4.9):

```xml
  <ItemGroup>
    <EmbeddedResource Include="TestTemplates\**" LogicalName="TestTemplates/%(RecursiveDir)%(Filename)%(Extension)" />
  </ItemGroup>
```

- [ ] **Step 2: Write the failing tests**

`tests/Typstium.UnitTests/Templates/DirectoryTemplateProviderTests.cs`:

```csharp
using Typstium.Testing;
using Typstium.Testing.Fixtures;

namespace Typstium.UnitTests.Templates;

public class DirectoryTemplateProviderTests
{
    private static readonly DirectoryTemplateProvider Samples = new(Path.Combine(RepositoryPaths.Samples, "templates"));

    [Test]
    public async Task Loads_the_license_sample_as_a_bundle()
    {
        var template = await Samples.GetTemplateAsync(TypstTemplateName.Parse("license"), CancellationToken.None);

        await Assert.That(template).IsNotNull();
        await Assert.That(template!.MainFile).IsEqualTo(TypstPath.Parse("license.typ"));
        await Assert.That(template.Files.Select(file => file.Path.Value)).Contains("logo.svg");
        await Assert.That(template.Version.Value).StartsWith("sha256:");
    }

    [Test]
    public async Task Version_is_stable_and_changes_with_content()
    {
        using var temp = new TemporaryDirectory("templates");
        var file = temp.CreateFile(Path.Combine("invoice", "main.typ"), "= Invoice");
        var provider = new DirectoryTemplateProvider(temp.Path);
        var name = TypstTemplateName.Parse("invoice");

        var first = await provider.GetTemplateAsync(name, CancellationToken.None);
        var second = await provider.GetTemplateAsync(name, CancellationToken.None);
        File.WriteAllText(file, "= Invoice v2");
        var third = await provider.GetTemplateAsync(name, CancellationToken.None);

        await Assert.That(first!.MainFile).IsEqualTo(TypstPath.Parse("main.typ"));
        await Assert.That(second!.Version).IsEqualTo(first.Version);
        await Assert.That(third!.Version.Equals(first.Version)).IsFalse();
    }

    [Test]
    public async Task Unknown_templates_return_null_and_bundles_without_a_main_file_are_invalid()
    {
        using var temp = new TemporaryDirectory("templates");
        temp.CreateFile(Path.Combine("broken", "other.typ"), "= Other");
        var provider = new DirectoryTemplateProvider(temp.Path);

        var missing = await provider.GetTemplateAsync(TypstTemplateName.Parse("nope"), CancellationToken.None);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetTemplateAsync(TypstTemplateName.Parse("broken"), CancellationToken.None).AsTask());

        await Assert.That(missing).IsNull();
        await Assert.That(exception.Message).Contains("broken.typ");
        await Assert.That(exception.Message).Contains("main.typ");
    }
}
```

`tests/Typstium.UnitTests/Templates/EmbeddedResourceTemplateProviderTests.cs`:

```csharp
namespace Typstium.UnitTests.Templates;

public class EmbeddedResourceTemplateProviderTests
{
    private static readonly EmbeddedResourceTemplateProvider Provider =
        new(typeof(EmbeddedResourceTemplateProviderTests).Assembly, "TestTemplates");

    [Test]
    public async Task Loads_templates_from_logical_names()
    {
        var template = await Provider.GetTemplateAsync(TypstTemplateName.Parse("greeting"), CancellationToken.None);

        await Assert.That(template).IsNotNull();
        await Assert.That(template!.MainFile).IsEqualTo(TypstPath.Parse("greeting.typ"));
        await Assert.That(template.Files.Select(file => file.Path.Value)).IsEquivalentTo(new[] { "assets/logo.svg", "greeting.typ" });

        var logo = template.Files.Single(file => file.Path.Value == "assets/logo.svg");
        using var stream = await logo.OpenReadAsync(CancellationToken.None);
        using var reader = new StreamReader(stream);
        await Assert.That(await reader.ReadToEndAsync()).Contains("<svg");
    }

    [Test]
    public async Task Unknown_templates_return_null()
    {
        var template = await Provider.GetTemplateAsync(TypstTemplateName.Parse("nope"), CancellationToken.None);
        await Assert.That(template).IsNull();
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test --project tests/Typstium.UnitTests --framework net10.0 --treenode-filter "/*/*/DirectoryTemplateProviderTests/*"`
Expected: build FAILS with `CS0246` for `DirectoryTemplateProvider`.

- [ ] **Step 4: Implement**

`src/Typstium/Templates/TemplateBundleRules.cs`:

```csharp
namespace Typstium.Templates;

internal static class TemplateBundleRules
{
    /// <summary>The main file is &lt;last name segment&gt;.typ, otherwise main.typ.</summary>
    public static TypstPath SelectMainFile(TypstTemplateName name, IEnumerable<TypstPath> files)
    {
        var paths = files.ToList();
        var byName = TypstPath.Parse(name.LastSegment + ".typ");
        var main = TypstPath.Parse("main.typ");
        if (paths.Contains(byName))
        {
            return byName;
        }

        if (paths.Contains(main))
        {
            return main;
        }

        throw new InvalidOperationException($"The template '{name}' has neither '{byName}' nor '{main}'.");
    }
}
```

`src/Typstium/Templates/TemplateHasher.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;

namespace Typstium.Templates;

internal static class TemplateHasher
{
    private static readonly byte[] Separator = [0];

    /// <summary>SHA-256 over (path, 0, content, 0) for every file in ordinal path order.</summary>
    public static async Task<TypstTemplateVersion> ComputeAsync(IEnumerable<TypstTemplateFile> files, CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        foreach (var file in files.OrderBy(file => file.Path.Value, StringComparer.Ordinal))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(file.Path.Value));
            hash.AppendData(Separator);
            var stream = await file.OpenReadAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                int read;
                while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    hash.AppendData(buffer, 0, read);
                }
            }
            finally
            {
                stream.Dispose();
            }

            hash.AppendData(Separator);
        }

        var digest = hash.GetHashAndReset();
        var hex = new StringBuilder("sha256:", 7 + (digest.Length * 2));
        foreach (var value in digest)
        {
            hex.Append(value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
        }

        return new TypstTemplateVersion(hex.ToString());
    }
}
```

`src/Typstium/Templates/DirectoryTemplateProvider.cs`:

```csharp
using Typstium.Templates;

namespace Typstium;

/// <summary>
/// Templates from a folder: each subfolder is a template named by its relative path, containing every
/// file below it. The main file is &lt;name&gt;.typ, otherwise main.typ.
/// </summary>
public sealed class DirectoryTemplateProvider : ITemplateProvider
{
    public DirectoryTemplateProvider(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        RootDirectory = Path.GetFullPath(rootDirectory);
    }

    public string RootDirectory { get; }

    public async ValueTask<TypstTemplate?> GetTemplateAsync(TypstTemplateName name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        var directory = Path.Combine(new[] { RootDirectory }.Concat(name.Value.Split('/')).ToArray());
        if (!Directory.Exists(directory))
        {
            return null;
        }

        var prefixLength = directory.Length + 1;
        var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Select(fullPath => TypstTemplateFile.FromFile(TypstPath.Parse(fullPath.Substring(prefixLength)), fullPath))
            .ToList();

        var mainFile = TemplateBundleRules.SelectMainFile(name, files.Select(file => file.Path));
        var version = await TemplateHasher.ComputeAsync(files, cancellationToken).ConfigureAwait(false);
        return new TypstTemplate(name, mainFile, version, files);
    }
}
```

`src/Typstium/Templates/EmbeddedResourceTemplateProvider.cs`:

```csharp
using System.Reflection;
using Typstium.Templates;

namespace Typstium;

/// <summary>
/// Templates embedded in an assembly. Embed them with a LogicalName that keeps folder separators:
/// <c>&lt;EmbeddedResource Include="Templates\**" LogicalName="Templates/%(RecursiveDir)%(Filename)%(Extension)" /&gt;</c>
/// and pass "Templates" as the prefix.
/// </summary>
public sealed class EmbeddedResourceTemplateProvider : ITemplateProvider
{
    private readonly Assembly _assembly;
    private readonly string _prefix;

    public EmbeddedResourceTemplateProvider(Assembly assembly, string resourcePrefix)
    {
        _assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
        ArgumentException.ThrowIfNullOrWhiteSpace(resourcePrefix);
        _prefix = Normalize(resourcePrefix).TrimEnd('/') + "/";
    }

    public async ValueTask<TypstTemplate?> GetTemplateAsync(TypstTemplateName name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        var templatePrefix = _prefix + name.Value + "/";
        var files = _assembly.GetManifestResourceNames()
            .Select(resource => (Resource: resource, Normalized: Normalize(resource)))
            .Where(entry => entry.Normalized.StartsWith(templatePrefix, StringComparison.Ordinal))
            .Select(entry => (TypstTemplateFile)new EmbeddedResourceTemplateFile(
                TypstPath.Parse(entry.Normalized.Substring(templatePrefix.Length)), _assembly, entry.Resource))
            .ToList();

        if (files.Count == 0)
        {
            return null;
        }

        var mainFile = TemplateBundleRules.SelectMainFile(name, files.Select(file => file.Path));
        var version = await TemplateHasher.ComputeAsync(files, cancellationToken).ConfigureAwait(false);
        return new TypstTemplate(name, mainFile, version, files);
    }

    // RecursiveDir produces backslashes on Windows.
    private static string Normalize(string resourceName) => resourceName.Replace('\\', '/');

    private sealed class EmbeddedResourceTemplateFile(TypstPath path, Assembly assembly, string resourceName) : TypstTemplateFile(path)
    {
        public override ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken) =>
            new(assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"The embedded resource '{resourceName}' disappeared from {assembly.GetName().Name}."));
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test --project tests/Typstium.UnitTests --treenode-filter "/*/*/DirectoryTemplateProviderTests/*"`
Then: `dotnet test --project tests/Typstium.UnitTests --treenode-filter "/*/*/EmbeddedResourceTemplateProviderTests/*"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src tests
git commit -m "Add directory and embedded-resource template providers

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 20: Integration tests against the real Typst CLI

End-to-end scenarios from spec 7.3, using the public API only. Tests skip with a clear reason when Typst
isn't installed, and fail instead when `TYPSTIUM_REQUIRE_TYPST=1` (CI). This task pins Review Focus 1
(a temp root with a space and `å`) and Review Focus 4 (parallel cold start) end to end.

**Files:**
- Create: `tests/Typstium.Testing/Typst/TypstAvailability.cs`, `tests/Typstium.Testing/Assertions/DocumentAssertions.cs`
- Create (under `tests/Typstium.IntegrationTests/`): `SampleTemplatesTests.cs`, `DiagnosticsTests.cs`, `LimitsTests.cs`, `ConcurrencyTests.cs`, `OutputTests.cs`, `PathsTests.cs`
- Delete: `tests/Typstium.IntegrationTests/SamplesTests.cs` (replaced by `SampleTemplatesTests.cs`)

**Interfaces:**
- Consumes: the public API from Tasks 2–19; `RepositoryPaths`, `TemporaryDirectory` (Tasks 1, 12).
- Produces (test infrastructure):
  - `Typstium.Testing.Typst.TypstAvailability`: `static Task<CliTypstCompiler> CreateCompilerAsync(CliTypstCompilerSettings settings)` (skips or fails), `static bool Required`
  - `Typstium.Testing.Assertions.DocumentAssertions`: `static Task<byte[]> PdfBytesAsync(TypstCompileResult)` (asserts success and the `%PDF-` header), `static Task<int> PageCountAsync(TypstCompileResult)` (asserts a paged result)

- [ ] **Step 1: Add the test infrastructure**

`tests/Typstium.Testing/Typst/TypstAvailability.cs`:

```csharp
namespace Typstium.Testing.Typst;

/// <summary>Finds Typst for integration tests: skip when it's missing, unless TYPSTIUM_REQUIRE_TYPST=1.</summary>
public static class TypstAvailability
{
    private static readonly Lazy<Task<string?>> Located = new(LocateAsync);

    public static bool Required => Environment.GetEnvironmentVariable("TYPSTIUM_REQUIRE_TYPST") == "1";

    public static async Task<CliTypstCompiler> CreateCompilerAsync(CliTypstCompilerSettings settings)
    {
        var path = await Located.Value;
        if (path is null)
        {
            if (Required)
            {
                throw new InvalidOperationException("TYPSTIUM_REQUIRE_TYPST=1 but Typst was not found.");
            }

            Skip.Test("Typst is not installed. Install it (winget install Typst.Typst) or set TYPST_PATH to run integration tests.");
        }

        return new CliTypstCompiler(TypstExecutableResolver.CreateDefault(path!), settings);
    }

    private static async Task<string?> LocateAsync()
    {
        try
        {
            return (await TypstExecutableResolver.CreateDefault().ResolveAsync(CancellationToken.None)).Path;
        }
        catch (TypstExecutableNotFoundException)
        {
            return null;
        }
    }
}
```

`tests/Typstium.Testing/Assertions/DocumentAssertions.cs`:

```csharp
using System.Text;

namespace Typstium.Testing.Assertions;

public static class DocumentAssertions
{
    public static async Task<byte[]> PdfBytesAsync(TypstCompileResult result)
    {
        await Assert.That(result.Status).IsEqualTo(TypstCompileStatus.Succeeded);
        var bytes = ((TypstDocument.SingleFile)result.Document!).Content.ToArray();
        await Assert.That(Encoding.ASCII.GetString(bytes, 0, 5)).IsEqualTo("%PDF-");
        return bytes;
    }

    public static async Task<int> PageCountAsync(TypstCompileResult result)
    {
        await Assert.That(result.Status).IsEqualTo(TypstCompileStatus.Succeeded);
        return ((TypstDocument.Paged)result.Document!).Pages.Length;
    }
}
```

- [ ] **Step 2: Write the sample and diagnostics tests**

Delete `tests/Typstium.IntegrationTests/SamplesTests.cs`.

`tests/Typstium.IntegrationTests/SampleTemplatesTests.cs`:

```csharp
using System.Collections.Immutable;
using Typstium.Testing;
using Typstium.Testing.Assertions;
using Typstium.Testing.Fixtures;
using Typstium.Testing.Typst;

namespace Typstium.IntegrationTests;

public class SampleTemplatesTests
{
    private static readonly DirectoryTemplateProvider Templates = new(Path.Combine(RepositoryPaths.Samples, "templates"));

    private static TypstCompileOptions SampleOptions => TypstCompileOptions.Default with
    {
        Fonts = TypstFontOptions.Default with { FontPaths = ImmutableArray.Create(Path.Combine(RepositoryPaths.Samples, "fonts")) },
    };

    [Test]
    public async Task License_certificate_compiles_to_pdf_and_three_png_pages()
    {
        using var temp = new TemporaryDirectory("it-samples");
        var compiler = await TypstAvailability.CreateCompilerAsync(CliTypstCompilerSettings.Default with { TempRoot = temp.Path });

        var pdf = await compiler.CompileTemplateAsync(Templates, "license", TypstOutput.Pdf(PdfStandard.A2b), SampleOptions);
        var png = await compiler.CompileTemplateAsync(Templates, "license", TypstOutput.Png(72), SampleOptions);

        await DocumentAssertions.PdfBytesAsync(pdf);
        await Assert.That(pdf.Warnings.Count()).IsEqualTo(0);
        await Assert.That(await DocumentAssertions.PageCountAsync(png)).IsEqualTo(3);
        await Assert.That(pdf.Invocation.TypstVersion >= new TypstVersion(0, 15, 0)).IsTrue();
    }

    [Test]
    public async Task Release_notes_render_markdown_through_cmarker()
    {
        using var temp = new TemporaryDirectory("it-samples");
        var compiler = await TypstAvailability.CreateCompilerAsync(CliTypstCompilerSettings.Default with { TempRoot = temp.Path });
        var cache = Path.Combine(Path.GetTempPath(), "typstium-tests", "package-cache");
        var options = SampleOptions with
        {
            Packages = TypstPackageOptions.Default with { NetworkAccess = TypstNetworkAccess.Allowed, PackageCachePath = cache },
        };

        var result = await compiler.CompileTemplateAsync(Templates, "release-notes", TypstOutput.Png(36), options);

        await Assert.That(await DocumentAssertions.PageCountAsync(result)).IsGreaterThan(3);
    }
}
```

The release-notes test downloads `@preview/cmarker:0.1.10` once into a test package cache, so it needs
network access the first time. Vendoring cmarker belongs to the Build and Markdown specs.

`tests/Typstium.IntegrationTests/DiagnosticsTests.cs`:

```csharp
using Typstium.Testing;
using Typstium.Testing.Fakes;
using Typstium.Testing.Fixtures;
using Typstium.Testing.Typst;

namespace Typstium.IntegrationTests;

public class DiagnosticsTests
{
    private static string FixtureDirectory => Path.Combine(RepositoryPaths.Fixtures, "typst-0.15", "diagnostics");

    [Test]
    [Arguments("01-syntax", "TYPST1007")]
    [Arguments("02-unknown-variable", "TYPST1001")]
    [Arguments("03-hint", "TYPST1001")]
    [Arguments("04-outside-root", "TYPST1003")]
    [Arguments("06-error-in-import", "TYPST0001")]
    [Arguments("07-infinite-loop", "TYPST1008")]
    [Arguments("08-recursion", "TYPST1008")]
    [Arguments("09-missing-package", "TYPST1005")]
    [Arguments("10-multiple-errors", "TYPST0001")]
    [Arguments("11-bad-image", "TYPST1006")]
    public async Task In_place_failures_carry_ids_and_full_paths(string name, string expectedId)
    {
        using var temp = new TemporaryDirectory("it-diagnostics");
        var compiler = await TypstAvailability.CreateCompilerAsync(CliTypstCompilerSettings.Default with { TempRoot = temp.Path });

        var result = await compiler.CompileAsync(TypstSource.File(Path.Combine(FixtureDirectory, name + ".typ"), FixtureDirectory));

        await Assert.That(result.Status).IsEqualTo(TypstCompileStatus.Failed);
        var first = result.Errors.First();
        await Assert.That(first.Id.Value).IsEqualTo(expectedId);
        var file = first.Location!.File as TypstSourceFile.Project;
        await Assert.That(file).IsNotNull();
        await Assert.That(File.Exists(file!.FullPath)).IsTrue();
    }

    [Test]
    public async Task Warnings_do_not_fail_the_compile_and_get_the_system_fonts_hint()
    {
        using var temp = new TemporaryDirectory("it-diagnostics");
        var compiler = await TypstAvailability.CreateCompilerAsync(CliTypstCompilerSettings.Default with { TempRoot = temp.Path });

        var result = await compiler.CompileAsync(TypstSource.File(Path.Combine(FixtureDirectory, "05-warning-font.typ")));

        await Assert.That(result.Status).IsEqualTo(TypstCompileStatus.Succeeded);
        var warning = result.Warnings.Single();
        await Assert.That(warning.Id.Value).IsEqualTo("TYPST2001");
        await Assert.That(warning.Hints.Any(hint => hint.Contains("system fonts are disabled"))).IsTrue();
    }

    [Test]
    public async Task Missing_packages_with_a_blocked_network_get_the_network_hint()
    {
        using var temp = new TemporaryDirectory("it-diagnostics");
        var compiler = await TypstAvailability.CreateCompilerAsync(CliTypstCompilerSettings.Default with { TempRoot = temp.Path });
        var options = TypstCompileOptions.Default with
        {
            Packages = TypstPackageOptions.Default with { PackagePath = Path.Combine(temp.Path, "empty-path"), PackageCachePath = Path.Combine(temp.Path, "empty-cache") },
        };

        var result = await compiler.CompileMarkupAsync("#import \"@preview/definitely-not-a-real-package:9.9.9\": *", TypstOutput.Pdf(), options);

        var error = result.Errors.Single();
        await Assert.That(error.Id.Value).IsEqualTo("TYPST1005");
        await Assert.That(error.Hints.Any(hint => hint.Contains("network access is blocked"))).IsTrue();
        await Assert.That(error.Location!.File).IsEqualTo((TypstSourceFile)TypstSourceFile.Markup.Instance);
    }

    [Test]
    public async Task Sandboxed_template_diagnostics_never_mention_the_sandbox()
    {
        using var temp = new TemporaryDirectory("it-diagnostics");
        var compiler = await TypstAvailability.CreateCompilerAsync(CliTypstCompilerSettings.Default with { TempRoot = temp.Path });
        var templates = new InMemoryTemplateProvider().Add(
            "imports",
            "main.typ",
            ("main.typ", "#import \"sub/helper.typ\": greet\n#greet(42)\n"),
            ("sub/helper.typ", "#let greet(name) = {\n  \"Hello \" + name\n}\n"));

        var result = await compiler.CompileTemplateAsync(templates, "imports");

        var error = result.Errors.Single();
        await Assert.That(error.Location!.File).IsEqualTo((TypstSourceFile)new TypstSourceFile.Project(TypstPath.Parse("sub/helper.typ"), null));
        await Assert.That(error.Trace.Single().Location!.File).IsEqualTo((TypstSourceFile)new TypstSourceFile.Project(TypstPath.Parse("main.typ"), null));
        await Assert.That(error.ToString()).DoesNotContain(temp.Path);
        await Assert.That(error.ToString()).DoesNotContain("ws-");
    }
}
```

`09-missing-package` runs with the default options: the network is blocked, so Typst can't reach the
registry and reports "failed to download package" (`TYPST1005`). Two things make the in-place cases
`01`–`11` work: the file source uses `FixtureDirectory` as the root, and `04-outside-root` reads
`../secret.txt`, which Typst refuses whatever is on disk.

- [ ] **Step 3: Write the limits, concurrency, output and path tests**

`tests/Typstium.IntegrationTests/LimitsTests.cs`:

```csharp
using System.Diagnostics;
using System.Runtime.InteropServices;
using Typstium.Testing.Fixtures;
using Typstium.Testing.Typst;

namespace Typstium.IntegrationTests;

public class LimitsTests
{
    private const string NestedLoops = "#let n = 0\n#for i in range(100000) { for j in range(100000) { n += 1 } }\n#n\n";

    private static bool NoWorkspaces(string tempRoot) => Directory.GetDirectories(tempRoot, "ws-*").Length == 0;

    [Test]
    public async Task Heavy_templates_time_out()
    {
        using var temp = new TemporaryDirectory("it-limits");
        var compiler = await TypstAvailability.CreateCompilerAsync(CliTypstCompilerSettings.Default with { TempRoot = temp.Path });
        var stopwatch = Stopwatch.StartNew();

        var result = await compiler.CompileMarkupAsync(NestedLoops, TypstOutput.Pdf(), TypstCompileOptions.Default with { Timeout = TimeSpan.FromSeconds(2) });

        await Assert.That(result.Status).IsEqualTo(TypstCompileStatus.TimedOut);
        await Assert.That(result.Diagnostics.Last().Id.Value).IsEqualTo("TYPST9002");
        await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromSeconds(15));
        await Assert.That(NoWorkspaces(temp.Path)).IsTrue();
    }

    [Test]
    public async Task Huge_output_hits_the_output_limit()
    {
        using var temp = new TemporaryDirectory("it-limits");
        var compiler = await TypstAvailability.CreateCompilerAsync(CliTypstCompilerSettings.Default with { TempRoot = temp.Path });

        var result = await compiler.CompileMarkupAsync(
            "#for i in range(20000) [Page #i #pagebreak()]",
            TypstOutput.Pdf(),
            TypstCompileOptions.Default with { MaxOutputBytes = 1024 * 1024 });

        await Assert.That(result.Status).IsEqualTo(TypstCompileStatus.OutputLimitExceeded);
    }

    [Test]
    public async Task Caller_cancellation_stops_typst_and_cleans_up()
    {
        using var temp = new TemporaryDirectory("it-limits");
        var compiler = await TypstAvailability.CreateCompilerAsync(CliTypstCompilerSettings.Default with { TempRoot = temp.Path });
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var stopwatch = Stopwatch.StartNew();

        await Assert.ThrowsAsync<OperationCanceledException>(() => compiler.CompileMarkupAsync(NestedLoops, cancellation.Token));

        await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromSeconds(10));
        await Assert.That(NoWorkspaces(temp.Path)).IsTrue();
    }

    [Test]
    public async Task Windows_job_object_memory_cap_stops_memory_hungry_templates()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Skip.Test("The memory cap uses Windows job objects.");
        }

        using var temp = new TemporaryDirectory("it-limits");
        var compiler = await TypstAvailability.CreateCompilerAsync(CliTypstCompilerSettings.Default with { TempRoot = temp.Path });
        var options = TypstCompileOptions.Default with { MemoryLimitBytes = 100L * 1024 * 1024, Timeout = TimeSpan.FromSeconds(30) };

        TypstCompileResult? result = null;
        try
        {
            result = await compiler.CompileMarkupAsync("#let a = range(50000000)\n#a.len()", TypstOutput.Pdf(), options);
        }
        catch (TypstProcessCrashedException)
        {
            // Expected: allocation fails under the cap and Typst aborts.
        }

        await Assert.That(result is null || !result.IsSuccess).IsTrue();
    }
}
```

`tests/Typstium.IntegrationTests/ConcurrencyTests.cs`:

```csharp
using Typstium.Testing.Fixtures;
using Typstium.Testing.Typst;

namespace Typstium.IntegrationTests;

public class ConcurrencyTests
{
    [Test]
    public async Task Fifty_parallel_compiles_on_a_cold_compiler()
    {
        using var temp = new TemporaryDirectory("it-parallel");
        var compiler = await TypstAvailability.CreateCompilerAsync(CliTypstCompilerSettings.Default with { TempRoot = temp.Path, MaxConcurrency = 4 });

        var results = await Task.WhenAll(Enumerable.Range(0, 50).Select(i =>
            Task.Run(() => compiler.CompileMarkupAsync($"= Document {i}\n#lorem(50)"))));

        await Assert.That(results.All(result => result.IsSuccess)).IsTrue();
        await Assert.That(Directory.GetDirectories(temp.Path, "ws-*").Length).IsEqualTo(0);
    }
}
```

`tests/Typstium.IntegrationTests/OutputTests.cs`:

```csharp
using Typstium.Testing.Assertions;
using Typstium.Testing.Fixtures;
using Typstium.Testing.Typst;

namespace Typstium.IntegrationTests;

public class OutputTests
{
    [Test]
    public async Task Fixed_creation_timestamp_gives_byte_identical_pdfs()
    {
        using var temp = new TemporaryDirectory("it-output");
        var compiler = await TypstAvailability.CreateCompilerAsync(CliTypstCompilerSettings.Default with { TempRoot = temp.Path });
        var options = TypstCompileOptions.Default with { CreationTimestamp = TypstCreationTimestamp.Fixed(DateTimeOffset.FromUnixTimeSeconds(1700000000)) };

        var first = await DocumentAssertions.PdfBytesAsync(await compiler.CompileMarkupAsync("= Same\n#lorem(30)", TypstOutput.Pdf(), options));
        var second = await DocumentAssertions.PdfBytesAsync(await compiler.CompileMarkupAsync("= Same\n#lorem(30)", TypstOutput.Pdf(), options));

        await Assert.That(second).IsEquivalentTo(first);
    }

    [Test]
    public async Task Multi_page_png_and_svg_come_back_in_page_order()
    {
        using var temp = new TemporaryDirectory("it-output");
        var compiler = await TypstAvailability.CreateCompilerAsync(CliTypstCompilerSettings.Default with { TempRoot = temp.Path });
        const string ThreePages = "= One\n#pagebreak()\n= Two\n#pagebreak()\n= Three";

        var png = await compiler.CompileMarkupAsync(ThreePages, TypstOutput.Png(36));
        var svg = await compiler.CompileMarkupAsync(ThreePages, TypstOutput.Svg(TypstPageSelection.Of(TypstPageRange.From(2))));

        await Assert.That(await DocumentAssertions.PageCountAsync(png)).IsEqualTo(3);
        await Assert.That(await DocumentAssertions.PageCountAsync(svg)).IsEqualTo(2);
    }

    [Test]
    public async Task Json_virtual_files_reach_the_template()
    {
        using var temp = new TemporaryDirectory("it-output");
        var compiler = await TypstAvailability.CreateCompilerAsync(CliTypstCompilerSettings.Default with { TempRoot = temp.Path });

        var result = await compiler.CompileAsync(
            TypstCompileRequest.ForMarkup("#let d = json(\"/data.json\")\n#assert(d.name == \"Åsa\")\nHello #d.name")
                .WithJson("data.json", new { Name = "Åsa" }));

        await DocumentAssertions.PdfBytesAsync(result);
    }
}
```

`tests/Typstium.IntegrationTests/PathsTests.cs` (Review Focus 1):

```csharp
using Typstium.Testing.Fakes;
using Typstium.Testing.Fixtures;
using Typstium.Testing.Typst;

namespace Typstium.IntegrationTests;

public class PathsTests
{
    [Test]
    public async Task Temp_root_with_spaces_and_non_ascii_characters_works_and_maps_diagnostics()
    {
        using var temp = new TemporaryDirectory("it-paths");
        var tempRoot = Path.Combine(temp.Path, "Åsa Ström", "typstium");
        var compiler = await TypstAvailability.CreateCompilerAsync(CliTypstCompilerSettings.Default with { TempRoot = tempRoot });
        var templates = new InMemoryTemplateProvider()
            .Add("ok", "ok.typ", ("ok.typ", "= Fine"))
            .Add("broken", "broken.typ", ("broken.typ", "#nope"));

        var ok = await compiler.CompileTemplateAsync(templates, "ok");
        var broken = await compiler.CompileTemplateAsync(templates, "broken");

        await Assert.That(ok.IsSuccess).IsTrue();
        var error = broken.Errors.Single();
        await Assert.That(error.Location!.File).IsEqualTo((TypstSourceFile)new TypstSourceFile.Project(TypstPath.Parse("broken.typ"), null));
        await Assert.That(error.ToString()).DoesNotContain("Åsa Ström");
    }
}
```

- [ ] **Step 4: Run the integration tests**

Run: `dotnet test --project tests/Typstium.IntegrationTests --framework net10.0`
Expected: PASS with Typst 0.15.x installed.

Then on Windows: `dotnet test --project tests/Typstium.IntegrationTests --framework net472`
Expected: PASS, which exercises the `netstandard2.0` build, the escaper and the job-object termination.

If `Release_notes_render_markdown_through_cmarker` fails without network access, that's expected; rerun
with network.

- [ ] **Step 5: Run the integration tests without Typst to check the skip path**

Temporarily make Typst unfindable (rename the executable, or unset `TYPST_PATH` and remove its folder from
`PATH` in the current shell), then run `dotnet test --project tests/Typstium.IntegrationTests --framework net10.0`.
Expected: the tests are reported as **skipped** with the install hint, not
failed. Then: `TYPSTIUM_REQUIRE_TYPST=1` with Typst hidden. Expected: FAIL with
"TYPSTIUM_REQUIRE_TYPST=1 but Typst was not found". Restore Typst afterwards.

- [ ] **Step 6: Commit**

```bash
git add tests
git commit -m "Add integration tests against the real Typst CLI

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 21: CI, public API tracking and README

**Files:**
- Create: `.github/workflows/ci.yml`
- Create: `src/Typstium/PublicAPI.Shipped.txt`, `src/Typstium/PublicAPI.Unshipped.txt`
- Modify: `src/Typstium/Typstium.csproj` (PublicApiAnalyzers)
- Modify: `README.md`

**Interfaces:**
- Consumes: the finished library and test projects.
- Produces: green CI on Windows and Ubuntu with Typst 0.15.1, an early-warning job on the latest Typst, and a reviewed public API baseline.

- [ ] **Step 1: Turn on public API tracking**

Add to `src/Typstium/Typstium.csproj`:

```xml
  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.PublicApiAnalyzers" PrivateAssets="all" />
    <AdditionalFiles Include="PublicAPI.Shipped.txt" />
    <AdditionalFiles Include="PublicAPI.Unshipped.txt" />
  </ItemGroup>
```

Create both files with exactly one line, `#nullable enable`.

Run: `dotnet build src/Typstium`
Expected: FAILS with many `RS0016: Symbol '...' is not part of the declared public API`.

- [ ] **Step 2: Record the API baseline**

Run: `dotnet format analyzers src/Typstium/Typstium.csproj --diagnostics RS0016 --severity warn`
Then: `dotnet build src/Typstium`
Expected: `Build succeeded`, and `PublicAPI.Unshipped.txt` lists every public symbol (nothing has shipped
yet, so `PublicAPI.Shipped.txt` stays `#nullable enable` only).

If `dotnet format` applies no fixes, copy each symbol from the RS0016 messages into
`PublicAPI.Unshipped.txt`, one per line, and rebuild.

Review the file. It must contain **no** `Typstium.Diagnostics.`, `Typstium.Processes.`,
`Typstium.Workspace.`, `Typstium.CommandLine.`, `Typstium.Capabilities.`, `Typstium.Executable.`,
`Typstium.Compilation.`, `Typstium.Templates.`, `Typstium.Interop.` or `Typstium.Utilities.` entries.
Anything there is an internal type that was made public by mistake; make it `internal` and rebuild.

- [ ] **Step 3: Add the CI workflow**

`.github/workflows/ci.yml`:

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:

permissions:
  contents: read

jobs:
  build:
    name: ${{ matrix.os }} / Typst ${{ matrix.typst }}
    strategy:
      fail-fast: false
      matrix:
        os: [windows-latest, ubuntu-latest]
        typst: ['0.15.1']
    runs-on: ${{ matrix.os }}
    env:
      DOTNET_NOLOGO: 'true'
      TYPSTIUM_REQUIRE_TYPST: '1'
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json
      - uses: typst-community/setup-typst@v5
        with:
          typst-version: ${{ matrix.typst }}
      - run: typst --version
      - run: dotnet build --configuration Release
      - run: dotnet test --configuration Release --no-build

  latest-typst:
    name: Early warning / latest Typst
    runs-on: ubuntu-latest
    continue-on-error: true
    env:
      DOTNET_NOLOGO: 'true'
      TYPSTIUM_REQUIRE_TYPST: '1'
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json
      - uses: typst-community/setup-typst@v5
        with:
          typst-version: latest
      - run: typst --version
      - run: dotnet build --configuration Release
      - run: dotnet test --configuration Release --no-build
```

- [ ] **Step 4: Replace the README**

`README.md`:

````markdown
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
````

- [ ] **Step 5: Verify everything**

Run: `dotnet build --configuration Release`
Expected: `Build succeeded`, 0 warnings.

Run: `dotnet test --configuration Release`
Expected: `Test run summary: Passed!` (with Typst installed, nothing skipped except the platform-specific
tests on the other OS).

- [ ] **Step 6: Commit**

```bash
git add .github README.md src/Typstium
git commit -m "Add CI, public API baseline and README

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

## Spec coverage

| Spec section | Task |
| --- | --- |
| 3 Repository layout, dependencies, Polyfill | 1, 21 |
| 4.1 Compiler, settings, `CreateDefault` | 18 |
| 4.2 Request (split by lifetime) | 8 |
| 4.3 Sources and root rules | 6, 12 |
| 4.4 Virtual files, value objects | 2, 6 |
| 4.5 Output, document, result | 7, 8 |
| 4.6 Builders and extensions | 9 |
| 4.7 Options and presets | 8 |
| 4.8 Command-line customization | 3, 14 |
| 4.9 Template providers | 5, 19 |
| 4.10 Locating the executable | 17 |
| 4.11 Exceptions | 4, 8, 17 |
| 4.12 Diagnostics, IDs, hints, parser | 4, 10, 11 |
| 5.1 Pipeline, orphan cleanup | 12, 18 |
| 5.2 Command line | 14 |
| 5.3 Environment | 14 |
| 5.4 Process runner, containment, interop | 12, 15 |
| 5.5 Outcome mapping | 18 |
| 5.6 Observability | 18 |
| 5.7 Version handling | 13, 16 |
| 6 Security summary | 12, 14, 15, 17, 18 |
| 7 Testing, fixtures per version, CI | 1, 10, 20, 21 |

## Deviations from the spec, decided while planning

1. **Canonical roots** (Task 12): roots are canonicalized before Typst sees them (8.3 names on Windows,
   symlinks on Unix). Without this, diagnostics wouldn't map back on GitHub's Windows runners or macOS.
   `Kernel32.GetLongPathNameW` marshals a string, an exception to the "blittable only" interop rule
   (still Native AOT-compatible).
2. **Job termination on kill** (Task 15): `IProcessContainment.Terminate()` uses `TerminateJobObject`.
   Polyfill's `Kill(entireProcessTree)` on `netstandard2.0` only kills the direct process.
3. **WinGet locator:** WinGet keeps one folder per package, so "newest first" is unnecessary.
   **PATH locator:** only `typst.exe` on Windows, since `.cmd`/`.bat` shims need a shell (spec said
   `PATHEXT`).
4. **`ITypstCompiler.CompileAsync`** gets `CancellationToken cancellationToken = default`, so
   `compiler.CompileAsync(request)` compiles without extension help.
5. **`TYPST1008`** also covers "condition is always true", Typst 0.15's static infinite-loop check
   (spike case 07).
6. **`TypstCompileResult`** has public `CreateSucceeded`/`CreateUnsuccessful` factories and
   `Errors`/`Warnings` helpers, so hosts and tests can build fake compilers.
7. **Implicit usings are on in `src`** (spec section 3 said off). Every source file would otherwise repeat the same
   five `using` lines; the global usings are standard BCL namespaces on both target frameworks.

Record these in the spec's decisions table (D22 onwards) when the plan is executed.
