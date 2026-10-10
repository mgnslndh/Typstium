# Cake.Typstium Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `Cake.Typstium`, a Cake addin whose `TypstCompile` aliases compile plain `.typ` files through the core
`Typstium` API, finding Typst in Cake's tools folder (`#tool`) after `TYPST_PATH`.

**Architecture:** Thin `[CakeMethodAlias]` methods hand off to an internal `TypstCompileRunner`, which maps a mutable
Cake-style `TypstCompileSettings` onto the core's immutable `TypstCompileRequest`, compiles with a per-call
`CliTypstCompiler`, logs diagnostics through `ICakeLog`, throws `CakeException` on failure and saves the document. The
compiler's resolver is the core's locator chain with one addin-owned locator, `CakeToolsLocator`, in the bundled
runtime's place.

**Tech Stack:** C# (latest), `net8.0` addin, Cake.Core 6.0.0 (private), the core `Typstium` package, TUnit on
Microsoft.Testing.Platform, Cake.Testing 6.3.0 fakes, Cake.Tool 6.3.0 and Cake.Sdk 6.3.0 for end-to-end runs.

**Spec:** [docs/specs/cake.md](../specs/cake.md). Read it first; decisions are cited as D1–D18, sections as §n.

## Global Constraints

- Addin target framework: `net8.0` only (D17). Test projects: `net10.0`.
- `Cake.Core` **6.0.0** with `PrivateAssets="all"` in the addin; `Cake.Testing` **6.3.0** in tests only.
- The addin depends on the core through a `ProjectReference` to `src/Typstium/Typstium.csproj`, which packs as a NuGet
  dependency on `Typstium` (D9). Nothing else is bundled.
- Every public type carries the `Typst` prefix; the namespace is `Cake.Typstium`; internals live in
  `Cake.Typstium.Compilation`, `Cake.Typstium.Executable` and `Cake.Typstium.Utilities` (foundation naming rule; helper
  folders are `Utilities`).
- Public API tracked with `Microsoft.CodeAnalysis.PublicApiAnalyzers` (`PublicAPI.Shipped.txt` /
  `PublicAPI.Unshipped.txt`, both starting with `#nullable enable`); XML docs on every public member
  (`GenerateDocumentationFile`, and `TreatWarningsAsErrors` comes from `Directory.Build.props`).
- Overload families have no optional parameters; async shapes come in pairs without and with a required
  `CancellationToken` (foundation D36).
- Settings defaults equal `TypstCompileOptions.Default`: no system fonts, network blocked, 60 s (D15).
- Locator order: `ToolPath` → `TYPST_PATH` → `CakeTools` → `BundledRuntime` → `WinGet` → `WellKnownLocations` →
  `PATH` (D5). The new locator's `Name` is exactly `CakeTools`.
- Failure: `Failed`, `TimedOut`, `OutputLimitExceeded` → `CakeException` after logging every diagnostic; `TypstException`,
  `ArgumentException` and `OperationCanceledException` pass through unwrapped (D10).
- Log every diagnostic with a `"{0}"` format and the text as the argument: Cake's log formats its message, and Typst
  messages contain braces.
- Sync aliases block with `.GetAwaiter().GetResult()` on the async path; nowhere else (D8).
- Markdown/cmarker is out of scope (D2): no cmarker fixtures, docs or tests.
- Commits: small, one task each, message ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- `dotnet test` in this repo needs `--project` (global.json selects Microsoft.Testing.Platform). TUnit filters use
  `--treenode-filter "/*/*/<Class>/*"`.

## Review Focus

1. **Diagnostic text with braces** (`expected "}"`, `{p}` in messages) must reach the log verbatim instead of throwing
   `FormatException` inside Cake's log. Pinned in Task 7.
2. **Relative paths in settings** (`FontPaths`, `Root`, `PackagePath`, `ToolPath`) must resolve against Cake's working
   directory, not the process's current directory, which differ under `--working`/Cake.Sdk's `RunWorkingDirectory`.
   Pinned in Tasks 3 and 6.
3. **Settings that don't apply to the chosen format** (`Ppi` on PDF, `PdfStandards` on PNG) must fail with
   `ArgumentException` naming the setting, not be silently ignored; `Pages` applies to PDF too. Pinned in Task 6.
4. **An output folder that doesn't exist yet** (`artifacts/docs/spec.pdf` on a clean checkout) must be created. Pinned
   in Task 8.
5. **Look-alike tool folders**: `Typstium.Runtime.linux-arm` must not match `Typstium.Runtime.linux-arm64.*`, and folder
   name case must not matter. Pinned in Task 2.

---

## File structure

```
Directory.Packages.props                                   + Cake.Core 6.0.0, Cake.Testing 6.3.0
Typstium.slnx                                              + 4 projects
src/Cake.Typstium/
  Cake.Typstium.csproj
  PublicAPI.Shipped.txt / PublicAPI.Unshipped.txt
  README.md                                                package readme
  TypstAliases.cs                                          [CakeMethodAlias] methods (Task 9)
  TypstCompileSettings.cs                                  public settings (Task 4)
  TypstCompileSettingsExtensions.cs                        fluent With… (Task 4)
  Compilation/TypstOutputPaths.cs                          default output, format inference (Task 5)
  Compilation/TypstCompileRequestMapper.cs                 settings → request and options (Task 6)
  Compilation/TypstDiagnosticLog.cs                        diagnostics → ICakeLog (Task 7)
  Compilation/TypstCompileRunner.cs                        one call: validate, compile, report, save (Task 8)
  Compilation/TypstCakeCompilers.cs                        per-call CliTypstCompiler (Task 9)
  Executable/HostRuntimeIdentifier.cs                      host RID + emulation candidates (Task 1)
  Executable/ToolFolderVersion.cs                          version in a tools folder name (Task 2)
  Executable/CakeToolsLocator.cs                           ITypstExecutableLocator "CakeTools" (Task 2)
  Executable/TypstCakeResolver.cs                          the ordered locator chain (Task 3)
  Utilities/CakePaths.cs                                   Cake path → full native path (Task 3)
tests/Cake.Typstium.UnitTests/                             internals visible, no Typst
tests/Cake.Typstium.IntegrationTests/                      public API, real Typst
tests/Cake.Typstium.EndToEndTests/                         real Cake runs against packed packages
tests/Typstium.Testing/Typst/TypstAvailability.cs          + RequireExecutablePathAsync (Task 10)
build/Build/Tasks/TestTasks.cs                             Cake unit/integration tests in UnitTests/IntegrationTests
build/Build/Tasks/CakeTasks.cs                             CakeEndToEndTests task (Task 11)
build/Build/Program.cs                                     register CakeTasks
.github/workflows/ci.yml                                   run CakeEndToEndTests in the runtime e2e jobs
docs/cake.md                                               user guide (Task 12)
docs/specs/cake.md, docs/design.md                         status updates (Task 12)
```

---

### Task 1: Scaffold the addin and its unit tests, with the host RID

**Files:**
- Modify: `Directory.Packages.props`
- Create: `src/Cake.Typstium/Cake.Typstium.csproj`
- Create: `src/Cake.Typstium/PublicAPI.Shipped.txt`, `src/Cake.Typstium/PublicAPI.Unshipped.txt`
- Create: `src/Cake.Typstium/README.md`
- Create: `src/Cake.Typstium/Executable/HostRuntimeIdentifier.cs`
- Create: `tests/Cake.Typstium.UnitTests/Cake.Typstium.UnitTests.csproj`
- Create: `tests/Cake.Typstium.UnitTests/Executable/HostRuntimeIdentifierTests.cs`
- Modify: `Typstium.slnx`
- Modify: `build/Build/Tasks/TestTasks.cs`

**Interfaces:**
- Produces: `internal static class Cake.Typstium.Executable.HostRuntimeIdentifier` with
  `string Current { get; }`, `string For(bool isWindows, bool isMacOS, Architecture architecture)`,
  `IReadOnlyList<string> Candidates(string runtimeIdentifier)`.

- [ ] **Step 1: Add the package versions**

In `Directory.Packages.props`, after the TUnit lines, add:

```xml
    <!-- Cake.Typstium (docs/specs/cake.md): the addin builds against the first Cake 6 so every Cake 6.x loads it;
         the tests use the fakes of the Cake version the repository's build runs. -->
    <PackageVersion Include="Cake.Core" Version="6.0.0" />
    <PackageVersion Include="Cake.Testing" Version="6.3.0" />
```

- [ ] **Step 2: Create the addin project**

`src/Cake.Typstium/Cake.Typstium.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <RootNamespace>Cake.Typstium</RootNamespace>
    <ImplicitUsings>enable</ImplicitUsings>
    <PackageId>Cake.Typstium</PackageId>
    <Description>Cake addin for Typst: compile .typ documents to PDF, PNG or SVG in Cake scripts, Frosting and Cake.Sdk, over the Typstium API.</Description>
    <PackageTags>cake-addin;cake;typst;pdf;typesetting;documents</PackageTags>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <!-- With TreatWarningsAsErrors, CS1591 makes every public member's XML docs part of the build. -->
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>

  <ItemGroup>
    <!-- Cake provides Cake.Core at run time; the addin must not ship or depend on it (docs/specs/cake.md §3). -->
    <PackageReference Include="Cake.Core" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.PublicApiAnalyzers" PrivateAssets="all" />
    <ProjectReference Include="..\Typstium\Typstium.csproj" />
  </ItemGroup>

  <ItemGroup>
    <AdditionalFiles Include="PublicAPI.Shipped.txt" />
    <AdditionalFiles Include="PublicAPI.Unshipped.txt" />
    <None Include="README.md" Pack="true" PackagePath="/" />
  </ItemGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="Cake.Typstium.UnitTests" />
  </ItemGroup>

</Project>
```

`src/Cake.Typstium/PublicAPI.Shipped.txt` and `src/Cake.Typstium/PublicAPI.Unshipped.txt`, each containing one line:

```
#nullable enable
```

`src/Cake.Typstium/README.md`:

````markdown
# Cake.Typstium

Compile [Typst](https://typst.app) documents in [Cake](https://cakebuild.net) builds, over the Typstium API.

```csharp
#addin nuget:?package=Cake.Typstium&loaddependencies=true
#tool nuget:?package=Typstium.Runtime.linux-x64

Task("Docs").Does(() =>
{
    TypstCompile("docs/spec.typ", "artifacts/spec.pdf");
});
```

`loaddependencies=true` is required: the addin uses the `Typstium` package. Typst comes from a `#tool` runtime package,
`TYPST_PATH`, or an installed Typst. See the user guide in the repository: `docs/cake.md`.
````

- [ ] **Step 3: Create the unit test project and add both projects to the solution**

`tests/Cake.Typstium.UnitTests/Cake.Typstium.UnitTests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="TUnit" />
    <PackageReference Include="Cake.Testing" />
    <ProjectReference Include="..\..\src\Cake.Typstium\Cake.Typstium.csproj" />
    <ProjectReference Include="..\Typstium.Testing\Typstium.Testing.csproj" />
  </ItemGroup>

</Project>
```

In `Typstium.slnx`, add `<Project Path="src/Cake.Typstium/Cake.Typstium.csproj" />` before the `src/Typstium` line in the
`/src/` folder, and `<Project Path="tests/Cake.Typstium.UnitTests/Cake.Typstium.UnitTests.csproj" />` as the first
line of the `/tests/` folder.

- [ ] **Step 4: Write the failing tests**

`tests/Cake.Typstium.UnitTests/Executable/HostRuntimeIdentifierTests.cs`:

```csharp
using System.Runtime.InteropServices;
using Cake.Typstium.Executable;

namespace Cake.Typstium.UnitTests.Executable;

public class HostRuntimeIdentifierTests
{
    [Test]
    [Arguments(true, false, Architecture.X64, "win-x64")]
    [Arguments(true, false, Architecture.Arm64, "win-arm64")]
    [Arguments(false, true, Architecture.Arm64, "osx-arm64")]
    [Arguments(false, true, Architecture.X64, "osx-x64")]
    [Arguments(false, false, Architecture.X64, "linux-x64")]
    [Arguments(false, false, Architecture.Arm, "linux-arm")]
    public async Task Names_the_runtime_package_of_an_os_and_architecture(bool isWindows, bool isMacOS, Architecture architecture, string expected)
    {
        await Assert.That(HostRuntimeIdentifier.For(isWindows, isMacOS, architecture)).IsEqualTo(expected);
    }

    [Test]
    [Arguments("win-arm64", "win-arm64,win-x64")]
    [Arguments("osx-arm64", "osx-arm64,osx-x64")]
    [Arguments("linux-arm64", "linux-arm64")]
    [Arguments("win-x64", "win-x64")]
    public async Task Tries_the_emulated_x64_package_after_the_native_one_on_arm_windows_and_macs(string rid, string expected)
    {
        await Assert.That(string.Join(",", HostRuntimeIdentifier.Candidates(rid))).IsEqualTo(expected);
    }

    [Test]
    public async Task The_current_identifier_starts_with_this_os()
    {
        var os = OperatingSystem.IsWindows() ? "win-" : OperatingSystem.IsMacOS() ? "osx-" : "linux-";
        await Assert.That(HostRuntimeIdentifier.Current).StartsWith(os);
    }
}
```

- [ ] **Step 5: Run the tests to verify they fail**

Run: `dotnet test --project tests/Cake.Typstium.UnitTests`
Expected: build error CS0103/CS0246, `HostRuntimeIdentifier` does not exist.

- [ ] **Step 6: Implement**

`src/Cake.Typstium/Executable/HostRuntimeIdentifier.cs`:

```csharp
using System.Runtime.InteropServices;

namespace Cake.Typstium.Executable;

/// <summary>
/// The runtime identifier of the machine running Cake, and the runtime packages worth trying on it, best first. A copy
/// of the core's RuntimeIdentifierFor and RuntimeIdentifierCandidates, which stay internal there (spec cake.md D7).
/// </summary>
internal static class HostRuntimeIdentifier
{
    public static string Current { get; } =
        For(OperatingSystem.IsWindows(), OperatingSystem.IsMacOS(), RuntimeInformation.OSArchitecture);

    public static string For(bool isWindows, bool isMacOS, Architecture architecture)
    {
        var os = isWindows ? "win" : isMacOS ? "osx" : "linux";
        var name = architecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.Arm => "arm",
            Architecture.X86 => "x86",
            var other => other.ToString().ToLowerInvariant(),
        };
        return os + "-" + name;
    }

    /// <summary>The native RID, then the x64 binary Windows on Arm and Rosetta run under emulation (runtime-packages D11).</summary>
    public static IReadOnlyList<string> Candidates(string runtimeIdentifier) => runtimeIdentifier switch
    {
        "win-arm64" => ["win-arm64", "win-x64"],
        "osx-arm64" => ["osx-arm64", "osx-x64"],
        _ => [runtimeIdentifier],
    };
}
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test --project tests/Cake.Typstium.UnitTests`
Expected: PASS, 11 tests.

- [ ] **Step 8: Run the addin's unit tests in the build's UnitTests task**

In `build/Build/Tasks/TestTasks.cs`, replace the `UnitTests` task's `Does` with:

```csharp
            .Does<BuildParameters>(parameters =>
            {
                RunProductTests("tests/Typstium.UnitTests", parameters);
                RunProductTests("tests/Cake.Typstium.UnitTests", parameters);
            });
```

Run: `./build.ps1 --target=UnitTests` (Windows) or `./build.sh --target=UnitTests`
Expected: the build succeeds and both unit test projects pass.

- [ ] **Step 9: Commit**

```bash
git add Directory.Packages.props Typstium.slnx src/Cake.Typstium tests/Cake.Typstium.UnitTests build/Build/Tasks/TestTasks.cs
git commit -m "Scaffold Cake.Typstium with the host runtime identifier"
```

---

### Task 2: Find Typst in Cake's tools folder

**Files:**
- Create: `src/Cake.Typstium/Executable/ToolFolderVersion.cs`
- Create: `src/Cake.Typstium/Executable/CakeToolsLocator.cs`
- Test: `tests/Cake.Typstium.UnitTests/Executable/ToolFolderVersionTests.cs`
- Test: `tests/Cake.Typstium.UnitTests/Executable/CakeToolsLocatorTests.cs`

**Interfaces:**
- Consumes: `HostRuntimeIdentifier.Candidates` (Task 1) only in later tasks; this task takes candidates as a parameter.
- Produces: `internal sealed class CakeToolsLocator(string toolsDirectory, IReadOnlyList<string> candidateRuntimeIdentifiers) : ITypstExecutableLocator`, `Name == "CakeTools"`.
  `internal sealed class ToolFolderVersion : IComparable<ToolFolderVersion>` with `static ToolFolderVersion? Parse(string folderName, string packageId)`.

- [ ] **Step 1: Write the failing tests**

`tests/Cake.Typstium.UnitTests/Executable/ToolFolderVersionTests.cs`:

```csharp
using Cake.Typstium.Executable;

namespace Cake.Typstium.UnitTests.Executable;

public class ToolFolderVersionTests
{
    private const string Id = "Typstium.Runtime.linux-arm";

    [Test]
    [Arguments("Typstium.Runtime.linux-arm")]
    [Arguments("Typstium.Runtime.linux-arm.0.15.1")]
    [Arguments("typstium.runtime.linux-arm.0.15.1")]
    [Arguments("Typstium.Runtime.linux-arm.0.16.0-beta.1")]
    public async Task Recognizes_the_package_folder_with_or_without_a_version(string folder)
    {
        await Assert.That(ToolFolderVersion.Parse(folder, Id)).IsNotNull();
    }

    [Test]
    [Arguments("Typstium.Runtime.linux-arm64")]
    [Arguments("Typstium.Runtime.linux-arm64.0.15.1")]
    [Arguments("Typstium.Runtime.linux-arm.banana")]
    [Arguments("Typstium.Runtime.All.0.15.1")]
    [Arguments("Typstium.Runtime.linux-arm.")]
    public async Task Rejects_other_packages_and_unparsable_versions(string folder)
    {
        await Assert.That(ToolFolderVersion.Parse(folder, Id)).IsNull();
    }

    [Test]
    public async Task Orders_numerically_with_releases_after_prereleases_and_no_version_first()
    {
        string[] folders = [Id + ".0.15.10", Id, Id + ".0.16.0", Id + ".0.15.1", Id + ".0.16.0-beta.1"];
        var ordered = folders.OrderBy(folder => ToolFolderVersion.Parse(folder, Id)).Select(folder => folder[Id.Length..]);

        await Assert.That(string.Join(" ", ordered)).IsEqualTo(" .0.15.1 .0.15.10 .0.16.0-beta.1 .0.16.0");
    }
}
```

`tests/Cake.Typstium.UnitTests/Executable/CakeToolsLocatorTests.cs`:

```csharp
using Cake.Typstium.Executable;
using Typstium;
using Typstium.Testing.Fixtures;

namespace Cake.Typstium.UnitTests.Executable;

public class CakeToolsLocatorTests
{
    private static string Executable => OperatingSystem.IsWindows() ? "typst.exe" : "typst";

    private static string Install(TemporaryDirectory tools, string folder, string rid) =>
        tools.CreateFile($"{folder}/tools/typst/{rid}/{Executable}", "stub");

    private static async Task<TypstLocatorResult> LocateAsync(TemporaryDirectory tools, params string[] candidates) =>
        await new CakeToolsLocator(tools.Path, candidates).LocateAsync(CancellationToken.None);

    [Test]
    public async Task Is_named_CakeTools()
    {
        await Assert.That(new CakeToolsLocator("tools", ["linux-x64"]).Name).IsEqualTo("CakeTools");
    }

    [Test]
    public async Task Finds_the_binary_of_a_versioned_tool_folder()
    {
        using var tools = new TemporaryDirectory("cake-tools");
        var path = Install(tools, "Typstium.Runtime.linux-x64.0.15.1", "linux-x64");

        await Assert.That(await LocateAsync(tools, "linux-x64")).IsEqualTo(TypstLocatorResult.Found(path));
    }

    [Test]
    public async Task Finds_the_binary_of_an_unversioned_tool_folder()
    {
        using var tools = new TemporaryDirectory("cake-tools");
        var path = Install(tools, "Typstium.Runtime.osx-arm64", "osx-arm64");

        await Assert.That(await LocateAsync(tools, "osx-arm64")).IsEqualTo(TypstLocatorResult.Found(path));
    }

    [Test]
    public async Task Falls_back_to_the_next_candidate()
    {
        using var tools = new TemporaryDirectory("cake-tools");
        var path = Install(tools, "Typstium.Runtime.win-x64.0.15.1", "win-x64");

        await Assert.That(await LocateAsync(tools, "win-arm64", "win-x64")).IsEqualTo(TypstLocatorResult.Found(path));
    }

    [Test]
    public async Task Prefers_the_native_candidate_when_both_are_installed()
    {
        using var tools = new TemporaryDirectory("cake-tools");
        var native = Install(tools, "Typstium.Runtime.win-arm64.0.15.1", "win-arm64");
        Install(tools, "Typstium.Runtime.win-x64.0.15.1", "win-x64");

        await Assert.That(await LocateAsync(tools, "win-arm64", "win-x64")).IsEqualTo(TypstLocatorResult.Found(native));
    }

    [Test]
    public async Task Picks_the_highest_installed_version()
    {
        using var tools = new TemporaryDirectory("cake-tools");
        Install(tools, "Typstium.Runtime.linux-x64", "linux-x64");
        Install(tools, "Typstium.Runtime.linux-x64.0.15.1", "linux-x64");
        var highest = Install(tools, "Typstium.Runtime.linux-x64.0.15.10", "linux-x64");

        await Assert.That(await LocateAsync(tools, "linux-x64")).IsEqualTo(TypstLocatorResult.Found(highest));
    }

    [Test]
    public async Task Ignores_a_package_whose_id_only_starts_with_the_rid()
    {
        using var tools = new TemporaryDirectory("cake-tools");
        Install(tools, "Typstium.Runtime.linux-arm64.0.15.1", "linux-arm64");

        await Assert.That(await LocateAsync(tools, "linux-arm")).IsTypeOf<TypstLocatorResult.NotFoundResult>();
    }

    [Test]
    public async Task Matches_folder_names_in_any_case()
    {
        using var tools = new TemporaryDirectory("cake-tools");
        var path = Install(tools, "typstium.runtime.linux-x64.0.15.1", "linux-x64");

        await Assert.That(await LocateAsync(tools, "linux-x64")).IsEqualTo(TypstLocatorResult.Found(path));
    }

    [Test]
    public async Task Reports_the_patterns_it_searched_when_nothing_is_installed()
    {
        using var tools = new TemporaryDirectory("cake-tools");
        var missing = tools.Combine("tools");

        var result = await new CakeToolsLocator(missing, ["win-arm64", "win-x64"]).LocateAsync(CancellationToken.None);

        var notFound = (TypstLocatorResult.NotFoundResult)result;
        await Assert.That(notFound.SearchedLocations.Length).IsEqualTo(2);
        await Assert.That(notFound.SearchedLocations[0]).Contains("Typstium.Runtime.win-arm64[.<version>]");
        await Assert.That(notFound.SearchedLocations[1]).StartsWith(missing);
    }

    [Test]
    public async Task Sets_the_execute_bit_that_nuget_extraction_drops()
    {
        if (OperatingSystem.IsWindows())
        {
            Skip.Test("Unix file modes only.");
            return;
        }

        using var tools = new TemporaryDirectory("cake-tools");
        var path = Install(tools, "Typstium.Runtime.linux-x64.0.15.1", "linux-x64");
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        await LocateAsync(tools, "linux-x64");

        await Assert.That(File.GetUnixFileMode(path).HasFlag(UnixFileMode.UserExecute)).IsTrue();
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/Cake.Typstium.UnitTests`
Expected: build error, `ToolFolderVersion` and `CakeToolsLocator` do not exist.

- [ ] **Step 3: Implement `ToolFolderVersion`**

`src/Cake.Typstium/Executable/ToolFolderVersion.cs`:

```csharp
namespace Cake.Typstium.Executable;

/// <summary>
/// The version in a folder <c>#tool</c> installed a package into: <c>&lt;id&gt;</c> or <c>&lt;id&gt;.&lt;version&gt;</c>,
/// depending on Cake's UseVersionInToolsFolder. A folder without a version sorts first, a prerelease before its release.
/// </summary>
internal sealed class ToolFolderVersion : IComparable<ToolFolderVersion>
{
    private ToolFolderVersion(Version? number, string prerelease)
    {
        Number = number;
        Prerelease = prerelease;
    }

    /// <summary>Null for a folder without a version.</summary>
    private Version? Number { get; }

    /// <summary>Empty for a release.</summary>
    private string Prerelease { get; }

    /// <summary>The folder's version, or null when the folder belongs to another package or its version can't be read.</summary>
    public static ToolFolderVersion? Parse(string folderName, string packageId)
    {
        if (string.Equals(folderName, packageId, StringComparison.OrdinalIgnoreCase))
        {
            return new ToolFolderVersion(null, "");
        }

        if (!folderName.StartsWith(packageId + ".", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var text = folderName[(packageId.Length + 1)..];
        var dash = text.IndexOf('-');
        var number = dash < 0 ? text : text[..dash];
        var prerelease = dash < 0 ? "" : text[(dash + 1)..];
        return Version.TryParse(number, out var parsed) ? new ToolFolderVersion(parsed, prerelease) : null;
    }

    public int CompareTo(ToolFolderVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        if (Number is null || other.Number is null)
        {
            return (Number is null ? 0 : 1) - (other.Number is null ? 0 : 1);
        }

        var byNumber = Number.CompareTo(other.Number);
        if (byNumber != 0)
        {
            return byNumber;
        }

        if (Prerelease.Length == 0 || other.Prerelease.Length == 0)
        {
            return (Prerelease.Length == 0 ? 1 : 0) - (other.Prerelease.Length == 0 ? 1 : 0);
        }

        return string.CompareOrdinal(Prerelease, other.Prerelease);
    }
}
```

- [ ] **Step 4: Implement `CakeToolsLocator`**

`src/Cake.Typstium/Executable/CakeToolsLocator.cs`:

```csharp
using Typstium;

namespace Cake.Typstium.Executable;

/// <summary>
/// Finds Typst where <c>#tool nuget:?package=Typstium.Runtime.&lt;rid&gt;</c> installs it (spec cake.md §5): in Cake's
/// tools folder, for each candidate RID in order, the highest installed version of that runtime package. Several RIDs
/// can be installed at once, so it never relies on Cake's tool registry, which resolves by file name only (D6).
/// </summary>
internal sealed class CakeToolsLocator(string toolsDirectory, IReadOnlyList<string> candidateRuntimeIdentifiers) : ITypstExecutableLocator
{
    private const string PackagePrefix = "Typstium.Runtime.";

    public string Name => "CakeTools";

    public ValueTask<TypstLocatorResult> LocateAsync(CancellationToken cancellationToken)
    {
        var executable = OperatingSystem.IsWindows() ? "typst.exe" : "typst";
        var searched = new List<string>();
        foreach (var rid in candidateRuntimeIdentifiers)
        {
            var packageId = PackagePrefix + rid;
            var inPackage = Path.Combine("tools", "typst", rid, executable);
            searched.Add(Path.Combine(toolsDirectory, packageId + "[.<version>]", inPackage));
            foreach (var folder in PackageFolders(packageId))
            {
                var candidate = Path.Combine(folder, inPackage);
                if (File.Exists(candidate))
                {
                    EnsureExecutable(candidate);
                    return new ValueTask<TypstLocatorResult>(TypstLocatorResult.Found(candidate));
                }
            }
        }

        return new ValueTask<TypstLocatorResult>(TypstLocatorResult.NotFound(searched));
    }

    /// <summary>The package's folders, highest version first.</summary>
    private IEnumerable<string> PackageFolders(string packageId)
    {
        if (!Directory.Exists(toolsDirectory))
        {
            return [];
        }

        return Directory.EnumerateDirectories(toolsDirectory)
            .Select(path => (Path: path, Version: ToolFolderVersion.Parse(Path.GetFileName(path), packageId)))
            .Where(folder => folder.Version is not null)
            .OrderByDescending(folder => folder.Version)
            .Select(folder => folder.Path)
            .ToArray();
    }

    /// <summary>NuGet extraction drops the execute bit; a read-only tools folder leaves the start failure to report it.</summary>
    private static void EnsureExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            var mode = File.GetUnixFileMode(path);
            if ((mode & UnixFileMode.UserExecute) == 0)
            {
                File.SetUnixFileMode(path, mode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test --project tests/Cake.Typstium.UnitTests`
Expected: PASS (the execute-bit test is skipped on Windows).

- [ ] **Step 6: Commit**

```bash
git add src/Cake.Typstium/Executable tests/Cake.Typstium.UnitTests/Executable
git commit -m "Find Typst in Cake's tools folder for the host runtime"
```

---

### Task 3: The resolver chain and Cake paths

**Files:**
- Create: `src/Cake.Typstium/Utilities/CakePaths.cs`
- Create: `src/Cake.Typstium/Executable/TypstCakeResolver.cs`
- Create: `tests/Cake.Typstium.UnitTests/Testing/CakeFakes.cs`
- Test: `tests/Cake.Typstium.UnitTests/Executable/TypstCakeResolverTests.cs`

**Interfaces:**
- Consumes: `CakeToolsLocator(string, IReadOnlyList<string>)`, `HostRuntimeIdentifier.Current`, `HostRuntimeIdentifier.Candidates` (Tasks 1–2).
- Produces:
  - `internal static class Cake.Typstium.Utilities.CakePaths` with `string Native(FilePath path, ICakeEnvironment environment)` and `string Native(DirectoryPath path, ICakeEnvironment environment)`.
  - `internal static class Cake.Typstium.Executable.TypstCakeResolver` with
    `IReadOnlyList<ITypstExecutableLocator> Locators(ICakeEnvironment environment, ICakeConfiguration configuration, FilePath? toolPath)` and
    `TypstExecutableResolver Create(ICakeEnvironment environment, ICakeConfiguration configuration, FilePath? toolPath)`.
  - Test helper `Cake.Typstium.UnitTests.Testing.CakeFakes.Environment(string workingDirectory) : FakeEnvironment`.

- [ ] **Step 1: Write the test helper and the failing tests**

`tests/Cake.Typstium.UnitTests/Testing/CakeFakes.cs`:

```csharp
using Cake.Core.IO;
using Cake.Testing;

namespace Cake.Typstium.UnitTests.Testing;

internal static class CakeFakes
{
    /// <summary>A fake environment whose working directory is a real folder, different from the process's.</summary>
    public static FakeEnvironment Environment(string workingDirectory)
    {
        var environment = OperatingSystem.IsWindows() ? FakeEnvironment.CreateWindowsEnvironment() : FakeEnvironment.CreateUnixEnvironment();
        environment.WorkingDirectory = new DirectoryPath(workingDirectory);
        return environment;
    }
}
```

`tests/Cake.Typstium.UnitTests/Executable/TypstCakeResolverTests.cs`:

```csharp
using Cake.Core.IO;
using Cake.Testing;
using Cake.Typstium.Executable;
using Cake.Typstium.UnitTests.Testing;
using Typstium;
using Typstium.Testing.Fixtures;

namespace Cake.Typstium.UnitTests.Executable;

public class TypstCakeResolverTests
{
    private static string Executable => OperatingSystem.IsWindows() ? "typst.exe" : "typst";

    [Test]
    public async Task Puts_TYPST_PATH_before_the_cake_tools_folder_and_the_tools_folder_in_the_bundled_runtimes_place()
    {
        using var work = new TemporaryDirectory("cake-resolver");
        var names = TypstCakeResolver.Locators(CakeFakes.Environment(work.Path), new FakeConfiguration(), toolPath: null).Select(l => l.Name);

        await Assert.That(string.Join(",", names)).IsEqualTo("TYPST_PATH,CakeTools,BundledRuntime,WinGet,WellKnownLocations,PATH");
    }

    [Test]
    public async Task Puts_an_explicit_tool_path_first()
    {
        using var work = new TemporaryDirectory("cake-resolver");
        var names = TypstCakeResolver.Locators(CakeFakes.Environment(work.Path), new FakeConfiguration(), new FilePath("typst")).Select(l => l.Name);

        await Assert.That(names.First()).IsEqualTo("ExplicitPath");
    }

    [Test]
    public async Task Resolves_a_relative_tool_path_against_cakes_working_directory()
    {
        using var work = new TemporaryDirectory("cake-resolver");
        var expected = work.CreateFile("bin/" + Executable, "stub");

        var locator = TypstCakeResolver.Locators(CakeFakes.Environment(work.Path), new FakeConfiguration(), new FilePath("bin/" + Executable))[0];

        await Assert.That(await locator.LocateAsync(CancellationToken.None)).IsEqualTo(TypstLocatorResult.Found(expected));
    }

    [Test]
    public async Task Looks_in_the_tools_folder_under_the_working_directory_by_default()
    {
        using var work = new TemporaryDirectory("cake-resolver");
        var rid = HostRuntimeIdentifier.Current;
        var expected = work.CreateFile($"tools/Typstium.Runtime.{rid}.0.15.1/tools/typst/{rid}/{Executable}", "stub");

        var locator = TypstCakeResolver.Locators(CakeFakes.Environment(work.Path), new FakeConfiguration(), toolPath: null)[1];

        await Assert.That(await locator.LocateAsync(CancellationToken.None)).IsEqualTo(TypstLocatorResult.Found(expected));
    }

    [Test]
    public async Task Honors_a_configured_tools_folder()
    {
        using var work = new TemporaryDirectory("cake-resolver");
        var rid = HostRuntimeIdentifier.Current;
        var expected = work.CreateFile($"build-tools/Typstium.Runtime.{rid}/tools/typst/{rid}/{Executable}", "stub");
        var configuration = new FakeConfiguration();
        configuration.SetValue("Paths_Tools", "build-tools");

        var locator = TypstCakeResolver.Locators(CakeFakes.Environment(work.Path), configuration, toolPath: null)[1];

        await Assert.That(await locator.LocateAsync(CancellationToken.None)).IsEqualTo(TypstLocatorResult.Found(expected));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/Cake.Typstium.UnitTests`
Expected: build error, `TypstCakeResolver` does not exist.

- [ ] **Step 3: Implement**

`src/Cake.Typstium/Utilities/CakePaths.cs`:

```csharp
using Cake.Core;
using Cake.Core.IO;

namespace Cake.Typstium.Utilities;

/// <summary>Cake paths as the core expects them: absolute against Cake's working directory, in the OS's own form.</summary>
internal static class CakePaths
{
    public static string Native(FilePath path, ICakeEnvironment environment) =>
        System.IO.Path.GetFullPath(path.MakeAbsolute(environment).FullPath);

    public static string Native(DirectoryPath path, ICakeEnvironment environment) =>
        System.IO.Path.GetFullPath(path.MakeAbsolute(environment).FullPath);
}
```

`src/Cake.Typstium/Executable/TypstCakeResolver.cs`:

```csharp
using Cake.Core;
using Cake.Core.Configuration;
using Cake.Core.IO;
using Cake.Typstium.Utilities;
using Typstium;

namespace Cake.Typstium.Executable;

/// <summary>
/// The core's locator chain with Cake's tools folder in the bundled runtime's place, after TYPST_PATH (spec cake.md §5,
/// D5): ToolPath → TYPST_PATH → CakeTools → BundledRuntime → WinGet → WellKnownLocations → PATH.
/// </summary>
internal static class TypstCakeResolver
{
    public static TypstExecutableResolver Create(ICakeEnvironment environment, ICakeConfiguration configuration, FilePath? toolPath) =>
        new(Locators(environment, configuration, toolPath));

    public static IReadOnlyList<ITypstExecutableLocator> Locators(ICakeEnvironment environment, ICakeConfiguration configuration, FilePath? toolPath)
    {
        var locators = new List<ITypstExecutableLocator>();
        if (toolPath is not null)
        {
            locators.Add(TypstExecutableLocators.ExplicitPath(CakePaths.Native(toolPath, environment)));
        }

        // The folder #tool installs into: Paths_Tools when configured, otherwise ./tools.
        var tools = configuration.GetToolPath(environment.WorkingDirectory, environment);
        locators.Add(TypstExecutableLocators.EnvironmentVariable());
        locators.Add(new CakeToolsLocator(CakePaths.Native(tools, environment), HostRuntimeIdentifier.Candidates(HostRuntimeIdentifier.Current)));
        locators.Add(TypstExecutableLocators.BundledRuntime());
        locators.Add(TypstExecutableLocators.WinGet());
        locators.Add(TypstExecutableLocators.WellKnownLocations());
        locators.Add(TypstExecutableLocators.SearchPath());
        return locators;
    }
}
```

`GetToolPath` is the extension `Cake.Core.CakeConfigurationExtensions.GetToolPath(ICakeConfiguration, DirectoryPath defaultRoot, ICakeEnvironment)`
(namespace `Cake.Core`). If `Honors_a_configured_tools_folder` fails because `GetToolPath` doesn't read
`Paths_Tools` the way the test assumes, read the value directly instead:
`configuration.GetValue("Paths_Tools") is { Length: > 0 } configured ? new DirectoryPath(configured) : environment.WorkingDirectory.Combine("tools")`,
and record the finding against spec §9 item 2 in Task 12.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --project tests/Cake.Typstium.UnitTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Cake.Typstium/Utilities src/Cake.Typstium/Executable/TypstCakeResolver.cs tests/Cake.Typstium.UnitTests
git commit -m "Order the Cake tools folder after TYPST_PATH in the locator chain"
```

---

### Task 4: Settings and their fluent extensions

**Files:**
- Create: `src/Cake.Typstium/TypstCompileSettings.cs`
- Create: `src/Cake.Typstium/TypstCompileSettingsExtensions.cs`
- Modify: `src/Cake.Typstium/PublicAPI.Unshipped.txt`
- Test: `tests/Cake.Typstium.UnitTests/TypstCompileSettingsTests.cs`

**Interfaces:**
- Produces: `public sealed class Cake.Typstium.TypstCompileSettings` with the properties of spec §4.2, and
  `public static class TypstCompileSettingsExtensions` with `WithToolPath(FilePath)`, `WithRoot(DirectoryPath)`,
  `WithFormat(TypstOutputFormat)`, `WithPdfStandard(PdfStandard)`, `WithPpi(int)`, `WithPages(TypstPageSelection)`,
  `WithInput(string key, string value)`, `WithFontPath(DirectoryPath)`, `WithSystemFonts()`,
  `WithPackagePath(DirectoryPath)`, `WithPackageCachePath(DirectoryPath)`, `WithNetwork()`,
  `WithCreationTimestamp(DateTimeOffset)`, `WithTimeout(TimeSpan)`, `WithWarningsAsErrors()`,
  `WithExtraArguments(TypstArguments)`, `WithArgumentCustomization(Func<TypstArguments, TypstArguments>)`; each returns
  the same settings instance.

- [ ] **Step 1: Write the failing tests**

`tests/Cake.Typstium.UnitTests/TypstCompileSettingsTests.cs`:

```csharp
using Cake.Core.IO;
using Typstium;

namespace Cake.Typstium.UnitTests;

public class TypstCompileSettingsTests
{
    [Test]
    public async Task Starts_empty_with_safe_defaults()
    {
        var settings = new TypstCompileSettings();

        await Assert.That(settings.ToolPath).IsNull();
        await Assert.That(settings.Root).IsNull();
        await Assert.That(settings.Format).IsNull();
        await Assert.That(settings.PdfStandards.Count).IsEqualTo(0);
        await Assert.That(settings.Inputs.Count).IsEqualTo(0);
        await Assert.That(settings.FontPaths.Count).IsEqualTo(0);
        await Assert.That(settings.IncludeSystemFonts).IsFalse();
        await Assert.That(settings.AllowNetwork).IsFalse();
        await Assert.That(settings.TreatWarningsAsErrors).IsFalse();
        await Assert.That(settings.ExtraArguments).IsEqualTo(TypstArguments.Empty);
        await Assert.That(settings.ArgumentCustomization).IsNull();
    }

    [Test]
    public async Task Fluent_methods_set_each_setting_and_return_the_same_instance()
    {
        var settings = new TypstCompileSettings();
        Func<TypstArguments, TypstArguments> customize = arguments => arguments;
        var timestamp = new DateTimeOffset(2026, 10, 10, 0, 0, 0, TimeSpan.Zero);

        var returned = settings
            .WithToolPath(new FilePath("bin/typst"))
            .WithRoot(new DirectoryPath("docs"))
            .WithFormat(TypstOutputFormat.Png)
            .WithPdfStandard(PdfStandard.A2b)
            .WithPpi(300)
            .WithPages(TypstPageSelection.Of(TypstPageRange.Single(1)))
            .WithInput("version", "1.2.3")
            .WithFontPath(new DirectoryPath("fonts"))
            .WithSystemFonts()
            .WithPackagePath(new DirectoryPath("packages"))
            .WithPackageCachePath(new DirectoryPath("cache"))
            .WithNetwork()
            .WithCreationTimestamp(timestamp)
            .WithTimeout(TimeSpan.FromMinutes(2))
            .WithWarningsAsErrors()
            .WithExtraArguments(TypstArguments.Empty.Add("--timings"))
            .WithArgumentCustomization(customize);

        await Assert.That(returned).IsSameReferenceAs(settings);
        await Assert.That(settings.ToolPath!.FullPath).IsEqualTo("bin/typst");
        await Assert.That(settings.Root!.FullPath).IsEqualTo("docs");
        await Assert.That(settings.Format).IsEqualTo(TypstOutputFormat.Png);
        await Assert.That(settings.PdfStandards.Single()).IsEqualTo(PdfStandard.A2b);
        await Assert.That(settings.Ppi).IsEqualTo(300);
        await Assert.That(settings.Pages).IsEqualTo(TypstPageSelection.Of(TypstPageRange.Single(1)));
        await Assert.That(settings.Inputs["version"]).IsEqualTo("1.2.3");
        await Assert.That(settings.FontPaths.Single().FullPath).IsEqualTo("fonts");
        await Assert.That(settings.IncludeSystemFonts).IsTrue();
        await Assert.That(settings.PackagePath!.FullPath).IsEqualTo("packages");
        await Assert.That(settings.PackageCachePath!.FullPath).IsEqualTo("cache");
        await Assert.That(settings.AllowNetwork).IsTrue();
        await Assert.That(settings.CreationTimestamp).IsEqualTo(timestamp);
        await Assert.That(settings.Timeout).IsEqualTo(TimeSpan.FromMinutes(2));
        await Assert.That(settings.TreatWarningsAsErrors).IsTrue();
        await Assert.That(settings.ExtraArguments.Count).IsEqualTo(1);
        await Assert.That(settings.ArgumentCustomization).IsSameReferenceAs(customize);
    }

    [Test]
    public async Task WithInput_replaces_an_existing_key()
    {
        var settings = new TypstCompileSettings().WithInput("version", "1").WithInput("version", "2");

        await Assert.That(settings.Inputs.Count).IsEqualTo(1);
        await Assert.That(settings.Inputs["version"]).IsEqualTo("2");
    }

    [Test]
    public async Task Adding_methods_accumulate()
    {
        var settings = new TypstCompileSettings()
            .WithFontPath(new DirectoryPath("a")).WithFontPath(new DirectoryPath("b"))
            .WithPdfStandard(PdfStandard.A2b).WithPdfStandard(PdfStandard.UA1);

        await Assert.That(settings.FontPaths.Count).IsEqualTo(2);
        await Assert.That(settings.PdfStandards.Count).IsEqualTo(2);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/Cake.Typstium.UnitTests`
Expected: build error, `TypstCompileSettings` does not exist.

- [ ] **Step 3: Implement the settings**

`src/Cake.Typstium/TypstCompileSettings.cs`:

```csharp
using Cake.Core.IO;
using Typstium;

namespace Cake.Typstium;

/// <summary>
/// How <see cref="TypstAliases.TypstCompile(Cake.Core.ICakeContext, FilePath, FilePath, TypstCompileSettings)"/>
/// compiles. The defaults equal <see cref="TypstCompileOptions.Default"/>: no system fonts, no network, a 60 second
/// timeout. Relative paths resolve against Cake's working directory.
/// </summary>
public sealed class TypstCompileSettings
{
    /// <summary>The Typst executable to use, ahead of every other place Typst is looked for.</summary>
    public FilePath? ToolPath { get; set; }

    /// <summary>The project root Typst may read files from; null means the input file's folder.</summary>
    public DirectoryPath? Root { get; set; }

    /// <summary>The output format; null infers it from the output file's extension (.pdf, .png, .svg, .html).</summary>
    public TypstOutputFormat? Format { get; set; }

    /// <summary>PDF standards to conform to, such as <see cref="PdfStandard.A2b"/>; PDF output only.</summary>
    public IList<PdfStandard> PdfStandards { get; } = new List<PdfStandard>();

    /// <summary>Pixels per inch for PNG output; null means 144.</summary>
    public int? Ppi { get; set; }

    /// <summary>The pages to export; null means all pages.</summary>
    public TypstPageSelection? Pages { get; set; }

    /// <summary>Values for <c>sys.inputs</c> in the document, passed as <c>--input key=value</c>.</summary>
    public IDictionary<string, string> Inputs { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Folders with fonts for the document.</summary>
    public IList<DirectoryPath> FontPaths { get; } = new List<DirectoryPath>();

    /// <summary>Whether Typst may use the machine's installed fonts; off by default for reproducible output.</summary>
    public bool IncludeSystemFonts { get; set; }

    /// <summary>A folder of vendored Typst packages, laid out as <c>namespace/name/version</c>.</summary>
    public DirectoryPath? PackagePath { get; set; }

    /// <summary>The folder Typst caches downloaded packages in; null means Typst's own.</summary>
    public DirectoryPath? PackageCachePath { get; set; }

    /// <summary>Whether Typst may download packages; off by default.</summary>
    public bool AllowNetwork { get; set; }

    /// <summary>The creation timestamp written into the document; null passes SOURCE_DATE_EPOCH through when set.</summary>
    public DateTimeOffset? CreationTimestamp { get; set; }

    /// <summary>How long one compile may run; null means the core's default of 60 seconds.</summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>Whether warnings fail the task, like MSBuild's TreatWarningsAsErrors.</summary>
    public bool TreatWarningsAsErrors { get; set; }

    /// <summary>Arguments appended after the ones Typstium generates.</summary>
    public TypstArguments ExtraArguments { get; set; } = TypstArguments.Empty;

    /// <summary>Changes the complete generated command line; can undo every guarantee Typstium gives.</summary>
    public Func<TypstArguments, TypstArguments>? ArgumentCustomization { get; set; }
}
```

- [ ] **Step 4: Implement the extensions**

`src/Cake.Typstium/TypstCompileSettingsExtensions.cs`:

```csharp
using Cake.Core.IO;
using Typstium;

namespace Cake.Typstium;

/// <summary>Fluent setters for <see cref="TypstCompileSettings"/>; each returns the same instance.</summary>
public static class TypstCompileSettingsExtensions
{
    /// <summary>Sets <see cref="TypstCompileSettings.ToolPath"/>.</summary>
    public static TypstCompileSettings WithToolPath(this TypstCompileSettings settings, FilePath toolPath) =>
        Set(settings, s => s.ToolPath = toolPath);

    /// <summary>Sets <see cref="TypstCompileSettings.Root"/>.</summary>
    public static TypstCompileSettings WithRoot(this TypstCompileSettings settings, DirectoryPath root) =>
        Set(settings, s => s.Root = root);

    /// <summary>Sets <see cref="TypstCompileSettings.Format"/>.</summary>
    public static TypstCompileSettings WithFormat(this TypstCompileSettings settings, TypstOutputFormat format) =>
        Set(settings, s => s.Format = format);

    /// <summary>Adds a standard to <see cref="TypstCompileSettings.PdfStandards"/>.</summary>
    public static TypstCompileSettings WithPdfStandard(this TypstCompileSettings settings, PdfStandard standard) =>
        Set(settings, s => s.PdfStandards.Add(standard));

    /// <summary>Sets <see cref="TypstCompileSettings.Ppi"/>.</summary>
    public static TypstCompileSettings WithPpi(this TypstCompileSettings settings, int ppi) =>
        Set(settings, s => s.Ppi = ppi);

    /// <summary>Sets <see cref="TypstCompileSettings.Pages"/>.</summary>
    public static TypstCompileSettings WithPages(this TypstCompileSettings settings, TypstPageSelection pages) =>
        Set(settings, s => s.Pages = pages);

    /// <summary>Adds or replaces an entry in <see cref="TypstCompileSettings.Inputs"/>.</summary>
    public static TypstCompileSettings WithInput(this TypstCompileSettings settings, string key, string value) =>
        Set(settings, s => s.Inputs[key] = value);

    /// <summary>Adds a folder to <see cref="TypstCompileSettings.FontPaths"/>.</summary>
    public static TypstCompileSettings WithFontPath(this TypstCompileSettings settings, DirectoryPath fontPath) =>
        Set(settings, s => s.FontPaths.Add(fontPath));

    /// <summary>Sets <see cref="TypstCompileSettings.IncludeSystemFonts"/>.</summary>
    public static TypstCompileSettings WithSystemFonts(this TypstCompileSettings settings) =>
        Set(settings, s => s.IncludeSystemFonts = true);

    /// <summary>Sets <see cref="TypstCompileSettings.PackagePath"/>.</summary>
    public static TypstCompileSettings WithPackagePath(this TypstCompileSettings settings, DirectoryPath packagePath) =>
        Set(settings, s => s.PackagePath = packagePath);

    /// <summary>Sets <see cref="TypstCompileSettings.PackageCachePath"/>.</summary>
    public static TypstCompileSettings WithPackageCachePath(this TypstCompileSettings settings, DirectoryPath packageCachePath) =>
        Set(settings, s => s.PackageCachePath = packageCachePath);

    /// <summary>Sets <see cref="TypstCompileSettings.AllowNetwork"/>.</summary>
    public static TypstCompileSettings WithNetwork(this TypstCompileSettings settings) =>
        Set(settings, s => s.AllowNetwork = true);

    /// <summary>Sets <see cref="TypstCompileSettings.CreationTimestamp"/>.</summary>
    public static TypstCompileSettings WithCreationTimestamp(this TypstCompileSettings settings, DateTimeOffset timestamp) =>
        Set(settings, s => s.CreationTimestamp = timestamp);

    /// <summary>Sets <see cref="TypstCompileSettings.Timeout"/>.</summary>
    public static TypstCompileSettings WithTimeout(this TypstCompileSettings settings, TimeSpan timeout) =>
        Set(settings, s => s.Timeout = timeout);

    /// <summary>Sets <see cref="TypstCompileSettings.TreatWarningsAsErrors"/>.</summary>
    public static TypstCompileSettings WithWarningsAsErrors(this TypstCompileSettings settings) =>
        Set(settings, s => s.TreatWarningsAsErrors = true);

    /// <summary>Sets <see cref="TypstCompileSettings.ExtraArguments"/>.</summary>
    public static TypstCompileSettings WithExtraArguments(this TypstCompileSettings settings, TypstArguments arguments) =>
        Set(settings, s => s.ExtraArguments = arguments);

    /// <summary>Sets <see cref="TypstCompileSettings.ArgumentCustomization"/>.</summary>
    public static TypstCompileSettings WithArgumentCustomization(this TypstCompileSettings settings, Func<TypstArguments, TypstArguments> customization) =>
        Set(settings, s => s.ArgumentCustomization = customization);

    private static TypstCompileSettings Set(TypstCompileSettings settings, Action<TypstCompileSettings> change)
    {
        ArgumentNullException.ThrowIfNull(settings);
        change(settings);
        return settings;
    }
}
```

The `<see cref>` to `TypstAliases.TypstCompile` in `TypstCompileSettings` doesn't resolve until Task 9. Until then,
write it as `<c>TypstCompile</c>` and change it to the `<see cref>` form in Task 9.

- [ ] **Step 5: Record the public API**

Run: `dotnet build src/Cake.Typstium`
Expected: RS0016 errors for every new public member. Apply the analyzer's fix to add them to
`PublicAPI.Unshipped.txt`:

Run: `dotnet format analyzers src/Cake.Typstium/Cake.Typstium.csproj --diagnostics RS0016 --severity info`

Then `dotnet build src/Cake.Typstium` must succeed. If `dotnet format` adds nothing, copy each member from the RS0016
messages into `PublicAPI.Unshipped.txt` by hand, in the analyzer's format (for example
`Cake.Typstium.TypstCompileSettings.Ppi.get -> int?`).

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test --project tests/Cake.Typstium.UnitTests`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/Cake.Typstium tests/Cake.Typstium.UnitTests
git commit -m "Add Typst compile settings with fluent setters"
```

---

### Task 5: Output paths and format inference

**Files:**
- Create: `src/Cake.Typstium/Compilation/TypstOutputPaths.cs`
- Test: `tests/Cake.Typstium.UnitTests/Compilation/TypstOutputPathsTests.cs`

**Interfaces:**
- Produces: `internal static class Cake.Typstium.Compilation.TypstOutputPaths` with
  `FilePath DefaultOutput(FilePath input)` and `TypstOutputFormat FormatOf(FilePath output, TypstOutputFormat? explicitFormat)`
  (throws `ArgumentException` with `ParamName == "output"` for an unknown extension and no explicit format).

- [ ] **Step 1: Write the failing tests**

`tests/Cake.Typstium.UnitTests/Compilation/TypstOutputPathsTests.cs`:

```csharp
using Cake.Core.IO;
using Cake.Typstium.Compilation;
using Typstium;

namespace Cake.Typstium.UnitTests.Compilation;

public class TypstOutputPathsTests
{
    [Test]
    [Arguments("docs/spec.typ", "docs/spec.pdf")]
    [Arguments("docs/spec", "docs/spec.pdf")]
    [Arguments("docs/v1.2/spec.typ", "docs/v1.2/spec.pdf")]
    public async Task The_default_output_is_a_pdf_beside_the_input(string input, string expected)
    {
        await Assert.That(TypstOutputPaths.DefaultOutput(new FilePath(input)).FullPath).IsEqualTo(expected);
    }

    [Test]
    [Arguments("out/spec.pdf", TypstOutputFormat.Pdf)]
    [Arguments("out/spec.PDF", TypstOutputFormat.Pdf)]
    [Arguments("out/page-{0p}.png", TypstOutputFormat.Png)]
    [Arguments("out/page-{p}.svg", TypstOutputFormat.Svg)]
    [Arguments("out/index.html", TypstOutputFormat.Html)]
    public async Task Infers_the_format_from_the_extension(string output, TypstOutputFormat expected)
    {
        await Assert.That(TypstOutputPaths.FormatOf(new FilePath(output), explicitFormat: null)).IsEqualTo(expected);
    }

    [Test]
    public async Task An_explicit_format_wins_over_the_extension()
    {
        await Assert.That(TypstOutputPaths.FormatOf(new FilePath("out/spec.bin"), TypstOutputFormat.Pdf)).IsEqualTo(TypstOutputFormat.Pdf);
    }

    [Test]
    [Arguments("out/spec.docx")]
    [Arguments("out/spec")]
    public async Task An_unknown_extension_without_a_format_names_the_setting(string output)
    {
        var exception = Assert.Throws<ArgumentException>(() => TypstOutputPaths.FormatOf(new FilePath(output), explicitFormat: null));

        await Assert.That(exception!.ParamName).IsEqualTo("output");
        await Assert.That(exception.Message).Contains("TypstCompileSettings.Format");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/Cake.Typstium.UnitTests --treenode-filter "/*/*/TypstOutputPathsTests/*"`
Expected: build error, `TypstOutputPaths` does not exist.

- [ ] **Step 3: Implement**

`src/Cake.Typstium/Compilation/TypstOutputPaths.cs`:

```csharp
using Cake.Core.IO;
using Typstium;

namespace Cake.Typstium.Compilation;

/// <summary>Where the output goes and in which format, as <c>typst compile</c> decides them (spec cake.md §4.3).</summary>
internal static class TypstOutputPaths
{
    /// <summary>The input's path with a .pdf extension, as <c>typst compile in.typ</c> writes <c>in.pdf</c>.</summary>
    public static FilePath DefaultOutput(FilePath input) => input.ChangeExtension(".pdf");

    public static TypstOutputFormat FormatOf(FilePath output, TypstOutputFormat? explicitFormat)
    {
        if (explicitFormat is { } format)
        {
            return format;
        }

        return output.GetExtension()?.ToLowerInvariant() switch
        {
            ".pdf" => TypstOutputFormat.Pdf,
            ".png" => TypstOutputFormat.Png,
            ".svg" => TypstOutputFormat.Svg,
            ".html" => TypstOutputFormat.Html,
            _ => throw new ArgumentException(
                $"Can't tell the output format from '{output.FullPath}'. Use .pdf, .png, .svg or .html, or set TypstCompileSettings.Format.",
                nameof(output)),
        };
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --project tests/Cake.Typstium.UnitTests --treenode-filter "/*/*/TypstOutputPathsTests/*"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Cake.Typstium/Compilation tests/Cake.Typstium.UnitTests/Compilation
git commit -m "Infer the output format and default output path as typst compile does"
```

---

### Task 6: Map settings onto the core request

**Files:**
- Create: `src/Cake.Typstium/Compilation/TypstCompileRequestMapper.cs`
- Test: `tests/Cake.Typstium.UnitTests/Compilation/TypstCompileRequestMapperTests.cs`

**Interfaces:**
- Consumes: `TypstCompileSettings` (Task 4), `CakePaths.Native` (Task 3), `CakeFakes.Environment` (Task 3).
- Produces: `internal static class Cake.Typstium.Compilation.TypstCompileRequestMapper` with
  `TypstCompileRequest Map(string inputPath, TypstOutputFormat format, TypstCompileSettings settings, ICakeEnvironment environment)`.
  `inputPath` is already a full native path. Throws `ArgumentException` (`ParamName == "settings"`) when `Ppi` is set for
  non-PNG output or `PdfStandards` is non-empty for non-PDF output.

- [ ] **Step 1: Write the failing tests**

`tests/Cake.Typstium.UnitTests/Compilation/TypstCompileRequestMapperTests.cs`:

```csharp
using Cake.Core.IO;
using Cake.Typstium.Compilation;
using Cake.Typstium.UnitTests.Testing;
using Typstium;
using Typstium.Testing.Fixtures;

namespace Cake.Typstium.UnitTests.Compilation;

public class TypstCompileRequestMapperTests
{
    private static TypstCompileRequest Map(TemporaryDirectory work, TypstOutputFormat format, TypstCompileSettings settings) =>
        TypstCompileRequestMapper.Map(work.Combine("doc.typ"), format, settings, CakeFakes.Environment(work.Path));

    [Test]
    public async Task Default_settings_give_the_cores_default_options_and_a_plain_pdf()
    {
        using var work = new TemporaryDirectory("cake-map");
        var request = Map(work, TypstOutputFormat.Pdf, new TypstCompileSettings());

        await Assert.That(request.Options).IsEqualTo(TypstCompileOptions.Default);
        await Assert.That(request.Output).IsEqualTo(TypstOutput.Pdf());
        await Assert.That(request.Inputs.Count).IsEqualTo(0);
    }

    [Test]
    public async Task The_source_is_the_input_file_in_its_own_folder_by_default()
    {
        using var work = new TemporaryDirectory("cake-map");
        var request = Map(work, TypstOutputFormat.Pdf, new TypstCompileSettings());

        await Assert.That(request.Source).IsEqualTo(TypstCompileRequest.ForFile(work.Combine("doc.typ")).Build().Source);
    }

    [Test]
    public async Task A_relative_root_resolves_against_cakes_working_directory()
    {
        using var work = new TemporaryDirectory("cake-map");
        var request = Map(work, TypstOutputFormat.Pdf, new TypstCompileSettings { Root = new DirectoryPath("docs") });

        await Assert.That(request.Source).IsEqualTo(TypstCompileRequest.ForFile(work.Combine("doc.typ"), work.Combine("docs")).Build().Source);
    }

    [Test]
    public async Task Fonts_packages_and_limits_map_to_the_options_with_paths_against_the_working_directory()
    {
        using var work = new TemporaryDirectory("cake-map");
        var timestamp = new DateTimeOffset(2026, 10, 10, 0, 0, 0, TimeSpan.Zero);
        Func<TypstArguments, TypstArguments> customize = arguments => arguments;
        var settings = new TypstCompileSettings()
            .WithFontPath(new DirectoryPath("fonts"))
            .WithSystemFonts()
            .WithPackagePath(new DirectoryPath("vendor/packages"))
            .WithPackageCachePath(new DirectoryPath("cache"))
            .WithNetwork()
            .WithCreationTimestamp(timestamp)
            .WithTimeout(TimeSpan.FromMinutes(5))
            .WithExtraArguments(TypstArguments.Empty.Add("--timings"))
            .WithArgumentCustomization(customize);

        var options = Map(work, TypstOutputFormat.Pdf, settings).Options;

        await Assert.That(options.Fonts.FontPaths.Single()).IsEqualTo(work.Combine("fonts"));
        await Assert.That(options.Fonts.IncludeSystemFonts).IsTrue();
        await Assert.That(options.Fonts.IncludeEmbeddedFonts).IsTrue();
        await Assert.That(options.Packages.PackagePath).IsEqualTo(work.Combine("vendor", "packages"));
        await Assert.That(options.Packages.PackageCachePath).IsEqualTo(work.Combine("cache"));
        await Assert.That(options.Packages.NetworkAccess).IsEqualTo(TypstNetworkAccess.Allowed);
        await Assert.That(options.CreationTimestamp).IsEqualTo(TypstCreationTimestamp.Fixed(timestamp));
        await Assert.That(options.Timeout).IsEqualTo(TimeSpan.FromMinutes(5));
        await Assert.That(options.ExtraArguments).IsEqualTo(settings.ExtraArguments);
        await Assert.That(options.ArgumentCustomization).IsSameReferenceAs(customize);
        await Assert.That(options.MaxOutputBytes).IsEqualTo(TypstCompileOptions.Default.MaxOutputBytes);
    }

    [Test]
    public async Task Inputs_become_request_inputs()
    {
        using var work = new TemporaryDirectory("cake-map");
        var request = Map(work, TypstOutputFormat.Pdf, new TypstCompileSettings().WithInput("version", "1.2.3"));

        await Assert.That(request.Inputs["version"]).IsEqualTo("1.2.3");
    }

    [Test]
    public async Task Each_format_maps_its_own_settings()
    {
        using var work = new TemporaryDirectory("cake-map");
        var pages = TypstPageSelection.Of(TypstPageRange.Between(2, 3));

        var pdf = Map(work, TypstOutputFormat.Pdf, new TypstCompileSettings().WithPdfStandard(PdfStandard.A2b).WithPages(pages)).Output;
        var png = Map(work, TypstOutputFormat.Png, new TypstCompileSettings().WithPpi(300).WithPages(pages)).Output;
        var svg = Map(work, TypstOutputFormat.Svg, new TypstCompileSettings().WithPages(pages)).Output;
        var html = Map(work, TypstOutputFormat.Html, new TypstCompileSettings()).Output;

        await Assert.That(pdf).IsEqualTo(TypstOutput.Pdf(new TypstPdfOptions { Standards = [PdfStandard.A2b], Pages = pages }));
        await Assert.That(png).IsEqualTo(TypstOutput.Png(300, pages));
        await Assert.That(svg).IsEqualTo(TypstOutput.Svg(pages));
        await Assert.That(html).IsEqualTo(TypstOutput.Html());
    }

    [Test]
    public async Task Png_defaults_to_144_ppi_and_every_page()
    {
        using var work = new TemporaryDirectory("cake-map");
        var png = Map(work, TypstOutputFormat.Png, new TypstCompileSettings()).Output;

        await Assert.That(png).IsEqualTo(TypstOutput.Png(144, TypstPageSelection.All));
    }

    [Test]
    [Arguments(TypstOutputFormat.Pdf)]
    [Arguments(TypstOutputFormat.Svg)]
    public async Task Ppi_for_anything_but_png_is_refused(TypstOutputFormat format)
    {
        using var work = new TemporaryDirectory("cake-map");
        var exception = Assert.Throws<ArgumentException>(() => Map(work, format, new TypstCompileSettings().WithPpi(300)));

        await Assert.That(exception!.Message).Contains("Ppi");
        await Assert.That(exception.ParamName).IsEqualTo("settings");
    }

    [Test]
    [Arguments(TypstOutputFormat.Png)]
    [Arguments(TypstOutputFormat.Html)]
    public async Task Pdf_standards_for_anything_but_pdf_are_refused(TypstOutputFormat format)
    {
        using var work = new TemporaryDirectory("cake-map");
        var exception = Assert.Throws<ArgumentException>(() => Map(work, format, new TypstCompileSettings().WithPdfStandard(PdfStandard.A2b)));

        await Assert.That(exception!.Message).Contains("PdfStandards");
    }
}
```

`TypstPdfOptions` has `init` properties `Standards` (`ImmutableArray<PdfStandard>`), `Tagged` and `Pages`; check
`src/Typstium/Output/TypstPdfOptions.cs` if the collection-expression initializer doesn't compile, and use
`ImmutableArray.Create(PdfStandard.A2b)` instead.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/Cake.Typstium.UnitTests --treenode-filter "/*/*/TypstCompileRequestMapperTests/*"`
Expected: build error, `TypstCompileRequestMapper` does not exist.

- [ ] **Step 3: Implement**

`src/Cake.Typstium/Compilation/TypstCompileRequestMapper.cs`:

```csharp
using System.Collections.Immutable;
using Cake.Core;
using Cake.Typstium.Utilities;
using Typstium;

namespace Cake.Typstium.Compilation;

/// <summary>Turns the mutable Cake settings into the core's immutable request (spec cake.md §4.2).</summary>
internal static class TypstCompileRequestMapper
{
    public static TypstCompileRequest Map(string inputPath, TypstOutputFormat format, TypstCompileSettings settings, ICakeEnvironment environment)
    {
        var builder = settings.Root is null
            ? TypstCompileRequest.ForFile(inputPath)
            : TypstCompileRequest.ForFile(inputPath, CakePaths.Native(settings.Root, environment));
        foreach (var input in settings.Inputs)
        {
            builder.WithInput(input.Key, input.Value);
        }

        return builder
            .WithOutput(MapOutput(format, settings))
            .WithOptions(MapOptions(settings, environment))
            .Build();
    }

    private static TypstOutput MapOutput(TypstOutputFormat format, TypstCompileSettings settings)
    {
        // A setting that can't apply to the format is a mistake in the script; ignoring it would hide that.
        if (settings.Ppi is not null && format != TypstOutputFormat.Png)
        {
            throw new ArgumentException($"Ppi applies to PNG output only, but the output is {format}.", nameof(settings));
        }

        if (settings.PdfStandards.Count > 0 && format != TypstOutputFormat.Pdf)
        {
            throw new ArgumentException($"PdfStandards apply to PDF output only, but the output is {format}.", nameof(settings));
        }

        var pages = settings.Pages ?? TypstPageSelection.All;
        return format switch
        {
            TypstOutputFormat.Pdf when settings.PdfStandards.Count == 0 && settings.Pages is null => TypstOutput.Pdf(),
            TypstOutputFormat.Pdf => TypstOutput.Pdf(new TypstPdfOptions { Standards = settings.PdfStandards.ToImmutableArray(), Pages = pages }),
            TypstOutputFormat.Png => TypstOutput.Png(settings.Ppi ?? 144, pages),
            TypstOutputFormat.Svg => TypstOutput.Svg(pages),
            TypstOutputFormat.Html => TypstOutput.Html(),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown output format."),
        };
    }

    private static TypstCompileOptions MapOptions(TypstCompileSettings settings, ICakeEnvironment environment)
    {
        var defaults = TypstCompileOptions.Default;
        return defaults with
        {
            Fonts = defaults.Fonts with
            {
                FontPaths = settings.FontPaths.Select(path => CakePaths.Native(path, environment)).ToImmutableArray(),
                IncludeSystemFonts = settings.IncludeSystemFonts,
            },
            Packages = defaults.Packages with
            {
                PackagePath = settings.PackagePath is null ? null : CakePaths.Native(settings.PackagePath, environment),
                PackageCachePath = settings.PackageCachePath is null ? null : CakePaths.Native(settings.PackageCachePath, environment),
                NetworkAccess = settings.AllowNetwork ? TypstNetworkAccess.Allowed : TypstNetworkAccess.Blocked,
            },
            CreationTimestamp = settings.CreationTimestamp is { } timestamp
                ? TypstCreationTimestamp.Fixed(timestamp)
                : TypstCreationTimestamp.Inherit,
            Timeout = settings.Timeout ?? defaults.Timeout,
            ExtraArguments = settings.ExtraArguments,
            ArgumentCustomization = settings.ArgumentCustomization,
        };
    }
}
```

`Default_settings_give_the_cores_default_options` relies on `TypstCompileOptions` record equality: the default
`FontPaths` must compare equal to an empty `ImmutableArray<string>` built here. If that test fails only on
`Fonts`, keep `defaults.Fonts` unchanged when `settings.FontPaths` is empty and `IncludeSystemFonts` is false:
`Fonts = settings.FontPaths.Count == 0 && !settings.IncludeSystemFonts ? defaults.Fonts : defaults.Fonts with { ... }`
(same rule for `Packages` when no package setting is set).

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --project tests/Cake.Typstium.UnitTests --treenode-filter "/*/*/TypstCompileRequestMapperTests/*"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Cake.Typstium/Compilation tests/Cake.Typstium.UnitTests/Compilation
git commit -m "Map Cake settings onto the core compile request"
```

---

### Task 7: Log diagnostics to Cake

**Files:**
- Create: `src/Cake.Typstium/Compilation/TypstDiagnosticLog.cs`
- Create: `tests/Cake.Typstium.UnitTests/Testing/Diagnostics.cs`
- Test: `tests/Cake.Typstium.UnitTests/Compilation/TypstDiagnosticLogTests.cs`

**Interfaces:**
- Produces: `internal static class Cake.Typstium.Compilation.TypstDiagnosticLog` with
  `void Write(ICakeLog log, IEnumerable<TypstDiagnostic> diagnostics)`; test helper
  `Cake.Typstium.UnitTests.Testing.Diagnostics.Error(string message, string file = "doc.typ", int line = 1, int column = 1)`
  and `.Warning(...)` with the same parameters, returning `TypstDiagnostic`.

- [ ] **Step 1: Write the helper and the failing tests**

`tests/Cake.Typstium.UnitTests/Testing/Diagnostics.cs`:

```csharp
using System.Collections.Immutable;
using Typstium;

namespace Cake.Typstium.UnitTests.Testing;

internal static class Diagnostics
{
    public static TypstDiagnostic Error(string message, string file = "doc.typ", int line = 1, int column = 1) =>
        Create("TYPST0001", TypstDiagnosticSeverity.Error, message, file, line, column);

    public static TypstDiagnostic Warning(string message, string file = "doc.typ", int line = 1, int column = 1) =>
        Create("TYPST0002", TypstDiagnosticSeverity.Warning, message, file, line, column);

    private static TypstDiagnostic Create(string id, TypstDiagnosticSeverity severity, string message, string file, int line, int column) =>
        new(TypstDiagnosticId.Parse(id), severity, message,
            new TypstLocation(new TypstSourceFile.Project(TypstPath.Parse(file), FullPath: null), new TypstPosition(line, column), End: null),
            ImmutableArray<string>.Empty, ImmutableArray<TypstTraceFrame>.Empty);
}
```

`tests/Cake.Typstium.UnitTests/Compilation/TypstDiagnosticLogTests.cs`:

```csharp
using System.Collections.Immutable;
using Cake.Core.Diagnostics;
using Cake.Testing;
using Cake.Typstium.Compilation;
using Cake.Typstium.UnitTests.Testing;
using Typstium;

namespace Cake.Typstium.UnitTests.Compilation;

public class TypstDiagnosticLogTests
{
    private static FakeLog Log() => new() { Verbosity = Verbosity.Diagnostic };

    [Test]
    public async Task Errors_and_warnings_are_logged_in_canonical_form_at_their_level()
    {
        var log = Log();
        var error = Diagnostics.Error("unknown variable: x", line: 3, column: 2);
        var warning = Diagnostics.Warning("unknown font family: foo");

        TypstDiagnosticLog.Write(log, [error, warning]);

        await Assert.That(log.Entries[0].Level).IsEqualTo(LogLevel.Error);
        await Assert.That(log.Entries[0].Message).IsEqualTo(error.ToString());
        await Assert.That(log.Entries[1].Level).IsEqualTo(LogLevel.Warning);
        await Assert.That(log.Entries[1].Message).IsEqualTo(warning.ToString());
    }

    [Test]
    public async Task Braces_in_messages_reach_the_log_verbatim()
    {
        var log = Log();

        TypstDiagnosticLog.Write(log, [Diagnostics.Error("expected \"}\" after {p} and {0}")]);

        await Assert.That(log.Entries.Single().Message).Contains("expected \"}\" after {p} and {0}");
    }

    [Test]
    public async Task Hints_and_trace_frames_follow_at_verbose()
    {
        var log = Log();
        var location = new TypstLocation(new TypstSourceFile.Project(TypstPath.Parse("lib.typ"), FullPath: null), new TypstPosition(7, 4), End: null);
        var diagnostic = new TypstDiagnostic(TypstDiagnosticId.Parse("TYPST0001"), TypstDiagnosticSeverity.Error, "cannot add {a} and b", Location: null,
            ImmutableArray.Create("try adding spaces"),
            ImmutableArray.Create(new TypstTraceFrame("while calling `greet`", location), new TypstTraceFrame("in {main}", null)));

        TypstDiagnosticLog.Write(log, [diagnostic]);

        await Assert.That(log.Entries.Count).IsEqualTo(4);
        await Assert.That(log.Entries.Skip(1).All(entry => entry.Verbosity == Verbosity.Verbose)).IsTrue();
        await Assert.That(log.Entries[1].Message).IsEqualTo("    hint: try adding spaces");
        await Assert.That(log.Entries[2].Message).IsEqualTo("    while calling `greet` at /lib.typ(7,4)");
        await Assert.That(log.Entries[3].Message).IsEqualTo("    in {main}");
    }
}
```

The constructor's parameter names come from `src/Typstium/Diagnostics/TypstDiagnostic.cs`
(`id, severity, message, location, hints, trace`); if `Location:` doesn't bind, use `location:`. `DisplayPath` of a
`Project` file without a full path is the root-relative `/lib.typ`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/Cake.Typstium.UnitTests --treenode-filter "/*/*/TypstDiagnosticLogTests/*"`
Expected: build error, `TypstDiagnosticLog` does not exist.

- [ ] **Step 3: Implement**

`src/Cake.Typstium/Compilation/TypstDiagnosticLog.cs`:

```csharp
using Cake.Core.Diagnostics;
using Typstium;

namespace Cake.Typstium.Compilation;

/// <summary>
/// Writes Typst's diagnostics to Cake's log in the canonical <c>path(line,col): error TYPST1001: message</c> form, errors
/// and warnings at their level, hints and call traces underneath at Verbose (spec cake.md §4.3). The text always goes in
/// as a format argument: Cake formats the message, and Typst's messages contain braces.
/// </summary>
internal static class TypstDiagnosticLog
{
    public static void Write(ICakeLog log, IEnumerable<TypstDiagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic.Severity == TypstDiagnosticSeverity.Error)
            {
                log.Error("{0}", diagnostic.ToString());
            }
            else
            {
                log.Warning("{0}", diagnostic.ToString());
            }

            foreach (var hint in diagnostic.Hints)
            {
                log.Verbose("    hint: {0}", hint);
            }

            foreach (var frame in diagnostic.Trace)
            {
                log.Verbose("    {0}", Describe(frame));
            }
        }
    }

    private static string Describe(TypstTraceFrame frame) => frame.Location is { } location
        ? $"{frame.Message} at {location.File.DisplayPath}({location.Start.Line},{location.Start.Column})"
        : frame.Message;
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --project tests/Cake.Typstium.UnitTests --treenode-filter "/*/*/TypstDiagnosticLogTests/*"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Cake.Typstium/Compilation tests/Cake.Typstium.UnitTests
git commit -m "Log Typst diagnostics to Cake in canonical form"
```

---

### Task 8: The compile runner

**Files:**
- Create: `src/Cake.Typstium/Compilation/TypstCompileRunner.cs`
- Create: `tests/Cake.Typstium.UnitTests/Testing/FixedResultCompiler.cs`
- Test: `tests/Cake.Typstium.UnitTests/Compilation/TypstCompileRunnerTests.cs`

**Interfaces:**
- Consumes: `TypstOutputPaths` (Task 5), `TypstCompileRequestMapper.Map` (Task 6), `TypstDiagnosticLog.Write` (Task 7),
  `CakePaths.Native` (Task 3), `Diagnostics` and `CakeFakes` test helpers.
- Produces: `internal sealed class Cake.Typstium.Compilation.TypstCompileRunner(ICakeEnvironment environment, ICakeLog log, Func<FilePath?, ITypstCompiler> compilerFactory)`
  with `Task<TypstCompileResult> RunAsync(FilePath input, FilePath? output, TypstCompileSettings settings, CancellationToken cancellationToken)`.
  `compilerFactory` receives `settings.ToolPath` unchanged (the resolver makes it absolute).

- [ ] **Step 1: Write the fake and the failing tests**

`tests/Cake.Typstium.UnitTests/Testing/FixedResultCompiler.cs`:

```csharp
using System.Collections.Immutable;
using Typstium;
using Typstium.Testing.Fakes;

namespace Cake.Typstium.UnitTests.Testing;

/// <summary>Returns the same result for every request and remembers the last request.</summary>
internal sealed class FixedResultCompiler(TypstCompileResult result) : ITypstCompiler
{
    public TypstCompileRequest? LastRequest { get; private set; }

    public Task<TypstCompileResult> CompileAsync(TypstCompileRequest request, CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        return Task.FromResult(result);
    }

    public static FixedResultCompiler Pdf(params TypstDiagnostic[] diagnostics) =>
        new(TypstCompileResult.CreateSucceeded(TypstDocument.CreateSingleFile(TypstOutputFormat.Pdf, "%PDF-1.7"u8.ToArray()),
            [.. diagnostics], FakeInvocation.Create()));

    public static FixedResultCompiler Pages(TypstOutputFormat format, int count) =>
        new(TypstCompileResult.CreateSucceeded(
            TypstDocument.CreatePaged(format, Enumerable.Range(1, count).Select(page => (ReadOnlyMemory<byte>)new[] { (byte)page }).ToImmutableArray()),
            ImmutableArray<TypstDiagnostic>.Empty, FakeInvocation.Create()));

    public static FixedResultCompiler Unsuccessful(TypstCompileStatus status, params TypstDiagnostic[] diagnostics) =>
        new(TypstCompileResult.CreateUnsuccessful(status, [.. diagnostics], FakeInvocation.Create()));
}
```

`tests/Cake.Typstium.UnitTests/Compilation/TypstCompileRunnerTests.cs`:

```csharp
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Testing;
using Cake.Typstium.Compilation;
using Cake.Typstium.UnitTests.Testing;
using Typstium;
using Typstium.Testing.Fixtures;

namespace Cake.Typstium.UnitTests.Compilation;

public class TypstCompileRunnerTests
{
    private sealed class Fixture : IDisposable
    {
        public TemporaryDirectory Work { get; } = new("cake-runner");
        public FakeLog Log { get; } = new() { Verbosity = Verbosity.Diagnostic };
        public FilePath? ToolPathSeen { get; private set; }
        public bool CompilerCreated { get; private set; }

        public TypstCompileRunner Runner(ITypstCompiler compiler) =>
            new(CakeFakes.Environment(Work.Path), Log, toolPath => { CompilerCreated = true; ToolPathSeen = toolPath; return compiler; });

        public void Dispose() => Work.Dispose();
    }

    [Test]
    public async Task Writes_the_pdf_beside_the_input_by_default_and_logs_one_line()
    {
        using var fixture = new Fixture();
        var compiler = FixedResultCompiler.Pdf();

        var result = await fixture.Runner(compiler).RunAsync(new FilePath("docs/spec.typ"), output: null, new TypstCompileSettings(), CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(File.ReadAllText(fixture.Work.Combine("docs", "spec.pdf"))).IsEqualTo("%PDF-1.7");
        await Assert.That(compiler.LastRequest!.Source).IsEqualTo(TypstCompileRequest.ForFile(fixture.Work.Combine("docs", "spec.typ")).Build().Source);
        var information = fixture.Log.Entries.Single(entry => entry.Level == LogLevel.Information);
        await Assert.That(information.Message).Contains("spec.pdf");
    }

    [Test]
    public async Task Creates_a_missing_output_folder()
    {
        using var fixture = new Fixture();

        await fixture.Runner(FixedResultCompiler.Pdf()).RunAsync(new FilePath("spec.typ"), new FilePath("artifacts/docs/spec.pdf"), new TypstCompileSettings(), CancellationToken.None);

        await Assert.That(File.Exists(fixture.Work.Combine("artifacts", "docs", "spec.pdf"))).IsTrue();
    }

    [Test]
    public async Task Saves_every_page_through_the_pattern()
    {
        using var fixture = new Fixture();

        await fixture.Runner(FixedResultCompiler.Pages(TypstOutputFormat.Png, 3))
            .RunAsync(new FilePath("spec.typ"), new FilePath("out/page-{0p}.png"), new TypstCompileSettings(), CancellationToken.None);

        await Assert.That(File.Exists(fixture.Work.Combine("out", "page-1.png"))).IsTrue();
        await Assert.That(File.Exists(fixture.Work.Combine("out", "page-3.png"))).IsTrue();
    }

    [Test]
    public async Task Several_pages_without_a_pattern_are_refused_before_anything_is_written()
    {
        using var fixture = new Fixture();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            fixture.Runner(FixedResultCompiler.Pages(TypstOutputFormat.Png, 3))
                .RunAsync(new FilePath("spec.typ"), new FilePath("out/page.png"), new TypstCompileSettings(), CancellationToken.None));

        await Assert.That(exception!.Message).Contains("3 pages");
        await Assert.That(Directory.Exists(fixture.Work.Combine("out"))).IsFalse();
    }

    [Test]
    [Arguments(TypstCompileStatus.Failed)]
    [Arguments(TypstCompileStatus.TimedOut)]
    [Arguments(TypstCompileStatus.OutputLimitExceeded)]
    public async Task An_unsuccessful_compile_logs_its_diagnostics_then_throws_a_cake_exception(TypstCompileStatus status)
    {
        using var fixture = new Fixture();
        var error = Diagnostics.Error("unknown variable: x");

        var exception = await Assert.ThrowsAsync<CakeException>(() =>
            fixture.Runner(FixedResultCompiler.Unsuccessful(status, error))
                .RunAsync(new FilePath("spec.typ"), output: null, new TypstCompileSettings(), CancellationToken.None));

        await Assert.That(exception!.Message).Contains("spec.typ");
        await Assert.That(exception.Message).Contains(status.ToString());
        await Assert.That(exception.Message).Contains("1 error");
        await Assert.That(fixture.Log.Entries.Any(entry => entry.Level == LogLevel.Error && entry.Message == error.ToString())).IsTrue();
        await Assert.That(File.Exists(fixture.Work.Combine("spec.pdf"))).IsFalse();
    }

    [Test]
    public async Task Warnings_are_logged_and_pass_by_default()
    {
        using var fixture = new Fixture();
        var warning = Diagnostics.Warning("unknown font family: foo");

        var result = await fixture.Runner(FixedResultCompiler.Pdf(warning)).RunAsync(new FilePath("spec.typ"), output: null, new TypstCompileSettings(), CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(fixture.Log.Entries.Any(entry => entry.Level == LogLevel.Warning && entry.Message == warning.ToString())).IsTrue();
    }

    [Test]
    public async Task Warnings_fail_the_task_when_treated_as_errors()
    {
        using var fixture = new Fixture();

        var exception = await Assert.ThrowsAsync<CakeException>(() =>
            fixture.Runner(FixedResultCompiler.Pdf(Diagnostics.Warning("unknown font family: foo")))
                .RunAsync(new FilePath("spec.typ"), output: null, new TypstCompileSettings().WithWarningsAsErrors(), CancellationToken.None));

        await Assert.That(exception!.Message).Contains("1 warning");
        await Assert.That(File.Exists(fixture.Work.Combine("spec.pdf"))).IsFalse();
    }

    [Test]
    public async Task An_unknown_extension_fails_before_the_compiler_is_created()
    {
        using var fixture = new Fixture();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            fixture.Runner(FixedResultCompiler.Pdf()).RunAsync(new FilePath("spec.typ"), new FilePath("spec.docx"), new TypstCompileSettings(), CancellationToken.None));

        await Assert.That(fixture.CompilerCreated).IsFalse();
    }

    [Test]
    public async Task The_tool_path_setting_reaches_the_compiler_factory()
    {
        using var fixture = new Fixture();

        await fixture.Runner(FixedResultCompiler.Pdf())
            .RunAsync(new FilePath("spec.typ"), output: null, new TypstCompileSettings().WithToolPath(new FilePath("bin/typst")), CancellationToken.None);

        await Assert.That(fixture.ToolPathSeen!.FullPath).IsEqualTo("bin/typst");
    }

    [Test]
    public async Task Core_exceptions_pass_through_unwrapped()
    {
        using var fixture = new Fixture();
        var notFound = new TypstExecutableNotFoundException([]);
        var runner = new TypstCompileRunner(CakeFakes.Environment(fixture.Work.Path), fixture.Log, _ => new ThrowingCompiler(notFound));

        var exception = await Assert.ThrowsAsync<TypstExecutableNotFoundException>(() =>
            runner.RunAsync(new FilePath("spec.typ"), output: null, new TypstCompileSettings(), CancellationToken.None));

        await Assert.That(exception).IsSameReferenceAs(notFound);
    }

    private sealed class ThrowingCompiler(Exception exception) : ITypstCompiler
    {
        public Task<TypstCompileResult> CompileAsync(TypstCompileRequest request, CancellationToken cancellationToken = default) =>
            Task.FromException<TypstCompileResult>(exception);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/Cake.Typstium.UnitTests --treenode-filter "/*/*/TypstCompileRunnerTests/*"`
Expected: build error, `TypstCompileRunner` does not exist.

- [ ] **Step 3: Implement**

`src/Cake.Typstium/Compilation/TypstCompileRunner.cs`:

```csharp
using System.Globalization;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Typstium.Utilities;
using Typstium;

namespace Cake.Typstium.Compilation;

/// <summary>
/// One alias call (spec cake.md §4.3): validate before any I/O, map the settings, compile, log every diagnostic, fail the
/// task the Cake way, save the document. Core exceptions pass through unwrapped (D10).
/// </summary>
internal sealed class TypstCompileRunner(ICakeEnvironment environment, ICakeLog log, Func<FilePath?, ITypstCompiler> compilerFactory)
{
    public async Task<TypstCompileResult> RunAsync(FilePath input, FilePath? output, TypstCompileSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(settings);

        var inputPath = CakePaths.Native(input, environment);
        var outputFile = output ?? TypstOutputPaths.DefaultOutput(input);
        var format = TypstOutputPaths.FormatOf(outputFile, settings.Format);
        var outputPath = CakePaths.Native(outputFile, environment);
        var request = TypstCompileRequestMapper.Map(inputPath, format, settings, environment);

        var result = await compilerFactory(settings.ToolPath).CompileAsync(request, cancellationToken).ConfigureAwait(false);
        TypstDiagnosticLog.Write(log, result.Diagnostics);

        if (!result.IsSuccess)
        {
            throw new CakeException($"Typst failed to compile {inputPath}: {result.Status}, {Count(result.Errors, "error")}.");
        }

        if (settings.TreatWarningsAsErrors && result.Warnings.Any())
        {
            throw new CakeException($"Typst compiled {inputPath} with {Count(result.Warnings, "warning")}, and TreatWarningsAsErrors is set.");
        }

        var pages = await SaveAsync(result.Document!, outputPath, cancellationToken).ConfigureAwait(false);
        log.Information("{0}", pages == 1
            ? $"Typst compiled {inputPath} to {outputPath} in {Milliseconds(result)} ms."
            : $"Typst compiled {inputPath} to {outputPath} ({pages} pages) in {Milliseconds(result)} ms.");
        return result;
    }

    private static async Task<int> SaveAsync(TypstDocument document, string outputPath, CancellationToken cancellationToken)
    {
        if (document is TypstDocument.Paged paged
            && paged.Pages.Length > 1
            && !outputPath.Contains("{p}", StringComparison.Ordinal)
            && !outputPath.Contains("{0p}", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"The document has {paged.Pages.Length} pages, so the output path needs {{p}} or {{0p}}, e.g. page-{{0p}}.png: {outputPath}",
                "output");
        }

        if (Path.GetDirectoryName(outputPath) is { Length: > 0 } folder)
        {
            Directory.CreateDirectory(folder);
        }

        switch (document)
        {
            case TypstDocument.SingleFile single:
                await single.SaveAsync(outputPath, cancellationToken).ConfigureAwait(false);
                return 1;
            case TypstDocument.Paged pagedDocument:
                return (await pagedDocument.SaveAsync(outputPath, cancellationToken).ConfigureAwait(false)).Count;
            default:
                throw new InvalidOperationException($"Unknown document type {document.GetType().Name}.");
        }
    }

    private static string Count(IEnumerable<TypstDiagnostic> diagnostics, string noun)
    {
        var count = diagnostics.Count();
        return count.ToString(CultureInfo.InvariantCulture) + " " + noun + (count == 1 ? "" : "s");
    }

    private static string Milliseconds(TypstCompileResult result) =>
        ((long)result.Invocation.TotalDuration.TotalMilliseconds).ToString(CultureInfo.InvariantCulture);
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --project tests/Cake.Typstium.UnitTests --treenode-filter "/*/*/TypstCompileRunnerTests/*"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Cake.Typstium/Compilation tests/Cake.Typstium.UnitTests
git commit -m "Compile, report and save one document per alias call"
```

---

### Task 9: The aliases

**Files:**
- Create: `src/Cake.Typstium/Compilation/TypstCakeCompilers.cs`
- Create: `src/Cake.Typstium/TypstAliases.cs`
- Modify: `src/Cake.Typstium/TypstCompileSettings.cs` (the `<see cref>` from Task 4)
- Modify: `src/Cake.Typstium/PublicAPI.Unshipped.txt`
- Create: `tests/Cake.Typstium.UnitTests/Testing/TestCakeContext.cs`
- Test: `tests/Cake.Typstium.UnitTests/TypstAliasesTests.cs`

**Interfaces:**
- Consumes: `TypstCakeResolver.Create` (Task 3), `TypstCompileRunner` (Task 8).
- Produces: `public static class Cake.Typstium.TypstAliases` with the signatures of spec §4.1:
  `TypstCompile(ICakeContext, FilePath)`, `TypstCompile(ICakeContext, FilePath, FilePath)`,
  `TypstCompile(ICakeContext, FilePath, FilePath, TypstCompileSettings)`, the six `TypstCompileAsync` overloads (each
  shape without and with a trailing `CancellationToken`), `TypstCompiler(ICakeContext)`,
  `TypstCompiler(ICakeContext, FilePath toolPath)`. `internal static class TypstCakeCompilers` with
  `ITypstCompiler Create(ICakeContext context, FilePath? toolPath)`. Test helper `TestCakeContext(FakeEnvironment environment)`.

- [ ] **Step 1: Write the context helper and the failing tests**

`tests/Cake.Typstium.UnitTests/Testing/TestCakeContext.cs`:

```csharp
using Cake.Core;
using Cake.Core.Configuration;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Core.Tooling;
using Cake.Testing;

namespace Cake.Typstium.UnitTests.Testing;

/// <summary>The members the aliases use (environment, configuration, log); everything else throws.</summary>
internal sealed class TestCakeContext(FakeEnvironment environment) : ICakeContext
{
    public FakeLog FakeLog { get; } = new() { Verbosity = Verbosity.Diagnostic };
    public FakeConfiguration FakeConfiguration { get; } = new();

    public ICakeEnvironment Environment => environment;
    public ICakeLog Log => FakeLog;
    public ICakeConfiguration Configuration => FakeConfiguration;
    public IFileSystem FileSystem => throw new NotSupportedException();
    public IGlobber Globber => throw new NotSupportedException();
    public ICakeArguments Arguments => throw new NotSupportedException();
    public IProcessRunner ProcessRunner => throw new NotSupportedException();
    public IRegistry Registry => throw new NotSupportedException();
    public IToolLocator Tools => throw new NotSupportedException();
    public ICakeDataResolver Data => throw new NotSupportedException();
}
```

`tests/Cake.Typstium.UnitTests/TypstAliasesTests.cs`:

```csharp
using Cake.Core.IO;
using Cake.Typstium.UnitTests.Testing;
using Typstium;
using Typstium.Testing.Fixtures;

namespace Cake.Typstium.UnitTests;

public class TypstAliasesTests
{
    [Test]
    public async Task TypstCompiler_returns_a_cli_compiler()
    {
        using var work = new TemporaryDirectory("cake-aliases");
        var context = new TestCakeContext(CakeFakes.Environment(work.Path));

        await Assert.That(context.TypstCompiler()).IsTypeOf<CliTypstCompiler>();
        await Assert.That(context.TypstCompiler(new FilePath("bin/typst"))).IsTypeOf<CliTypstCompiler>();
    }

    [Test]
    public async Task Null_arguments_are_refused_before_any_work()
    {
        using var work = new TemporaryDirectory("cake-aliases");
        var context = new TestCakeContext(CakeFakes.Environment(work.Path));

        Assert.Throws<ArgumentNullException>(() => TypstAliases.TypstCompile(null!, new FilePath("a.typ")));
        Assert.Throws<ArgumentNullException>(() => context.TypstCompile(null!));
        Assert.Throws<ArgumentNullException>(() => context.TypstCompile(new FilePath("a.typ"), null!));
        Assert.Throws<ArgumentNullException>(() => context.TypstCompile(new FilePath("a.typ"), new FilePath("a.pdf"), null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => context.TypstCompileAsync(null!));
        Assert.Throws<ArgumentNullException>(() => context.TypstCompiler(null!));
    }

    [Test]
    public async Task A_missing_explicit_tool_path_is_reported_by_the_locators()
    {
        using var work = new TemporaryDirectory("cake-aliases");
        work.CreateFile("doc.typ", "= Hi");
        var context = new TestCakeContext(CakeFakes.Environment(work.Path));
        var settings = new TypstCompileSettings().WithToolPath(new FilePath("missing/typst"));

        // Other locators may still find an installed Typst; either the compile succeeds or the not-found error lists
        // the explicit path first. Both prove the setting reached the resolver without the alias wrapping anything.
        try
        {
            await context.TypstCompileAsync(new FilePath("doc.typ"), new FilePath("doc.pdf"), settings);
        }
        catch (TypstExecutableNotFoundException exception)
        {
            await Assert.That(exception.Searches[0].LocatorName).IsEqualTo("ExplicitPath");
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/Cake.Typstium.UnitTests --treenode-filter "/*/*/TypstAliasesTests/*"`
Expected: build error, `TypstAliases` does not exist. If `ICakeContext` in Cake.Core 6.3.0 has members other than the
ten in `TestCakeContext`, the build error names them; add each as `=> throw new NotSupportedException();`.

- [ ] **Step 3: Implement the compiler factory**

`src/Cake.Typstium/Compilation/TypstCakeCompilers.cs`:

```csharp
using Cake.Core;
using Cake.Core.IO;
using Cake.Typstium.Executable;
using Typstium;

namespace Cake.Typstium.Compilation;

/// <summary>A new compiler per call, over the Cake locator chain; no state survives the call (spec cake.md D16).</summary>
internal static class TypstCakeCompilers
{
    public static ITypstCompiler Create(ICakeContext context, FilePath? toolPath) =>
        new CliTypstCompiler(TypstCakeResolver.Create(context.Environment, context.Configuration, toolPath), new CliTypstCompilerSettings());
}
```

- [ ] **Step 4: Implement the aliases**

`src/Cake.Typstium/TypstAliases.cs`:

```csharp
using Cake.Core;
using Cake.Core.Annotations;
using Cake.Core.IO;
using Cake.Typstium.Compilation;
using Typstium;

namespace Cake.Typstium;

/// <summary>
/// Compiles Typst documents. Typst is found through, in order: <see cref="TypstCompileSettings.ToolPath"/>, TYPST_PATH,
/// a <c>#tool nuget:?package=Typstium.Runtime.&lt;rid&gt;</c> in Cake's tools folder, a runtime package referenced by the
/// build project, WinGet, well-known install folders, and PATH.
/// </summary>
[CakeAliasCategory("Typst")]
[CakeNamespaceImport("Typstium")]
public static class TypstAliases
{
    /// <summary>Compiles <paramref name="input"/> to a PDF beside it, as <c>typst compile input.typ</c> does.</summary>
    /// <param name="context">The Cake context.</param>
    /// <param name="input">The .typ file.</param>
    /// <returns>The result; a failed compile throws <see cref="CakeException"/> instead.</returns>
    [CakeMethodAlias]
    public static TypstCompileResult TypstCompile(this ICakeContext context, FilePath input) =>
        TypstCompileAsync(context, input).GetAwaiter().GetResult();

    /// <summary>Compiles <paramref name="input"/> to <paramref name="output"/>; the extension picks the format.</summary>
    /// <param name="context">The Cake context.</param>
    /// <param name="input">The .typ file.</param>
    /// <param name="output">The output file, or a page pattern with {p} or {0p} for PNG and SVG.</param>
    /// <returns>The result; a failed compile throws <see cref="CakeException"/> instead.</returns>
    [CakeMethodAlias]
    public static TypstCompileResult TypstCompile(this ICakeContext context, FilePath input, FilePath output) =>
        TypstCompileAsync(context, input, output).GetAwaiter().GetResult();

    /// <summary>Compiles <paramref name="input"/> to <paramref name="output"/> with <paramref name="settings"/>.</summary>
    /// <param name="context">The Cake context.</param>
    /// <param name="input">The .typ file.</param>
    /// <param name="output">The output file, or a page pattern with {p} or {0p} for PNG and SVG.</param>
    /// <param name="settings">Fonts, inputs, format and limits.</param>
    /// <returns>The result; a failed compile throws <see cref="CakeException"/> instead.</returns>
    [CakeMethodAlias]
    public static TypstCompileResult TypstCompile(this ICakeContext context, FilePath input, FilePath output, TypstCompileSettings settings) =>
        TypstCompileAsync(context, input, output, settings).GetAwaiter().GetResult();

    /// <summary>Compiles <paramref name="input"/> to a PDF beside it.</summary>
    /// <param name="context">The Cake context.</param>
    /// <param name="input">The .typ file.</param>
    /// <returns>The result; a failed compile throws <see cref="CakeException"/> instead.</returns>
    [CakeMethodAlias]
    public static Task<TypstCompileResult> TypstCompileAsync(this ICakeContext context, FilePath input) =>
        Run(context, input, output: null, new TypstCompileSettings(), CancellationToken.None);

    /// <summary>Compiles <paramref name="input"/> to a PDF beside it.</summary>
    /// <param name="context">The Cake context.</param>
    /// <param name="input">The .typ file.</param>
    /// <param name="cancellationToken">Stops the compile and kills Typst.</param>
    /// <returns>The result; a failed compile throws <see cref="CakeException"/> instead.</returns>
    [CakeMethodAlias]
    public static Task<TypstCompileResult> TypstCompileAsync(this ICakeContext context, FilePath input, CancellationToken cancellationToken) =>
        Run(context, input, output: null, new TypstCompileSettings(), cancellationToken);

    /// <summary>Compiles <paramref name="input"/> to <paramref name="output"/>; the extension picks the format.</summary>
    /// <param name="context">The Cake context.</param>
    /// <param name="input">The .typ file.</param>
    /// <param name="output">The output file, or a page pattern with {p} or {0p} for PNG and SVG.</param>
    /// <returns>The result; a failed compile throws <see cref="CakeException"/> instead.</returns>
    [CakeMethodAlias]
    public static Task<TypstCompileResult> TypstCompileAsync(this ICakeContext context, FilePath input, FilePath output) =>
        Run(context, input, output ?? throw new ArgumentNullException(nameof(output)), new TypstCompileSettings(), CancellationToken.None);

    /// <summary>Compiles <paramref name="input"/> to <paramref name="output"/>; the extension picks the format.</summary>
    /// <param name="context">The Cake context.</param>
    /// <param name="input">The .typ file.</param>
    /// <param name="output">The output file, or a page pattern with {p} or {0p} for PNG and SVG.</param>
    /// <param name="cancellationToken">Stops the compile and kills Typst.</param>
    /// <returns>The result; a failed compile throws <see cref="CakeException"/> instead.</returns>
    [CakeMethodAlias]
    public static Task<TypstCompileResult> TypstCompileAsync(this ICakeContext context, FilePath input, FilePath output, CancellationToken cancellationToken) =>
        Run(context, input, output ?? throw new ArgumentNullException(nameof(output)), new TypstCompileSettings(), cancellationToken);

    /// <summary>Compiles <paramref name="input"/> to <paramref name="output"/> with <paramref name="settings"/>.</summary>
    /// <param name="context">The Cake context.</param>
    /// <param name="input">The .typ file.</param>
    /// <param name="output">The output file, or a page pattern with {p} or {0p} for PNG and SVG.</param>
    /// <param name="settings">Fonts, inputs, format and limits.</param>
    /// <returns>The result; a failed compile throws <see cref="CakeException"/> instead.</returns>
    [CakeMethodAlias]
    public static Task<TypstCompileResult> TypstCompileAsync(this ICakeContext context, FilePath input, FilePath output, TypstCompileSettings settings) =>
        Run(context, input, output ?? throw new ArgumentNullException(nameof(output)), settings, CancellationToken.None);

    /// <summary>Compiles <paramref name="input"/> to <paramref name="output"/> with <paramref name="settings"/>.</summary>
    /// <param name="context">The Cake context.</param>
    /// <param name="input">The .typ file.</param>
    /// <param name="output">The output file, or a page pattern with {p} or {0p} for PNG and SVG.</param>
    /// <param name="settings">Fonts, inputs, format and limits.</param>
    /// <param name="cancellationToken">Stops the compile and kills Typst.</param>
    /// <returns>The result; a failed compile throws <see cref="CakeException"/> instead.</returns>
    [CakeMethodAlias]
    public static Task<TypstCompileResult> TypstCompileAsync(this ICakeContext context, FilePath input, FilePath output, TypstCompileSettings settings, CancellationToken cancellationToken) =>
        Run(context, input, output ?? throw new ArgumentNullException(nameof(output)), settings, cancellationToken);

    /// <summary>
    /// A compiler over the same search for Typst, for what the TypstCompile aliases don't cover: markup and template
    /// sources, virtual files, pages in memory, and batches that share one compiler's concurrency limit.
    /// </summary>
    /// <param name="context">The Cake context.</param>
    /// <returns>A new compiler; keep it for a batch.</returns>
    [CakeMethodAlias]
    public static ITypstCompiler TypstCompiler(this ICakeContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return TypstCakeCompilers.Create(context, toolPath: null);
    }

    /// <summary>A compiler that tries <paramref name="toolPath"/> first.</summary>
    /// <param name="context">The Cake context.</param>
    /// <param name="toolPath">The Typst executable; relative paths resolve against Cake's working directory.</param>
    /// <returns>A new compiler; keep it for a batch.</returns>
    [CakeMethodAlias]
    public static ITypstCompiler TypstCompiler(this ICakeContext context, FilePath toolPath)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(toolPath);
        return TypstCakeCompilers.Create(context, toolPath);
    }

    private static Task<TypstCompileResult> Run(ICakeContext context, FilePath input, FilePath? output, TypstCompileSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(settings);
        var runner = new TypstCompileRunner(context.Environment, context.Log, toolPath => TypstCakeCompilers.Create(context, toolPath));
        return runner.RunAsync(input, output, settings, cancellationToken);
    }
}
```

`Run` validates synchronously, so the null checks throw from the alias call itself rather than from the returned task;
that is what `Null_arguments_are_refused_before_any_work` asserts for the sync aliases. For
`TypstCompileAsync(null!)` the test uses `ThrowsAsync`, which accepts a synchronous throw too.

In `src/Cake.Typstium/TypstCompileSettings.cs`, change the class summary's `<c>TypstCompile</c>` (Task 4) to
`<see cref="TypstAliases.TypstCompile(Cake.Core.ICakeContext, FilePath, FilePath, TypstCompileSettings)"/>`.

If `[CakeNamespaceImport]` doesn't allow `AttributeTargets.Class` (CS0592), put it on each alias method instead.

- [ ] **Step 5: Record the public API**

Run: `dotnet format analyzers src/Cake.Typstium/Cake.Typstium.csproj --diagnostics RS0016 --severity info`
Then `dotnet build src/Cake.Typstium` must succeed with no RS0016/RS0026/RS0027 errors. RS0026/RS0027 would mean an
overload with an optional parameter slipped in; remove the default value rather than suppressing the rule.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test --project tests/Cake.Typstium.UnitTests`
Expected: PASS, all unit tests.

- [ ] **Step 7: Commit**

```bash
git add src/Cake.Typstium tests/Cake.Typstium.UnitTests
git commit -m "Add the TypstCompile and TypstCompiler aliases"
```

---

### Task 10: Integration tests with real Typst

**Files:**
- Modify: `tests/Typstium.Testing/Typst/TypstAvailability.cs`
- Create: `tests/Cake.Typstium.IntegrationTests/Cake.Typstium.IntegrationTests.csproj`
- Create: `tests/Cake.Typstium.IntegrationTests/TestCakeContext.cs`
- Create: `tests/Cake.Typstium.IntegrationTests/Documents/two-pages.typ`
- Create: `tests/Cake.Typstium.IntegrationTests/Documents/needs-input.typ`
- Create: `tests/Cake.Typstium.IntegrationTests/Documents/broken.typ`
- Test: `tests/Cake.Typstium.IntegrationTests/TypstCompileAliasTests.cs`
- Modify: `Typstium.slnx`, `build/Build/Tasks/TestTasks.cs`

**Interfaces:**
- Consumes: the public aliases and settings (Tasks 4, 9).
- Produces: `TypstAvailability.RequireExecutablePathAsync() : Task<string>` (skips, or fails under
  `TYPSTIUM_REQUIRE_TYPST=1`, when Typst is missing).

- [ ] **Step 1: Expose Typst's path to tests**

In `tests/Typstium.Testing/Typst/TypstAvailability.cs`, add below `CreateCompilerAsync`:

```csharp
    /// <summary>The full path of the Typst found for integration tests; skips (or fails when required) without one.</summary>
    public static async Task<string> RequireExecutablePathAsync()
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

        return path!;
    }
```

- [ ] **Step 2: Create the project, documents and context**

`tests/Cake.Typstium.IntegrationTests/Cake.Typstium.IntegrationTests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="TUnit" />
    <PackageReference Include="Cake.Testing" />
    <ProjectReference Include="..\..\src\Cake.Typstium\Cake.Typstium.csproj" />
    <ProjectReference Include="..\Typstium.Testing\Typstium.Testing.csproj" />
  </ItemGroup>

  <ItemGroup>
    <None Include="Documents\**" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>

</Project>
```

Add `<Project Path="tests/Cake.Typstium.IntegrationTests/Cake.Typstium.IntegrationTests.csproj" />` to the `/tests/`
folder of `Typstium.slnx`, before the unit test project.

`tests/Cake.Typstium.IntegrationTests/TestCakeContext.cs`: the same class as Task 9's `TestCakeContext`, in namespace
`Cake.Typstium.IntegrationTests` (copied, not shared: the unit test project sees internals and this one must not), plus
a factory:

```csharp
using Cake.Core;
using Cake.Core.Configuration;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Core.Tooling;
using Cake.Testing;

namespace Cake.Typstium.IntegrationTests;

/// <summary>The members the aliases use (environment, configuration, log); everything else throws.</summary>
internal sealed class TestCakeContext(FakeEnvironment environment) : ICakeContext
{
    public static TestCakeContext In(string workingDirectory)
    {
        var environment = OperatingSystem.IsWindows() ? FakeEnvironment.CreateWindowsEnvironment() : FakeEnvironment.CreateUnixEnvironment();
        environment.WorkingDirectory = new DirectoryPath(workingDirectory);
        return new TestCakeContext(environment);
    }

    public FakeLog FakeLog { get; } = new() { Verbosity = Verbosity.Diagnostic };
    public FakeConfiguration FakeConfiguration { get; } = new();

    public ICakeEnvironment Environment => environment;
    public ICakeLog Log => FakeLog;
    public ICakeConfiguration Configuration => FakeConfiguration;
    public IFileSystem FileSystem => throw new NotSupportedException();
    public IGlobber Globber => throw new NotSupportedException();
    public ICakeArguments Arguments => throw new NotSupportedException();
    public IProcessRunner ProcessRunner => throw new NotSupportedException();
    public IRegistry Registry => throw new NotSupportedException();
    public IToolLocator Tools => throw new NotSupportedException();
    public ICakeDataResolver Data => throw new NotSupportedException();
}
```

`tests/Cake.Typstium.IntegrationTests/Documents/two-pages.typ`:

```typ
#set page(width: 10cm, height: 8cm)
#set text(font: "Source Sans 3")

= Release notes

First page.

#pagebreak()

Second page.
```

`tests/Cake.Typstium.IntegrationTests/Documents/needs-input.typ`:

```typ
#assert.eq(sys.inputs.at("version", default: none), "1.2.3", message: "the version input did not arrive")
Version #sys.inputs.version
```

`tests/Cake.Typstium.IntegrationTests/Documents/broken.typ`:

```typ
= Broken

#let x = (1, 2
```

- [ ] **Step 3: Write the tests**

`tests/Cake.Typstium.IntegrationTests/TypstCompileAliasTests.cs`:

```csharp
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Typstium;
using Typstium.Testing;
using Typstium.Testing.Fixtures;
using Typstium.Testing.Typst;

namespace Cake.Typstium.IntegrationTests;

public class TypstCompileAliasTests
{
    private static readonly DirectoryPath Fonts = new(Path.Combine(RepositoryPaths.Samples, "fonts"));

    private static async Task<(TemporaryDirectory Work, TestCakeContext Context, TypstCompileSettings Settings)> ArrangeAsync(string document)
    {
        var typst = await TypstAvailability.RequireExecutablePathAsync();
        var work = new TemporaryDirectory("cake-it");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Documents", document), work.Combine(document));
        var settings = new TypstCompileSettings().WithToolPath(new FilePath(typst)).WithFontPath(Fonts);
        return (work, TestCakeContext.In(work.Path), settings);
    }

    [Test]
    public async Task Compiles_a_pdf_with_a_bundled_font_and_no_warnings()
    {
        var (work, context, settings) = await ArrangeAsync("two-pages.typ");
        using var _ = work;

        var result = context.TypstCompile(new FilePath("two-pages.typ"), new FilePath("out/two-pages.pdf"), settings);

        await Assert.That(result.Warnings.Any()).IsFalse();
        await Assert.That(File.ReadAllBytes(work.Combine("out", "two-pages.pdf"))[..4]).IsEquivalentTo("%PDF"u8.ToArray());
        await Assert.That(result.Invocation.ExecutableFoundBy).IsEqualTo("ExplicitPath");
    }

    [Test]
    public async Task Compiles_every_page_to_png_through_the_pattern()
    {
        var (work, context, settings) = await ArrangeAsync("two-pages.typ");
        using var _ = work;

        await context.TypstCompileAsync(new FilePath("two-pages.typ"), new FilePath("pages/page-{0p}.png"), settings);

        await Assert.That(File.Exists(work.Combine("pages", "page-1.png"))).IsTrue();
        await Assert.That(File.Exists(work.Combine("pages", "page-2.png"))).IsTrue();
    }

    [Test]
    public async Task Compiles_svg_pages()
    {
        var (work, context, settings) = await ArrangeAsync("two-pages.typ");
        using var _ = work;

        context.TypstCompile(new FilePath("two-pages.typ"), new FilePath("svg/{p}.svg"), settings);

        await Assert.That(File.ReadAllText(work.Combine("svg", "2.svg"))).Contains("<svg");
    }

    [Test]
    public async Task Inputs_reach_sys_inputs()
    {
        var (work, context, settings) = await ArrangeAsync("needs-input.typ");
        using var _ = work;

        var result = context.TypstCompile(new FilePath("needs-input.typ"), new FilePath("needs-input.pdf"), settings.WithInput("version", "1.2.3"));

        await Assert.That(result.IsSuccess).IsTrue();
    }

    [Test]
    public async Task A_syntax_error_fails_with_the_canonical_line_in_the_log()
    {
        var (work, context, settings) = await ArrangeAsync("broken.typ");
        using var _ = work;

        var exception = Assert.Throws<CakeException>(() => context.TypstCompile(new FilePath("broken.typ"), new FilePath("broken.pdf"), settings));

        await Assert.That(exception!.Message).Contains("Failed");
        var errors = context.FakeLog.Entries.Where(entry => entry.Level == LogLevel.Error).Select(entry => entry.Message).ToArray();
        await Assert.That(errors.Any(message => message.Contains("broken.typ(") && message.Contains("error TYPST"))).IsTrue();
        await Assert.That(File.Exists(work.Combine("broken.pdf"))).IsFalse();
    }

    [Test]
    public async Task A_fixed_creation_timestamp_gives_byte_identical_pdfs()
    {
        var (work, context, settings) = await ArrangeAsync("two-pages.typ");
        using var _ = work;
        settings.WithCreationTimestamp(new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));

        context.TypstCompile(new FilePath("two-pages.typ"), new FilePath("a.pdf"), settings);
        context.TypstCompile(new FilePath("two-pages.typ"), new FilePath("b.pdf"), settings);

        await Assert.That(File.ReadAllBytes(work.Combine("a.pdf"))).IsEquivalentTo(File.ReadAllBytes(work.Combine("b.pdf")));
    }
}
```

- [ ] **Step 4: Run the tests**

Run: `dotnet test --project tests/Cake.Typstium.IntegrationTests`
Expected: PASS with a local Typst 0.15.1 (`winget install Typst.Typst`), or every test skipped with the install hint
when none is found. A failure means an earlier task's mapping is wrong; fix it there, not here.

- [ ] **Step 5: Run them in the build's IntegrationTests task**

In `build/Build/Tasks/TestTasks.cs`, replace the `IntegrationTests` task's `Does` with:

```csharp
            .Does<BuildParameters>(parameters =>
            {
                RunProductTests("tests/Typstium.IntegrationTests", parameters);
                RunProductTests("tests/Cake.Typstium.IntegrationTests", parameters);
            });
```

Run: `./build.ps1` (or `./build.sh`)
Expected: the full default build passes: build tests, build, unit tests, integration tests, timing tests.

- [ ] **Step 6: Commit**

```bash
git add tests/Typstium.Testing tests/Cake.Typstium.IntegrationTests Typstium.slnx build/Build/Tasks/TestTasks.cs
git commit -m "Compile real documents through the Cake aliases"
```

---

### Task 11: End-to-end tests with real Cake

**Files:**
- Create: `tests/Cake.Typstium.EndToEndTests/Cake.Typstium.EndToEndTests.csproj`
- Create: `tests/Cake.Typstium.EndToEndTests/AssemblyInfo.cs`
- Create: `tests/Cake.Typstium.EndToEndTests/CakeSession.cs`
- Create: `tests/Cake.Typstium.EndToEndTests/CakeEndToEndTests.cs`
- Create: `tests/Cake.Typstium.UnitTests/ScenarioCatalogTests.cs`
- Create: `build/Build/Tasks/CakeTasks.cs`
- Modify: `build/Build/Program.cs`, `Typstium.slnx`, `.github/workflows/ci.yml`

**Interfaces:**
- Consumes: `Typstium.Runtime.Testing.DotNet.RunAsync/RunToolAsync`, `DotNetRun(ExitCode, Output)`,
  `PackageFeed.ArtifactsRoot`, `Scenario.Key`, `Typstium.Testing.RepositoryPaths`, `TemporaryDirectory`.
- Produces: build target `CakeEndToEndTests`; tests tagged `[Property(Scenario.Key, "CK1")]` … `"CK6"`.

- [ ] **Step 1: Write the scenario catalog check (fails until the scenarios are tagged)**

`tests/Cake.Typstium.UnitTests/ScenarioCatalogTests.cs`:

```csharp
using System.Text.RegularExpressions;
using Typstium.Testing;

namespace Cake.Typstium.UnitTests;

public partial class ScenarioCatalogTests
{
    // §7's rows: | CKn | scenario | expected | verified by |
    [GeneratedRegex(@"^\| (CK\d+) \|", RegexOptions.Multiline)]
    private static partial Regex Row();

    [GeneratedRegex(@"\[Property\((?:Scenario\.Key|""Scenario""), ""(CK\d+)""\)\]")]
    private static partial Regex Tag();

    [Test]
    public async Task Every_cake_scenario_has_a_tagged_end_to_end_test()
    {
        var spec = File.ReadAllText(Path.Combine(RepositoryPaths.Root, "docs", "specs", "cake.md"));
        var needed = Row().Matches(spec).Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        var tests = Path.Combine(RepositoryPaths.Root, "tests", "Cake.Typstium.EndToEndTests");
        var tagged = Directory.Exists(tests)
            ? Directory.EnumerateFiles(tests, "*.cs", SearchOption.AllDirectories)
                .SelectMany(file => Tag().Matches(File.ReadAllText(file)).Select(match => match.Groups[1].Value))
                .ToHashSet(StringComparer.Ordinal)
            : [];

        await Assert.That(needed.Except(tagged).Order(StringComparer.Ordinal).ToArray()).IsEmpty();
        await Assert.That(needed.Count).IsEqualTo(6);
    }
}
```

Run: `dotnet test --project tests/Cake.Typstium.UnitTests --treenode-filter "/*/*/ScenarioCatalogTests/*"`
Expected: FAIL, CK1–CK6 untagged.

- [ ] **Step 2: Create the project**

`tests/Cake.Typstium.EndToEndTests/Cake.Typstium.EndToEndTests.csproj`:

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

`tests/Cake.Typstium.EndToEndTests/AssemblyInfo.cs`:

```csharp
using TUnit.Core.Interfaces;

[assembly: ParallelLimiter<Cake.Typstium.EndToEndTests.TwoAtATime>]

namespace Cake.Typstium.EndToEndTests;

// Each test installs Cake.Tool or restores a Cake.Sdk project and copies a 20 MB runtime package.
public sealed class TwoAtATime : IParallelLimit
{
    public int Limit => 2;
}
```

Add `<Project Path="tests/Cake.Typstium.EndToEndTests/Cake.Typstium.EndToEndTests.csproj" />` to `Typstium.slnx`.

- [ ] **Step 3: Write the session**

`tests/Cake.Typstium.EndToEndTests/CakeSession.cs`:

```csharp
using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Typstium.Runtime.Testing;
using Typstium.Testing;
using Typstium.Testing.Fixtures;

namespace Cake.Typstium.EndToEndTests;

/// <summary>
/// Typstium and Cake.Typstium packed once per session with a unique version, next to the real runtime packages in
/// $TYPSTIUM_RUNTIME_PACKAGES, and a fresh NuGet cache (spec cake.md §8).
/// </summary>
internal static class CakeSession
{
    private static readonly Lazy<Task<Session>> Current = new(CreateAsync);

    private static bool Required => Environment.GetEnvironmentVariable("TYPSTIUM_REQUIRE_RUNTIME_PACKAGES") == "1";

    public static string HostRid { get; } =
        (OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux") + "-" + RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();

    public static Task<Session> GetAsync() => Current.Value;

    private static async Task<Session> CreateAsync()
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
        var version = "0.0.0-c" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
        var feed = Path.Combine(PackageFeed.ArtifactsRoot, "cake-" + version);
        var environment = new Dictionary<string, string?>
        {
            ["NUGET_PACKAGES"] = Path.Combine(PackageFeed.ArtifactsRoot, "cake-cache-" + version),
            // Every scenario starts without TYPST_PATH; CK5 sets it on purpose.
            ["TYPST_PATH"] = null,
        };
        foreach (var project in new[] { Path.Combine("src", "Typstium", "Typstium.csproj"), Path.Combine("src", "Cake.Typstium", "Cake.Typstium.csproj") })
        {
            var pack = await DotNet.RunAsync(RepositoryPaths.Root, environment, "pack", project, "-c", "Release", "-o", feed, "-p:Version=" + version, "-nodeReuse:false");
            if (pack.ExitCode != 0)
            {
                throw new InvalidOperationException($"dotnet pack {project} failed:\n{pack.Output}");
            }
        }

        return new Session(feed, version, packages!, runtimeVersion, environment);
    }

    /// <summary>Extracts the host's typst from its runtime package, for CK5's TYPST_PATH.</summary>
    public static string ExtractHostTypst(Session session, TemporaryDirectory into)
    {
        var package = Path.Combine(session.RuntimePackages, $"Typstium.Runtime.{HostRid}.{session.RuntimeVersion}.nupkg");
        var name = OperatingSystem.IsWindows() ? "typst.exe" : "typst";
        var target = into.Combine("typst-path", name);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        using (var zip = ZipFile.OpenRead(package))
        {
            zip.GetEntry($"tools/typst/{HostRid}/{name}")!.ExtractToFile(target);
        }

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(target, File.GetUnixFileMode(target) | UnixFileMode.UserExecute);
        }

        return target;
    }
}

internal sealed record Session(string Feed, string Version, string RuntimePackages, string RuntimeVersion, IReadOnlyDictionary<string, string?> Environment)
{
    /// <summary>A nuget.config with the session feed, the runtime packages and nuget.org (for Cake.Tool and Cake.Sdk).</summary>
    public string NuGetConfig => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <configuration>
          <packageSources>
            <clear />
            <add key="typstium" value="{Feed}" />
            <add key="runtimes" value="{RuntimePackages}" />
            <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
          </packageSources>
        </configuration>
        """;
}
```

- [ ] **Step 4: Write the scenarios**

`tests/Cake.Typstium.EndToEndTests/CakeEndToEndTests.cs`:

```csharp
using Typstium.Runtime.Testing;
using Typstium.Testing;
using Typstium.Testing.Fixtures;

namespace Cake.Typstium.EndToEndTests;

public class CakeEndToEndTests
{
    private const string Document = "= Hello from Cake\n\nCompiled by Cake.Typstium.\n";
    private const string BrokenDocument = "= Broken\n\n#let x = (1, 2\n";

    private static string OtherRid => CakeSession.HostRid.StartsWith("win", StringComparison.Ordinal) ? "linux-x64" : "win-x64";

    /// <summary>A folder with nuget.config, global.json, doc.typ and a local Cake.Tool 6.3.0.</summary>
    private static async Task<TemporaryDirectory> ScriptFolderAsync(Session session, string document = Document)
    {
        var folder = new TemporaryDirectory("cake-e2e");
        folder.CreateFile("nuget.config", session.NuGetConfig);
        folder.CreateFile("doc.typ", document);
        File.Copy(Path.Combine(RepositoryPaths.Root, "global.json"), folder.Combine("global.json"));
        await Expect(await DotNet.RunAsync(folder.Path, session.Environment, "new", "tool-manifest"), "dotnet new tool-manifest");
        await Expect(await DotNet.RunAsync(folder.Path, session.Environment, "tool", "install", "Cake.Tool", "--version", "6.3.0"), "dotnet tool install Cake.Tool");
        return folder;
    }

    private static string Script(Session session, bool loadDependencies, params string[] rids) =>
        $"#addin nuget:?package=Cake.Typstium&version={session.Version}{(loadDependencies ? "&loaddependencies=true" : "")}\n" +
        string.Concat(rids.Select(rid => $"#tool nuget:?package=Typstium.Runtime.{rid}&version={session.RuntimeVersion}\n")) +
        """
        Task("Default").Does(() =>
        {
            var result = TypstCompile("doc.typ", "out/doc.pdf");
            Information("found-by={0}", result.Invocation.ExecutableFoundBy);
            Information("path={0}", result.Invocation.ExecutablePath);
        });
        RunTarget("Default");
        """;

    private static Task<DotNetRun> RunCakeAsync(Session session, TemporaryDirectory folder, IReadOnlyDictionary<string, string?>? extra = null)
    {
        var environment = new Dictionary<string, string?>(session.Environment);
        foreach (var (name, value) in extra ?? new Dictionary<string, string?>())
        {
            environment[name] = value;
        }

        return DotNet.RunAsync(folder.Path, environment, "cake", "build.cake");
    }

    private static async Task Expect(DotNetRun run, string what)
    {
        if (run.ExitCode != 0)
        {
            throw new InvalidOperationException($"{what} failed:\n{run.Output}");
        }

        await Task.CompletedTask;
    }

    [Test]
    [Property(Scenario.Key, "CK1")]
    public async Task A_script_compiles_with_typst_from_its_tool_directive()
    {
        var session = await CakeSession.GetAsync();
        using var folder = await ScriptFolderAsync(session);
        folder.CreateFile("build.cake", Script(session, loadDependencies: true, CakeSession.HostRid));

        var run = await RunCakeAsync(session, folder);

        await Expect(run, "dotnet cake");
        await Assert.That(run.Output).Contains("found-by=CakeTools");
        await Assert.That(File.ReadAllBytes(folder.Combine("out", "doc.pdf"))[..4]).IsEquivalentTo("%PDF"u8.ToArray());
    }

    [Test]
    [Property(Scenario.Key, "CK2")]
    public async Task A_script_with_several_tool_directives_picks_the_hosts_binary()
    {
        var session = await CakeSession.GetAsync();
        using var folder = await ScriptFolderAsync(session);
        folder.CreateFile("build.cake", Script(session, loadDependencies: true, OtherRid, CakeSession.HostRid));

        var run = await RunCakeAsync(session, folder);

        await Expect(run, "dotnet cake");
        await Assert.That(run.Output).Contains("found-by=CakeTools");
        await Assert.That(run.Output).Contains($"{Path.DirectorySeparatorChar}typst{Path.DirectorySeparatorChar}{CakeSession.HostRid}{Path.DirectorySeparatorChar}");
    }

    [Test]
    [Property(Scenario.Key, "CK3")]
    public async Task A_script_without_loaddependencies_fails_naming_the_typstium_assembly()
    {
        var session = await CakeSession.GetAsync();
        using var folder = await ScriptFolderAsync(session);
        folder.CreateFile("build.cake", Script(session, loadDependencies: false, CakeSession.HostRid));

        var run = await RunCakeAsync(session, folder);

        await Assert.That(run.ExitCode).IsNotEqualTo(0);
        await Assert.That(run.Output).Contains("Typstium");
        await Assert.That(run.Output).DoesNotContain("found-by=");
    }

    [Test]
    [Property(Scenario.Key, "CK4")]
    public async Task A_cake_sdk_project_finds_typst_through_a_runtime_package_reference()
    {
        var session = await CakeSession.GetAsync();
        using var folder = new TemporaryDirectory("cake-e2e");
        folder.CreateFile("nuget.config", session.NuGetConfig);
        folder.CreateFile("doc.typ", Document);
        File.Copy(Path.Combine(RepositoryPaths.Root, "global.json"), folder.Combine("global.json"));
        folder.CreateFile("Build.csproj", $"""
            <Project Sdk="Cake.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Cake.Typstium" Version="{session.Version}" />
                <PackageReference Include="Typstium.Runtime.{CakeSession.HostRid}" Version="{session.RuntimeVersion}" />
              </ItemGroup>
            </Project>
            """);
        folder.CreateFile("Program.cs", """
            using Typstium;

            Task("Default").Does(() =>
            {
                var result = TypstCompile("doc.typ", "out/doc.pdf");
                Information("found-by={0}", result.Invocation.ExecutableFoundBy);
            });
            RunTarget("Default");
            """);

        var run = await DotNet.RunAsync(folder.Path, session.Environment, "run", "--project", "Build.csproj");

        await Expect(run, "dotnet run (Cake.Sdk)");
        await Assert.That(run.Output).Contains("found-by=BundledRuntime");
        await Assert.That(File.Exists(folder.Combine("out", "doc.pdf"))).IsTrue();
    }

    [Test]
    [Property(Scenario.Key, "CK5")]
    public async Task TYPST_PATH_wins_over_a_tool_directive()
    {
        var session = await CakeSession.GetAsync();
        using var folder = await ScriptFolderAsync(session);
        folder.CreateFile("build.cake", Script(session, loadDependencies: true, CakeSession.HostRid));
        var typst = CakeSession.ExtractHostTypst(session, folder);

        var run = await RunCakeAsync(session, folder, new Dictionary<string, string?> { ["TYPST_PATH"] = typst });

        await Expect(run, "dotnet cake");
        await Assert.That(run.Output).Contains("found-by=TYPST_PATH");
    }

    [Test]
    [Property(Scenario.Key, "CK6")]
    public async Task A_document_with_an_error_fails_the_build_with_a_canonical_error_line()
    {
        var session = await CakeSession.GetAsync();
        using var folder = await ScriptFolderAsync(session, BrokenDocument);
        folder.CreateFile("build.cake", Script(session, loadDependencies: true, CakeSession.HostRid));

        var run = await RunCakeAsync(session, folder);

        await Assert.That(run.ExitCode).IsNotEqualTo(0);
        await Assert.That(run.Output).Contains("doc.typ(");
        await Assert.That(run.Output).Contains("error TYPST");
    }
}
```

- [ ] **Step 5: Add the build task and run the scenarios locally**

`build/Build/Tasks/CakeTasks.cs`:

```csharp
public static partial class Program
{
    // docs/specs/cake.md §8. Not part of Default: needs the real runtime packages (PackTypstRuntimes) and runs real Cake.
    static void RegisterCakeTasks()
    {
        Task("CakeEndToEndTests")
            .Does<BuildParameters>(parameters =>
                DotNetTest("tests/Cake.Typstium.EndToEndTests", new DotNetTestSettings
                {
                    PathType = DotNetTestPathType.Project,
                    Configuration = parameters.Configuration,
                    EnvironmentVariables = { ["TYPSTIUM_RUNTIME_PACKAGES"] = MakeAbsolute(Directory("artifacts/packages")).FullPath },
                }));
    }
}
```

In `build/Build/Program.cs`, add `RegisterCakeTasks();` after `RegisterRuntimeTasks();`, and append
`CakeEndToEndTests` to the comment's list of non-default targets.

Run: `./build.ps1 --target=PackTypstRuntimes` (once; downloads about 120 MB), then `./build.ps1 --target=CakeEndToEndTests`
Expected: CK1–CK6 pass. Where one fails, the cause is one of the facts spec §9 lists as unverified. Fix the plan's
assumption, not the test's intent:

- CK1/CK2 can't restore the addin: Cake didn't read `nuget.config` from the script folder. Pass the feed explicitly,
  `#addin nuget:{session.Feed}?package=…` (and the same for `#tool` with `session.RuntimePackages`); record it in the
  user guide (Task 12).
- CK1/CK2 report `found-by=` something other than `CakeTools`: list the folder names under `tools/` in the failure
  output and fix `ToolFolderVersion`/`CakeToolsLocator` for the layout Cake actually writes (spec §9 item 1).
- CK3 passes for a different reason (e.g. the run failed earlier): check that `run.Output` contains `CS0012` or
  `CS0246`, and assert on the code it actually prints.
- CK4 fails to compile `TypstCompile(...)`: Cake.Sdk didn't generate the alias proxy. Use `Context.TypstCompile(...)`
  in the test and in the user guide, and record it against spec §9 item 3.

- [ ] **Step 6: Run the catalog check again**

Run: `dotnet test --project tests/Cake.Typstium.UnitTests --treenode-filter "/*/*/ScenarioCatalogTests/*"`
Expected: PASS.

- [ ] **Step 7: Run the scenarios in CI**

In `.github/workflows/ci.yml`, in both `runtime-e2e` and `runtime-e2e-windows-arm`, after the existing
`RuntimeEndToEndTests` step(s), add:

```yaml
      - if: runner.os == 'Windows'
        run: ./build.ps1 --target=CakeEndToEndTests
        shell: pwsh
      - if: runner.os != 'Windows'
        run: ./build.sh --target=CakeEndToEndTests
        shell: bash
```

In `runtime-e2e-windows-arm`, which runs on Windows only, add just the `./build.ps1 --target=CakeEndToEndTests` step
with `shell: pwsh`.

- [ ] **Step 8: Commit**

```bash
git add tests/Cake.Typstium.EndToEndTests tests/Cake.Typstium.UnitTests/ScenarioCatalogTests.cs build/Build Typstium.slnx .github/workflows/ci.yml
git commit -m "Run Cake scripts and a Cake.Sdk project against the packed addin end to end"
```

---

### Task 12: User guide and status

**Files:**
- Create: `docs/cake.md`
- Modify: `src/Cake.Typstium/README.md` (only if Task 11 changed how scripts must reference the feed)
- Modify: `docs/specs/cake.md` (status, §9 findings)
- Modify: `docs/design.md` (status row, next steps)

- [ ] **Step 1: Write the user guide**

`docs/cake.md` is for people writing Cake builds (consumer audience: no pack properties, no Cake tasks of this
repository). Sections, each with a working example:

1. **Install**: the `#addin nuget:?package=Cake.Typstium&loaddependencies=true` directive and why `loaddependencies`
   is required (the error a script gets without it, CK3's message); the global alternatives
   `NuGet_LoadDependencies=true` in `cake.config` and `CAKE_NUGET_LOADDEPENDENCIES=true`. For Cake.Sdk and Frosting:
   `<PackageReference Include="Cake.Typstium" />`, plus `using Typstium;` where core types are used.
2. **Getting Typst**: the table of spec §6, in that order, with the one-`#tool`-per-platform example:

   ```csharp
   #tool nuget:?package=Typstium.Runtime.win-x64&version=0.15.1
   #tool nuget:?package=Typstium.Runtime.linux-x64&version=0.15.1
   #tool nuget:?package=Typstium.Runtime.osx-arm64&version=0.15.1
   ```

   and the full search order (`ToolPath` → `TYPST_PATH` → `CakeTools` → `BundledRuntime` → `WinGet` →
   `WellKnownLocations` → `PATH`). Note that `Typstium.Runtime.All` doesn't work with `#tool` (no dependencies).
3. **Compiling**: `TypstCompile(input)`, `(input, output)`, `(input, output, settings)`; formats by extension; page
   patterns `{p}`, `{0p}`, `{t}` for PNG and SVG; the async aliases and when to use them.
4. **Settings**: a table of every `TypstCompileSettings` property with its default and fluent method; the reproducible
   defaults (no system fonts, no network) and how to add fonts (`WithFontPath`) and vendored packages
   (`WithPackagePath`).
5. **Failures and diagnostics**: what is logged, the canonical line format, `TreatWarningsAsErrors`, which exceptions
   pass through, and that the result is returned on success.
6. **Beyond the aliases**: `TypstCompiler()` with one example (a batch sharing one compiler, or markup with a virtual
   file).
7. **Not yet supported**: Markdown through cmarker (planned with the Markdown support in all integrations).

Include every adjustment Task 11 made (feed syntax, `Context.` prefix in Cake.Sdk) as the documented way, not as a
caveat.

- [ ] **Step 2: Update the spec and the design overview**

- `docs/specs/cake.md`: status line `**Status:** Implemented · <date> · Magnus Lindhe`; in §9, strike through each item
  the implementation verified and append what was found (as items 4 and 7 already are). Link the user guide from §6.
- `docs/design.md`: Cake row status `Implemented` and the user guide link next to the spec link, as the runtime packages
  row has; tick `- [x] \`Cake.Typstium\` ([spec](specs/cake.md))` in **Next steps**.

- [ ] **Step 3: Verify the whole build**

Run: `./build.ps1` (or `./build.sh`)
Expected: PASS.
Run: `dotnet pack src/Cake.Typstium -c Release -o artifacts/check`
Expected: `artifacts/check/Cake.Typstium.1.0.0.nupkg` whose `.nuspec` lists a dependency on `Typstium` and none on
`Cake.Core` (open the nupkg as a zip to check), and whose `lib/net8.0/` holds only `Cake.Typstium.dll` and
`Cake.Typstium.xml`. Delete `artifacts/check` afterwards.

- [ ] **Step 4: Commit**

```bash
git add docs src/Cake.Typstium/README.md
git commit -m "Add the Cake.Typstium user guide and mark the spec implemented"
```
