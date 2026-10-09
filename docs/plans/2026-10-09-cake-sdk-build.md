# Cake.Sdk Build Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the PowerShell build (`build.ps1` and three helper scripts under `build/`) with a project-based Cake.Sdk build. It splits into a Typstium-specific `Build` project and a Typstium-agnostic, TUnit-tested `Build.Cake.Extensions` library. The build is started by thin `build.ps1` and `build.sh` bootstraps.

**Architecture:**

- `build/Build` (Sdk `Cake.Sdk`) holds the task graph and the Typstium values.
- `build/Build.Cake.Extensions` holds:
  - pure coverage logic: Cobertura reading, grouping, Markdown, comparison, `summary.json`
  - Spectre.Console rendering
  - thin edges over Cake: ReportGenerator.Core in process, `gh`/`git`, the GitHub Actions step summary
- Pure units are proven byte-identical to the PowerShell scripts with golden files that those scripts generate before
  they are deleted.

**Tech Stack:**

- .NET 10 SDK, Cake.Sdk 6.3.0 (Cake.Core and Cake.Common 6.3.0), Cake.BuildSystems.Module 9.0.0
- ReportGenerator.Core 5.5.11, Spectre.Console 0.57.2
- TUnit 1.73.5 on Microsoft.Testing.Platform

**Spec:** [`docs/specs/cake-sdk-build.md`](../specs/cake-sdk-build.md). Task 1 amends it with the refinements below.

## Global Constraints

- Branch `chore/cake-sdk`, with one pull request at the end. No pushing or PR creation without the user's confirmation.
- Cake.Sdk `6.3.0`, pinned in `global.json` under `msbuild-sdks`. Cake.Common `6.3.0`. Cake.BuildSystems.Module
  `9.0.0`. ReportGenerator.Core `5.5.11`. Spectre.Console and Spectre.Console.Testing `0.57.2`. All versions live in
  the root `Directory.Packages.props` (central package management).
- `build/` projects are not in `Typstium.slnx`. They are in `build/Build.slnx`.
- The root `Directory.Build.props` applies (`TreatWarningsAsErrors`, `Nullable`, `LangVersion latest`). Warnings are
  never silenced wholesale. Use only a narrowly scoped `NoWarn` in `build/Directory.Build.props`, with a comment.
- `Build.Cake.Extensions` never mentions Typstium. The Typstium values live in `build/Build/TypstiumCoverage.cs`.
- Pure units take no `ICakeContext`. Edges (`CoverageMerger`, `CoverageAliases`, `GitHubCli`,
  `PullRequestCoverageAliases`) do.
- Markdown and JSON outputs are byte-identical to the PowerShell scripts' output, compared with line endings
  normalized to `\n`. The C# code always writes `\n`.
- All number formatting uses `CultureInfo.InvariantCulture`. The developer's machine may be `sv-SE`.
- Arguments: `--configuration=<c>` (default `Release`), `--coverage`, `--no-job-summary`, `--target=<t>` (default
  `Default`), `--max-line-decrease=<pp>` (default `0.5`).
- Targets: `Default`, `BuildTests`, `Build`, `UnitTests`, `IntegrationTests`, `TimingTests`, `CoverageReport`,
  `CoverageComparison`.
- Test names follow the repo's style: `Sentence_with_underscores`. Assertions use TUnit's `await Assert.That(...)`.
- Commits end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Refinements to the spec (Task 1 writes them into the spec)

1. **Pull-request context comes from the event payload.** Cake's `GitHubActions.Environment.Workflow.EventPath`
   points at the event JSON, which holds `pull_request.number`, `.head.sha`, `.head.repo.full_name` and
   `.user.login`. This replaces the `PR_NUMBER`, `HEAD_SHA`, `HEAD_REPOSITORY` and `PR_AUTHOR` environment variables,
   so `ci.yml` passes only `GH_TOKEN`. Repository and base ref come from `Workflow.Repository` and
   `Workflow.BaseRef`, which read `GITHUB_REPOSITORY` and `GITHUB_BASE_REF` (checked against Cake 6.3.0's
   `GitHubActionsWorkflowInfo`).
2. **Request bodies go through JSON files.** The comment and the check run are posted with
   `gh api --input <file.json>`, with the JSON built in C#. This avoids quoting Markdown on a command line. Comments
   are listed with `gh api --paginate --slurp` and searched in C# instead of with `--jq`.
3. **A pull request whose head repository was deleted** (`head.repo: null`) has a read-only token, so it is treated
   like a fork. The script would have tried to post and failed.
4. **Name ordering.** Ordering uses `StringComparer.InvariantCultureIgnoreCase`, which is closest to `Sort-Object`.
   Lookups use `StringComparer.OrdinalIgnoreCase`, like PowerShell hashtables. One constant each in `NameOrder`; the
   golden files decide.
5. **Extra files:**
   - `Coverage/CoverageAliases.cs`: the `ReportCoverage` edge that runs section 6 of the spec end to end.
   - `GitHub/PullRequestEvent.cs`, `GitHub/WorkflowRun.cs` and `GitHub/GitHubJson.cs`: pure parsers and payloads.
   - `GitHub/PullRequestCoverageAliases.cs`: the `PublishPullRequestCoverage` edge, which replaces
     `PullRequestCoverage.cs`.

## Review Focus

1. **A developer machine with a comma decimal separator (`sv-SE`).** Report tables, `summary.json`, the PR comment
   and the `--max-line-decrease` argument must still use `.`. Pinned by tests in Tasks 6, 7 and 9, and by parsing in
   Task 12.
2. **`--target=CoverageReport` (or `--coverage`) with no raw Cobertura files.** Expect a one-line error naming the
   folder and saying to run the tests with `--coverage`, not a ReportGenerator stack trace or an empty report.
   Pinned in Task 4.
3. **Baselines written by the PowerShell version**, including one whose `namespaces` collapsed to a single object or
   that has extra properties. Expect them to be read as before. Pinned in Task 3.
4. **A pull request from a fork that was since deleted** (`head.repo` is `null`). Expect the read-only path (job
   summary and notice), not a failed comment post. Pinned in Task 10.
5. **Running a bootstrap from a subdirectory** (`cd tests && ../build.sh --target=BuildTests`). Expect the same
   behavior as from the root, because `RunWorkingDirectory` is the repo root. Checked in Task 12.

## File Map

```
build.ps1                                   rewritten: bootstrap
build.sh                                    new: bootstrap (executable, LF)
global.json                                 + msbuild-sdks
Directory.Packages.props                    + Cake/ReportGenerator/Spectre versions
.gitattributes                              + build.sh text eol=lf
.github/workflows/ci.yml                    new commands
.github/dependabot.yml                      cake group, Spectre ignore, ReportGenerator in build-tooling
README.md                                   Building section
docs/specs/cake-sdk-build.md                refinements
build/Build.slnx
build/Directory.Build.props
build/Build/Build.csproj
build/Build/Program.cs
build/Build/BuildParameters.cs
build/Build/TypstiumCoverage.cs
build/Build/Tasks/BuildTasks.cs
build/Build/Tasks/TestTasks.cs
build/Build/Tasks/CoverageTasks.cs
build/Build/Tasks/DefaultTasks.cs
build/Build.Cake.Extensions/Build.Cake.Extensions.csproj
build/Build.Cake.Extensions/Invariant.cs
build/Build.Cake.Extensions/NameOrder.cs
build/Build.Cake.Extensions/Coverage/Model/CoverageCounts.cs
build/Build.Cake.Extensions/Coverage/Model/ClassCoverage.cs
build/Build.Cake.Extensions/Coverage/Model/CoverageSummary.cs
build/Build.Cake.Extensions/Coverage/Cobertura/CoberturaPathNormalizer.cs
build/Build.Cake.Extensions/Coverage/Cobertura/CoberturaReader.cs
build/Build.Cake.Extensions/Coverage/Reporting/PublicApiCatalog.cs
build/Build.Cake.Extensions/Coverage/Reporting/NamespaceResolver.cs
build/Build.Cake.Extensions/Coverage/Reporting/CoverageTable.cs
build/Build.Cake.Extensions/Coverage/Reporting/CoverageReport.cs
build/Build.Cake.Extensions/Coverage/Reporting/CoverageReportBuilder.cs
build/Build.Cake.Extensions/Coverage/Rendering/MarkdownTableRenderer.cs
build/Build.Cake.Extensions/Coverage/Rendering/SpectreTableRenderer.cs
build/Build.Cake.Extensions/Coverage/Comparison/CoverageComparisonOptions.cs
build/Build.Cake.Extensions/Coverage/Comparison/CoverageComparison.cs
build/Build.Cake.Extensions/Coverage/Comparison/CoverageComparer.cs
build/Build.Cake.Extensions/Coverage/CoverageMerger.cs
build/Build.Cake.Extensions/Coverage/CoverageReportSettings.cs
build/Build.Cake.Extensions/Coverage/CoverageAliases.cs
build/Build.Cake.Extensions/GitHub/PullRequestEvent.cs
build/Build.Cake.Extensions/GitHub/PullRequestContext.cs
build/Build.Cake.Extensions/GitHub/WorkflowRun.cs
build/Build.Cake.Extensions/GitHub/GitHubJson.cs
build/Build.Cake.Extensions/GitHub/GitHubCli.cs
build/Build.Cake.Extensions/GitHub/PullRequestCoverageSettings.cs
build/Build.Cake.Extensions/GitHub/PullRequestCoverageAliases.cs
build/Build.Cake.Extensions.Tests/Build.Cake.Extensions.Tests.csproj
build/Build.Cake.Extensions.Tests/Fixture.cs
build/Build.Cake.Extensions.Tests/**Tests.cs                    one per unit, mirroring the library's folders
build/Build.Cake.Extensions.Tests/Fixtures/...                  see Task 2
build/Compare-Coverage.ps1, Publish-CoverageComparison.ps1, Write-CoverageReport.ps1   deleted in Task 13
```

---

### Task 1: Amend the spec and scaffold the build projects

**Files:**
- Modify: `docs/specs/cake-sdk-build.md`
- Modify: `global.json`
- Modify: `Directory.Packages.props`
- Create: `build/Build.slnx`, `build/Directory.Build.props`
- Create: `build/Build/Build.csproj`, `build/Build/Program.cs`
- Create: `build/Build.Cake.Extensions/Build.Cake.Extensions.csproj`
- Create: `build/Build.Cake.Extensions.Tests/Build.Cake.Extensions.Tests.csproj`, `build/Build.Cake.Extensions.Tests/Fixture.cs`, `build/Build.Cake.Extensions.Tests/ScaffoldTests.cs`

**Interfaces:**
- Produces:
  - Project paths used by every later task.
  - `Fixture.Path(params string[] parts) : string` and `Fixture.Read(params string[] parts) : string`. `Read`
    normalizes line endings to `\n`.

- [ ] **Step 1: Amend the spec.** Edit `docs/specs/cake-sdk-build.md` so it matches "Refinements to the spec" in this
  plan:
  - §3 layout: add the files listed in refinement 5, and replace `PullRequestCoverage.cs` with
    `PullRequestCoverageAliases.cs`.
  - §7: the context comes from the event payload; bodies go through `--input` JSON files; comments are listed with
    `--slurp`; a deleted head repository counts as read-only; ordering uses `InvariantCultureIgnoreCase` and lookups
    `OrdinalIgnoreCase`.
  - §10: the coverage-comparison step passes only `GH_TOKEN`.
  - §6: Markdown is always written with `\n`.

- [ ] **Step 2: Pin Cake.Sdk.** Replace the contents of `global.json` with:

```json
{
  "sdk": {
    "version": "10.0.401",
    "rollForward": "latestFeature"
  },
  "msbuild-sdks": {
    "Cake.Sdk": "6.3.0"
  },
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

- [ ] **Step 3: Add the package versions.** Append to the `ItemGroup` in `Directory.Packages.props`:

```xml
    <!-- The build under build/ (docs/specs/cake-sdk-build.md). Cake.Common matches Cake.Sdk in global.json;
         Spectre.Console matches the version Cake.Cli ships, and moves only with Cake. -->
    <PackageVersion Include="Cake.Common" Version="6.3.0" />
    <PackageVersion Include="Cake.BuildSystems.Module" Version="9.0.0" />
    <PackageVersion Include="ReportGenerator.Core" Version="5.5.11" />
    <PackageVersion Include="Spectre.Console" Version="0.57.2" />
    <PackageVersion Include="Spectre.Console.Testing" Version="0.57.2" />
```

- [ ] **Step 4: Create `build/Directory.Build.props`.**

```xml
<Project>
  <!-- The repository's settings (warnings as errors, nullable, latest C#), then what only the build projects need. -->
  <Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" />
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
</Project>
```

- [ ] **Step 5: Create `build/Build.Cake.Extensions/Build.Cake.Extensions.csproj`.**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <PackageReference Include="Cake.Common" />
    <PackageReference Include="ReportGenerator.Core" />
    <PackageReference Include="Spectre.Console" />
  </ItemGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="Build.Cake.Extensions.Tests" />
  </ItemGroup>

</Project>
```

- [ ] **Step 6: Create `build/Build/Build.csproj`.**

```xml
<Project Sdk="Cake.Sdk">

  <PropertyGroup>
    <!-- Every path in the build is relative to the repository root, wherever the bootstrap is run from. -->
    <RunWorkingDirectory>$(MSBuildProjectDirectory)/../..</RunWorkingDirectory>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Cake.BuildSystems.Module" />
    <ProjectReference Include="../Build.Cake.Extensions/Build.Cake.Extensions.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 7: Create a minimal `build/Build/Program.cs`** to prove Cake.Sdk compiles under the repo's settings. Task
  12 replaces it.

```csharp
var target = Argument("target", "Default");

Task("Default")
    .Does(() => Information("Cake.Sdk build scaffold"));

RunTarget(target);
```

- [ ] **Step 8: Create the test project `build/Build.Cake.Extensions.Tests/Build.Cake.Extensions.Tests.csproj`.**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <PackageReference Include="TUnit" />
    <PackageReference Include="Spectre.Console.Testing" />
    <ProjectReference Include="../Build.Cake.Extensions/Build.Cake.Extensions.csproj" />
  </ItemGroup>

  <ItemGroup>
    <None Include="Fixtures/**" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>

</Project>
```

- [ ] **Step 9: Create `build/Build.Cake.Extensions.Tests/Fixture.cs`.**

```csharp
namespace Build.Cake.Extensions.Tests;

// Files under Fixtures/, copied next to the test assembly. Golden files were written by the PowerShell scripts, some
// on Windows: reading normalizes line endings, so comparisons are about content, not the machine that wrote them.
internal static class Fixture
{
    public static string Path(params string[] parts) => System.IO.Path.Combine([AppContext.BaseDirectory, "Fixtures", .. parts]);

    public static string Read(params string[] parts) => File.ReadAllText(Path(parts)).ReplaceLineEndings("\n");
}
```

- [ ] **Step 10: Write a scaffold test** in `build/Build.Cake.Extensions.Tests/ScaffoldTests.cs`. Task 3 deletes it.

```csharp
namespace Build.Cake.Extensions.Tests;

public class ScaffoldTests
{
    [Test]
    public async Task Test_project_runs()
    {
        await Assert.That(AppContext.BaseDirectory).IsNotEmpty();
    }
}
```

- [ ] **Step 11: Create `build/Build.slnx`.**

```xml
<Solution>
  <Project Path="Build/Build.csproj" />
  <Project Path="Build.Cake.Extensions/Build.Cake.Extensions.csproj" />
  <Project Path="Build.Cake.Extensions.Tests/Build.Cake.Extensions.Tests.csproj" />
</Solution>
```

- [ ] **Step 12: Build, test and run.**
  - Run: `dotnet build build/Build.slnx`. Expected: build succeeded, 0 warnings.
  - If Cake.Sdk's generated code trips a warning-as-error, add only that ID to `<NoWarn>` in
    `build/Directory.Build.props`, inside a `PropertyGroup` with `Condition="'$(UsingCakeSdk)' == 'true'"` if Cake.Sdk
    defines such a property. Otherwise put it in `Build.csproj`. Add a comment naming the generator.
  - Run: `dotnet test --project build/Build.Cake.Extensions.Tests`. Expected: 1 test passed.
  - Run: `dotnet run --project build/Build`. Expected: the log shows `Cake.Sdk build scaffold`, and the exit code is
    0.
  - Run: `dotnet build` (repo root). Expected: it builds only `Typstium.slnx`, and none of `build/` appears.

- [ ] **Step 13: Commit.**

```bash
git add global.json Directory.Packages.props build/Build.slnx build/Directory.Build.props build/Build build/Build.Cake.Extensions build/Build.Cake.Extensions.Tests docs/specs/cake-sdk-build.md
git commit -m "Scaffold the Cake.Sdk build projects" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Golden fixtures from the PowerShell scripts

The scripts are the specification for the byte-identical outputs. This task runs them on fixed inputs and commits
what they write.

**Files (all under `build/Build.Cake.Extensions.Tests/Fixtures/`):**
- Create: `Real/Cobertura.xml`, `Real/src/Typstium/PublicAPI.*.txt`, `Real/expected/summary.json`, `Real/expected/job-summary.md`
- Create: `EdgeCases/Cobertura.xml`, `EdgeCases/src/Alpha/PublicAPI.Shipped.txt`, `EdgeCases/expected/summary.json`, `EdgeCases/expected/job-summary.md`
- Create: `Summaries/current.json`, `Summaries/baseline-pass.json`, `Summaries/baseline-fail.json`, `Summaries/current-boundary.json`, `Summaries/baseline-boundary.json`, `Summaries/single-namespace.json`
- Create: `Summaries/expected/{no-baseline,pass,fail,boundary,unchanged}.md` and `.result.json`
- Create: `Events/pull_request.json`, `Events/pull_request-deleted-fork.json`

**Interfaces:**
- Produces: the fixture paths that Tasks 3–10 read through `Fixture.Read(...)`.

- [ ] **Step 1: Produce a real merged report with the current build.**
  - Run (pwsh, repo root, Typst installed): `./build.ps1 -Coverage`
  - Expected: every test step passes, the coverage tables print, and `artifacts/coverage/report/Cobertura.xml` and
    `artifacts/coverage/summary.json` exist.
  - Then keep a copy for Task 12's side-by-side comparison:
    `Copy-Item -Recurse artifacts/coverage artifacts/coverage-powershell`

- [ ] **Step 2: Copy the real inputs.**

```powershell
$f = 'build/Build.Cake.Extensions.Tests/Fixtures'
New-Item -ItemType Directory -Force "$f/Real/src/Typstium", "$f/Real/expected" | Out-Null
Copy-Item artifacts/coverage/report/Cobertura.xml "$f/Real/Cobertura.xml"
Copy-Item src/Typstium/PublicAPI.*.txt "$f/Real/src/Typstium/"
Select-String -Path "$f/Real/Cobertura.xml" -Pattern 'C:\\|/home/|Users' | Select-Object -First 5
```

Expected: no matches, because the merged report holds repo-relative file names. If `<source>` elements carry an
absolute path, replace their text with `.` and say so in the commit message.

- [ ] **Step 3: Write `EdgeCases/Cobertura.xml`.** It covers nested and generic types, a partial class in two files,
  a root-level file, a file outside the product folder, an absolute path, a class without lines, and a product
  without PublicAPI files.

```xml
<?xml version="1.0" encoding="utf-8"?>
<coverage line-rate="0" branch-rate="0" version="1.9" timestamp="0">
  <packages>
    <package name="Alpha" line-rate="0" branch-rate="0" complexity="0">
      <classes>
        <class name="Alpha.Api.Widget" filename="src/Alpha/Api/Widget.cs" line-rate="0" branch-rate="0" complexity="0">
          <methods>
            <method name="Run" signature="()" line-rate="1" branch-rate="1" complexity="1">
              <lines><line number="10" hits="3" branch="false" /></lines>
            </method>
            <method name="Stop" signature="()" line-rate="0" branch-rate="0" complexity="1">
              <lines><line number="20" hits="0" branch="false" /></lines>
            </method>
          </methods>
          <lines>
            <line number="10" hits="3" branch="true" condition-coverage="50% (1/2)" />
            <line number="11" hits="3" branch="false" />
            <line number="20" hits="0" branch="false" />
            <line number="21" hits="0" branch="true" condition-coverage="0% (0/2)" />
          </lines>
        </class>
        <class name="Alpha.Api.Widget.Inner" filename="src/Alpha/Api/Widget.cs" line-rate="0" branch-rate="0" complexity="0">
          <methods />
          <lines>
            <line number="40" hits="1" branch="false" />
            <line number="41" hits="0" branch="false" />
          </lines>
        </class>
        <class name="Alpha.Api.Box&lt;T&gt;" filename="src/Alpha/Api/Box.cs" line-rate="0" branch-rate="0" complexity="0">
          <methods>
            <method name="Get" signature="()" line-rate="1" branch-rate="1" complexity="1">
              <lines><line number="5" hits="2" branch="false" /></lines>
            </method>
          </methods>
          <lines>
            <line number="5" hits="2" branch="false" />
            <line number="6" hits="2" branch="false" />
            <line number="7" hits="2" branch="false" />
          </lines>
        </class>
        <class name="Alpha.Internal.Cache&lt;TKey, TValue&gt;" filename="src/Alpha/Internal/Cache.cs" line-rate="0" branch-rate="0" complexity="0">
          <methods />
          <lines>
            <line number="1" hits="1" branch="false" />
            <line number="2" hits="0" branch="false" />
            <line number="3" hits="0" branch="false" />
            <line number="4" hits="0" branch="false" />
          </lines>
        </class>
        <class name="Alpha.Internal.Parser" filename="src/Alpha/Internal/Parser.cs" line-rate="0" branch-rate="0" complexity="0">
          <methods />
          <lines>
            <line number="1" hits="1" branch="false" />
            <line number="2" hits="1" branch="false" />
            <line number="3" hits="0" branch="false" />
            <line number="4" hits="0" branch="false" />
            <line number="5" hits="0" branch="false" />
          </lines>
        </class>
        <class name="Alpha.Internal.Parser" filename="src/Alpha/Internal/Parser.Tokens.cs" line-rate="0" branch-rate="0" complexity="0">
          <methods />
          <lines>
            <line number="1" hits="0" branch="true" condition-coverage="25% (1/4)" />
            <line number="2" hits="0" branch="false" />
            <line number="3" hits="0" branch="false" />
          </lines>
        </class>
        <class name="Alpha.Root" filename="src/Alpha/Root.cs" line-rate="0" branch-rate="0" complexity="0">
          <methods />
          <lines>
            <line number="1" hits="1" branch="false" />
            <line number="2" hits="1" branch="false" />
          </lines>
        </class>
        <class name="Alpha.Generated" filename="obj/Release/Generated.cs" line-rate="0" branch-rate="0" complexity="0">
          <methods />
          <lines>
            <line number="1" hits="0" branch="false" />
            <line number="2" hits="0" branch="false" />
          </lines>
        </class>
        <class name="Alpha.Empty" filename="src/Alpha/Empty.cs" line-rate="0" branch-rate="0" complexity="0">
          <methods />
          <lines />
        </class>
      </classes>
    </package>
    <package name="Beta" line-rate="0" branch-rate="0" complexity="0">
      <classes>
        <class name="Beta.Thing" filename="C:/agent/work/src/Beta/Thing.cs" line-rate="0" branch-rate="0" complexity="0">
          <methods />
          <lines>
            <line number="1" hits="1" branch="false" />
            <line number="2" hits="0" branch="false" />
            <line number="3" hits="0" branch="false" />
            <line number="4" hits="0" branch="false" />
            <line number="5" hits="0" branch="false" />
            <line number="6" hits="0" branch="false" />
          </lines>
        </class>
        <class name="Beta.Tools.Helper" filename="src/Beta/Tools/Helper.cs" line-rate="0" branch-rate="0" complexity="0">
          <methods />
          <lines>
            <line number="1" hits="5" branch="false" />
            <line number="2" hits="5" branch="false" />
          </lines>
        </class>
      </classes>
    </package>
  </packages>
</coverage>
```

- [ ] **Step 4: Write `EdgeCases/src/Alpha/PublicAPI.Shipped.txt`.** Beta deliberately has none.

```
#nullable enable
Alpha.Api.Widget
Alpha.Api.Widget.Run() -> void
Alpha.Api.Widget.Inner
Alpha.Api.Box<T>
Alpha.Api.Box<T>.Get() -> T
Alpha.Root
```

- [ ] **Step 5: Generate the report golden files** with the current script.

```powershell
$f = 'build/Build.Cake.Extensions.Tests/Fixtures'
foreach ($name in 'Real', 'EdgeCases') {
    New-Item -ItemType Directory -Force "$f/$name/expected" | Out-Null
    Remove-Item "$f/$name/expected/job-summary.md" -ErrorAction SilentlyContinue
    $env:GITHUB_STEP_SUMMARY = (Resolve-Path "$f/$name/expected").Path + '/job-summary.md'
    ./build/Write-CoverageReport.ps1 -CoberturaPath "$f/$name/Cobertura.xml" -SourceDirectory "$f/$name/src" -SummaryPath "$f/$name/expected/summary.json"
}
Remove-Item Env:GITHUB_STEP_SUMMARY
```

Expected: both runs print four tables. Each `expected/` folder then holds `summary.json` and `job-summary.md`. In
`EdgeCases/expected/job-summary.md`, check by eye that:
- Beta's group reads `Unknown (no PublicAPI files)`
- `Alpha.Api.Widget.Inner` is counted under namespace `Alpha.Api`
- `Generated.cs` falls in area `(root)`

- [ ] **Step 6: Write the comparison inputs.** These go in `Summaries/`. PowerShell's `ConvertTo-Json` layout doesn't
  matter here: the files are inputs only.

`current.json` (83.0% lines):

```json
{
  "totals": { "coveredLines": 830, "lines": 1000, "coveredBranches": 300, "branches": 400, "coveredMethods": 180, "methods": 200 },
  "namespaces": [
    { "name": "Typstium", "coveredLines": 400, "lines": 450, "coveredBranches": 150, "branches": 180, "coveredMethods": 90, "methods": 95 },
    { "name": "Typstium.Compilation", "coveredLines": 250, "lines": 300, "coveredBranches": 100, "branches": 120, "coveredMethods": 60, "methods": 65 },
    { "name": "Typstium.Processes", "coveredLines": 180, "lines": 250, "coveredBranches": 50, "branches": 100, "coveredMethods": 30, "methods": 40 }
  ]
}
```

`baseline-pass.json` (82.0%, so +1.0 pp; only Compilation changed):

```json
{
  "totals": { "coveredLines": 820, "lines": 1000, "coveredBranches": 290, "branches": 400, "coveredMethods": 178, "methods": 200 },
  "namespaces": [
    { "name": "Typstium", "coveredLines": 400, "lines": 450, "coveredBranches": 150, "branches": 180, "coveredMethods": 90, "methods": 95 },
    { "name": "Typstium.Compilation", "coveredLines": 240, "lines": 300, "coveredBranches": 90, "branches": 120, "coveredMethods": 58, "methods": 65 },
    { "name": "Typstium.Processes", "coveredLines": 180, "lines": 250, "coveredBranches": 50, "branches": 100, "coveredMethods": 30, "methods": 40 }
  ]
}
```

`baseline-fail.json` (85.0%, so −2.0 pp; Typstium changed, Processes is new, Legacy was removed):

```json
{
  "totals": { "coveredLines": 850, "lines": 1000, "coveredBranches": 320, "branches": 400, "coveredMethods": 185, "methods": 200 },
  "namespaces": [
    { "name": "Typstium", "coveredLines": 410, "lines": 450, "coveredBranches": 160, "branches": 180, "coveredMethods": 92, "methods": 95 },
    { "name": "Typstium.Compilation", "coveredLines": 250, "lines": 300, "coveredBranches": 100, "branches": 120, "coveredMethods": 60, "methods": 65 },
    { "name": "Typstium.Legacy", "coveredLines": 190, "lines": 250, "coveredBranches": 60, "branches": 100, "coveredMethods": 33, "methods": 40 }
  ]
}
```

`current-boundary.json` (99.5%) and `baseline-boundary.json` (100.0%). The change is exactly −0.5 pp, which passes:

```json
{
  "totals": { "coveredLines": 995, "lines": 1000, "coveredBranches": 10, "branches": 10, "coveredMethods": 10, "methods": 10 },
  "namespaces": [
    { "name": "Typstium", "coveredLines": 995, "lines": 1000, "coveredBranches": 10, "branches": 10, "coveredMethods": 10, "methods": 10 }
  ]
}
```

```json
{
  "totals": { "coveredLines": 1000, "lines": 1000, "coveredBranches": 10, "branches": 10, "coveredMethods": 10, "methods": 10 },
  "namespaces": [
    { "name": "Typstium", "coveredLines": 1000, "lines": 1000, "coveredBranches": 10, "branches": 10, "coveredMethods": 10, "methods": 10 }
  ]
}
```

`single-namespace.json`: the shape PowerShell writes when an array collapses to an object, plus an unknown property.

```json
{
  "totals": { "coveredLines": 5, "lines": 10, "coveredBranches": 0, "branches": 0, "coveredMethods": 1, "methods": 2, "extra": 1 },
  "namespaces": { "name": "Solo", "coveredLines": 5, "lines": 10, "coveredBranches": 0, "branches": 0, "coveredMethods": 1, "methods": 2 }
}
```

- [ ] **Step 7: Generate the comparison golden files.**

```powershell
$s = 'build/Build.Cake.Extensions.Tests/Fixtures/Summaries'
New-Item -ItemType Directory -Force "$s/expected" | Out-Null
$cases = @(
    @{ Name = 'no-baseline'; Current = 'current.json';          Baseline = $null;                   Label = '`main`' },
    @{ Name = 'pass';        Current = 'current.json';          Baseline = 'baseline-pass.json';     Label = '`main` at `abc1234` (merge base)' },
    @{ Name = 'fail';        Current = 'current.json';          Baseline = 'baseline-fail.json';     Label = '`main` at `def5678` (latest; the merge base `abc1234` has no coverage)' },
    @{ Name = 'boundary';    Current = 'current-boundary.json'; Baseline = 'baseline-boundary.json'; Label = '`main` at `abc1234` (merge base)' },
    @{ Name = 'unchanged';   Current = 'current.json';          Baseline = 'current.json';           Label = '`main` at `abc1234` (merge base)' })
foreach ($case in $cases) {
    $arguments = @{ Current = "$s/$($case.Current)"; BaselineLabel = $case.Label; MaxLineDecrease = 0.5
        MarkdownPath = "$s/expected/$($case.Name).md"; ResultPath = "$s/expected/$($case.Name).result.json" }
    if ($case.Baseline) { $arguments.Baseline = "$s/$($case.Baseline)" }
    ./build/Compare-Coverage.ps1 @arguments
}
```

Expected output:
- `no-baseline`: neutral, `83.0% lines (no baseline)`
- `pass`: success, `83.0% lines (+1.0 pp)`
- `fail`: failure, `83.0% lines (−2.0 pp)`
- `boundary`: success, `99.5% lines (−0.5 pp)`
- `unchanged`: success, `83.0% lines (±0.0 pp)`

- [ ] **Step 8: Write the event payloads** in `Events/`. They are trimmed to the fields used, in GitHub's
  `pull_request` event shape.

`pull_request.json`:

```json
{
  "action": "synchronize",
  "number": 42,
  "pull_request": {
    "number": 42,
    "head": { "ref": "chore/cake-sdk", "sha": "0123456789abcdef0123456789abcdef01234567", "repo": { "full_name": "mgnslndh/Typstium" } },
    "base": { "ref": "main", "sha": "89abcdef0123456789abcdef0123456789abcdef", "repo": { "full_name": "mgnslndh/Typstium" } },
    "user": { "login": "mgnslndh" }
  },
  "repository": { "full_name": "mgnslndh/Typstium" }
}
```

`pull_request-deleted-fork.json`:

```json
{
  "action": "opened",
  "number": 7,
  "pull_request": {
    "number": 7,
    "head": { "ref": "patch-1", "sha": "fedcba9876543210fedcba9876543210fedcba98", "repo": null },
    "base": { "ref": "main", "sha": "89abcdef0123456789abcdef0123456789abcdef", "repo": { "full_name": "mgnslndh/Typstium" } },
    "user": { "login": "someone" }
  },
  "repository": { "full_name": "mgnslndh/Typstium" }
}
```

- [ ] **Step 9: Check that the fixtures are copied to the test output.**
  - Run: `dotnet build build/Build.Cake.Extensions.Tests`
  - Then: `ls build/Build.Cake.Extensions.Tests/bin/Debug/net10.0/Fixtures/Summaries/expected`
  - Expected: the 10 expected files are listed.

- [ ] **Step 10: Commit.**

```bash
git add build/Build.Cake.Extensions.Tests/Fixtures
git commit -m "Add coverage fixtures and golden files written by the PowerShell scripts" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Coverage model and `summary.json`

**Files:**
- Create: `build/Build.Cake.Extensions/Invariant.cs`, `build/Build.Cake.Extensions/NameOrder.cs`
- Create: `build/Build.Cake.Extensions/Coverage/Model/CoverageCounts.cs`, `ClassCoverage.cs`, `CoverageSummary.cs`
- Create: `build/Build.Cake.Extensions.Tests/Coverage/Model/CoverageSummaryTests.cs`
- Delete: `build/Build.Cake.Extensions.Tests/ScaffoldTests.cs`

**Interfaces:**
- Produces:
  - `record CoverageCounts(int CoveredLines, int Lines, int CoveredBranches, int Branches, int CoveredMethods, int Methods)`,
    with `Zero`, `MissedLines`, `MissedBranches`, `operator +` and `Sum(IEnumerable<CoverageCounts>)`
  - `record ClassCoverage(string Assembly, string Name, string File, CoverageCounts Counts)`
  - `record NamespaceSummary(string Name, CoverageCounts Counts)`
  - `record CoverageSummary(CoverageCounts Totals, IReadOnlyList<NamespaceSummary> Namespaces)`, with
    `string ToJson()`, `static CoverageSummary FromJson(string)`, `void Write(string path)` and
    `static CoverageSummary Read(string path)`
  - `static class Invariant`: `string Format(FormattableString)`
  - `static class NameOrder`: `StringComparer Sort`, `StringComparer Lookup`

- [ ] **Step 1: Write the failing tests** in `build/Build.Cake.Extensions.Tests/Coverage/Model/CoverageSummaryTests.cs`.

```csharp
using Build.Cake.Extensions.Coverage.Model;

namespace Build.Cake.Extensions.Tests.Coverage.Model;

public class CoverageSummaryTests
{
    [Test]
    [Arguments("Real")]
    [Arguments("EdgeCases")]
    public async Task Writes_a_summary_exactly_as_the_PowerShell_report_did(string fixture)
    {
        var expected = Fixture.Read(fixture, "expected", "summary.json");

        var written = CoverageSummary.FromJson(expected).ToJson() + "\n";

        await Assert.That(written).IsEqualTo(expected);
    }

    [Test]
    public async Task Reads_a_baseline_written_by_the_PowerShell_report()
    {
        var summary = CoverageSummary.FromJson(Fixture.Read("Real", "expected", "summary.json"));

        await Assert.That(summary.Totals.Lines).IsGreaterThan(0);
        await Assert.That(summary.Namespaces.Count).IsGreaterThan(0);
        await Assert.That(summary.Namespaces[0].Name).StartsWith("Typstium");
    }

    [Test]
    public async Task Reads_a_single_namespace_collapsed_to_an_object_and_ignores_unknown_properties()
    {
        var summary = CoverageSummary.FromJson(Fixture.Read("Summaries", "single-namespace.json"));

        await Assert.That(summary.Totals).IsEqualTo(new CoverageCounts(5, 10, 0, 0, 1, 2));
        await Assert.That(summary.Namespaces.Count).IsEqualTo(1);
        await Assert.That(summary.Namespaces[0]).IsEqualTo(new NamespaceSummary("Solo", new CoverageCounts(5, 10, 0, 0, 1, 2)));
    }

    [Test]
    public async Task Writes_no_namespaces_as_an_empty_array()
    {
        var json = new CoverageSummary(CoverageCounts.Zero, []).ToJson();

        await Assert.That(json).Contains("\"namespaces\": []");
    }

    [Test]
    public async Task Write_creates_the_folder_and_ends_with_a_newline_without_a_byte_order_mark()
    {
        var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "summary-" + Guid.NewGuid().ToString("N"));
        var path = System.IO.Path.Combine(folder, "nested", "summary.json");
        try
        {
            new CoverageSummary(new CoverageCounts(1, 2, 0, 0, 1, 1), []).Write(path);

            var bytes = File.ReadAllBytes(path);
            await Assert.That(bytes[0]).IsEqualTo((byte)'{');
            await Assert.That(bytes[^1]).IsEqualTo((byte)'\n');
            await Assert.That(CoverageSummary.Read(path).Totals.Lines).IsEqualTo(2);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Test]
    public async Task Counts_add_up_field_by_field()
    {
        var sum = CoverageCounts.Sum([new CoverageCounts(1, 2, 3, 4, 5, 6), new CoverageCounts(10, 20, 30, 40, 50, 60)]);

        await Assert.That(sum).IsEqualTo(new CoverageCounts(11, 22, 33, 44, 55, 66));
        await Assert.That(sum.MissedLines).IsEqualTo(11);
        await Assert.That(sum.MissedBranches).IsEqualTo(11);
    }
}
```

Delete `ScaffoldTests.cs`.

- [ ] **Step 2: Run the tests to verify they fail.**
  - Run: `dotnet test --project build/Build.Cake.Extensions.Tests`
  - Expected: a compile error, because `Build.Cake.Extensions.Coverage.Model` doesn't exist.

- [ ] **Step 3: Implement the shared helpers.**

`build/Build.Cake.Extensions/Invariant.cs`:

```csharp
using System.Globalization;

namespace Build.Cake.Extensions;

// Numbers in reports, comments and JSON never follow the machine's culture: a Swedish laptop must write 83.0%, not 83,0%.
internal static class Invariant
{
    public static string Format(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
```

`build/Build.Cake.Extensions/NameOrder.cs`:

```csharp
namespace Build.Cake.Extensions;

// How the PowerShell scripts compared names, kept in one place: Sort-Object sorts culture-aware and ignores case;
// hashtables look keys up ignoring case.
internal static class NameOrder
{
    public static StringComparer Sort { get; } = StringComparer.InvariantCultureIgnoreCase;

    public static StringComparer Lookup { get; } = StringComparer.OrdinalIgnoreCase;
}
```

- [ ] **Step 4: Implement the model.**

`build/Build.Cake.Extensions/Coverage/Model/CoverageCounts.cs`:

```csharp
namespace Build.Cake.Extensions.Coverage.Model;

// Line, branch and method counts of a class, a group of classes or a whole report. A method counts as covered
// ("called") once any of its lines ran.
public sealed record CoverageCounts(int CoveredLines, int Lines, int CoveredBranches, int Branches, int CoveredMethods, int Methods)
{
    public static CoverageCounts Zero { get; } = new(0, 0, 0, 0, 0, 0);

    public int MissedLines => Lines - CoveredLines;

    public int MissedBranches => Branches - CoveredBranches;

    public static CoverageCounts operator +(CoverageCounts left, CoverageCounts right) => new(
        left.CoveredLines + right.CoveredLines,
        left.Lines + right.Lines,
        left.CoveredBranches + right.CoveredBranches,
        left.Branches + right.Branches,
        left.CoveredMethods + right.CoveredMethods,
        left.Methods + right.Methods);

    public static CoverageCounts Sum(IEnumerable<CoverageCounts> counts) => counts.Aggregate(Zero, (total, next) => total + next);
}
```

`build/Build.Cake.Extensions/Coverage/Model/ClassCoverage.cs`:

```csharp
namespace Build.Cake.Extensions.Coverage.Model;

// One class in one file (partial classes appear once per file). File is relative to the product folder
// (src/<Assembly>/), or just the file name when the class lives elsewhere.
public sealed record ClassCoverage(string Assembly, string Name, string File, CoverageCounts Counts);
```

`build/Build.Cake.Extensions/Coverage/Model/CoverageSummary.cs`:

```csharp
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Build.Cake.Extensions.Coverage.Model;

public sealed record NamespaceSummary(string Name, CoverageCounts Counts);

// The counts a pull request is compared by: totals and per namespace. Written as artifacts/coverage/summary.json and
// kept by CI as the baseline of later runs, so the format is fixed: the PowerShell report's, which baselines from
// before the move to Cake still use.
public sealed record CoverageSummary(CoverageCounts Totals, IReadOnlyList<NamespaceSummary> Namespaces)
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public string ToJson()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("totals");
            WriteCounts(writer, name: null, Totals);
            writer.WriteStartArray("namespaces");
            foreach (var entry in Namespaces)
            {
                WriteCounts(writer, entry.Name, entry.Counts);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public void Write(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, ToJson() + "\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public static CoverageSummary Read(string path) => FromJson(File.ReadAllText(path));

    // PowerShell's ConvertTo-Json writes a one-element array as a bare object; both shapes are read.
    public static CoverageSummary FromJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var namespaces = root.TryGetProperty("namespaces", out var element) ? element : default;
        var entries = namespaces.ValueKind switch
        {
            JsonValueKind.Array => namespaces.EnumerateArray().ToList(),
            JsonValueKind.Object => [namespaces],
            _ => [],
        };

        return new CoverageSummary(
            ReadCounts(root.GetProperty("totals")),
            entries.Select(entry => new NamespaceSummary(entry.GetProperty("name").GetString() ?? "", ReadCounts(entry))).ToList());
    }

    private static void WriteCounts(Utf8JsonWriter writer, string? name, CoverageCounts counts)
    {
        writer.WriteStartObject();
        if (name is not null)
        {
            writer.WriteString("name", name);
        }

        writer.WriteNumber("coveredLines", counts.CoveredLines);
        writer.WriteNumber("lines", counts.Lines);
        writer.WriteNumber("coveredBranches", counts.CoveredBranches);
        writer.WriteNumber("branches", counts.Branches);
        writer.WriteNumber("coveredMethods", counts.CoveredMethods);
        writer.WriteNumber("methods", counts.Methods);
        writer.WriteEndObject();
    }

    private static CoverageCounts ReadCounts(JsonElement element) => new(
        element.GetProperty("coveredLines").GetInt32(),
        element.GetProperty("lines").GetInt32(),
        element.GetProperty("coveredBranches").GetInt32(),
        element.GetProperty("branches").GetInt32(),
        element.GetProperty("coveredMethods").GetInt32(),
        element.GetProperty("methods").GetInt32());
}
```

- [ ] **Step 5: Run the tests to verify they pass.**
  - Run: `dotnet test --project build/Build.Cake.Extensions.Tests`
  - Expected: all tests pass.
  - If `Writes_a_summary_exactly_as_the_PowerShell_report_did` fails on whitespace, compare the strings: PowerShell 7's
    `ConvertTo-Json` indents by two spaces and writes `"key": value`. Adjust the writer, not the golden file.

- [ ] **Step 6: Commit.**

```bash
git add build/Build.Cake.Extensions build/Build.Cake.Extensions.Tests
git commit -m "Port the coverage summary model and its JSON format" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Cobertura path normalization and reading

**Files:**
- Create: `build/Build.Cake.Extensions/Coverage/Cobertura/CoberturaPathNormalizer.cs`
- Create: `build/Build.Cake.Extensions/Coverage/Cobertura/CoberturaReader.cs`
- Test: `build/Build.Cake.Extensions.Tests/Coverage/Cobertura/CoberturaPathNormalizerTests.cs`
- Test: `build/Build.Cake.Extensions.Tests/Coverage/Cobertura/CoberturaReaderTests.cs`

**Interfaces:**
- Consumes: `ClassCoverage`, `CoverageCounts` (Task 3).
- Produces:
  - `static class CoberturaPathNormalizer`, with:
    - `string ToRepoRelativePath(string path)`
    - `string ToRepoRelative(string coberturaXml)`
    - `int NormalizeFiles(string sourceDirectory, string targetDirectory)`. It returns the number of files and throws
      `InvalidOperationException` when there are none.
  - `static class CoberturaReader`: `IReadOnlyList<ClassCoverage> Read(string coberturaXml)`

- [ ] **Step 1: Write the failing normalizer tests.**

```csharp
using Build.Cake.Extensions.Coverage.Cobertura;

namespace Build.Cake.Extensions.Tests.Coverage.Cobertura;

public class CoberturaPathNormalizerTests
{
    [Test]
    [Arguments(@"D:\a\Typstium\Typstium\src\Typstium\Compilation\Compiler.cs", "src/Typstium/Compilation/Compiler.cs")]
    [Arguments("/home/runner/work/Typstium/Typstium/src/Typstium/Compilation/Compiler.cs", "src/Typstium/Compilation/Compiler.cs")]
    [Arguments("/home/runner/work/Typstium/Typstium/tests/Typstium.Testing/Fake.cs", "tests/Typstium.Testing/Fake.cs")]
    [Arguments("/work/src/old/tests/Typstium.Testing/Fake.cs", "tests/Typstium.Testing/Fake.cs")]
    [Arguments(@"C:\elsewhere\Generated.cs", "C:/elsewhere/Generated.cs")]
    public async Task Makes_paths_repo_relative_from_the_last_src_or_tests_folder(string path, string expected)
    {
        await Assert.That(CoberturaPathNormalizer.ToRepoRelativePath(path)).IsEqualTo(expected);
    }

    [Test]
    public async Task Rewrites_every_filename_attribute_and_nothing_else()
    {
        const string xml = """<class name="A" filename="D:\a\r\src\P\A.cs"><source>D:\a\r\src</source></class><class name="B" filename="/home/x/src/P/B.cs" />""";

        var normalized = CoberturaPathNormalizer.ToRepoRelative(xml);

        await Assert.That(normalized).IsEqualTo("""<class name="A" filename="src/P/A.cs"><source>D:\a\r\src</source></class><class name="B" filename="src/P/B.cs" />""");
    }

    [Test]
    public async Task Normalizes_every_raw_file_into_the_target_folder()
    {
        var root = Path.Combine(Path.GetTempPath(), "normalize-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "raw");
        var target = Path.Combine(root, "relative");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "a.cobertura.xml"), """<class filename="C:\r\src\P\A.cs" />""");
        File.WriteAllText(Path.Combine(source, "ignored.txt"), "not coverage");
        try
        {
            var count = CoberturaPathNormalizer.NormalizeFiles(source, target);

            await Assert.That(count).IsEqualTo(1);
            await Assert.That(File.ReadAllText(Path.Combine(target, "a.cobertura.xml"))).IsEqualTo("""<class filename="src/P/A.cs" />""");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task No_raw_files_is_an_error_that_says_what_to_do()
    {
        var empty = Path.Combine(Path.GetTempPath(), "empty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(empty);
        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() => CoberturaPathNormalizer.NormalizeFiles(empty, Path.Combine(empty, "out")));

            await Assert.That(exception.Message).Contains(empty);
            await Assert.That(exception.Message).Contains("--coverage");
        }
        finally
        {
            Directory.Delete(empty, recursive: true);
        }
    }

    [Test]
    public async Task A_missing_raw_folder_is_the_same_error()
    {
        var missing = Path.Combine(Path.GetTempPath(), "missing-" + Guid.NewGuid().ToString("N"));

        var exception = Assert.Throws<InvalidOperationException>(() => CoberturaPathNormalizer.NormalizeFiles(missing, Path.Combine(missing, "out")));

        await Assert.That(exception.Message).Contains("--coverage");
    }
}
```

- [ ] **Step 2: Write the failing reader tests.**

```csharp
using Build.Cake.Extensions.Coverage.Cobertura;
using Build.Cake.Extensions.Coverage.Model;

namespace Build.Cake.Extensions.Tests.Coverage.Cobertura;

public class CoberturaReaderTests
{
    private static IReadOnlyList<ClassCoverage> EdgeCases() => CoberturaReader.Read(Fixture.Read("EdgeCases", "Cobertura.xml"));

    [Test]
    public async Task Counts_lines_branches_and_called_methods()
    {
        var widget = EdgeCases().Single(c => c.Name == "Alpha.Api.Widget");

        await Assert.That(widget.Assembly).IsEqualTo("Alpha");
        await Assert.That(widget.File).IsEqualTo("Api/Widget.cs");
        await Assert.That(widget.Counts).IsEqualTo(new CoverageCounts(CoveredLines: 2, Lines: 4, CoveredBranches: 1, Branches: 4, CoveredMethods: 1, Methods: 2));
    }

    [Test]
    public async Task Keeps_one_entry_per_class_and_file()
    {
        var parser = EdgeCases().Where(c => c.Name == "Alpha.Internal.Parser").Select(c => c.File).ToList();

        await Assert.That(parser).IsEquivalentTo(["Internal/Parser.cs", "Internal/Parser.Tokens.cs"]);
    }

    [Test]
    [Arguments("Beta.Thing", "Thing.cs")]
    [Arguments("Alpha.Generated", "Generated.cs")]
    [Arguments("Alpha.Root", "Root.cs")]
    public async Task Makes_files_relative_to_the_product_folder_or_keeps_the_file_name(string name, string file)
    {
        await Assert.That(EdgeCases().Single(c => c.Name == name).File).IsEqualTo(file);
    }

    [Test]
    public async Task A_class_without_lines_has_zero_counts()
    {
        await Assert.That(EdgeCases().Single(c => c.Name == "Alpha.Empty").Counts).IsEqualTo(CoverageCounts.Zero);
    }

    [Test]
    public async Task Reads_the_real_report()
    {
        var classes = CoberturaReader.Read(Fixture.Read("Real", "Cobertura.xml"));
        var summary = Build.Cake.Extensions.Coverage.Model.CoverageSummary.FromJson(Fixture.Read("Real", "expected", "summary.json"));

        await Assert.That(CoverageCounts.Sum(classes.Select(c => c.Counts))).IsEqualTo(summary.Totals);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail.**
  - Run: `dotnet test --project build/Build.Cake.Extensions.Tests`
  - Expected: a compile error, because `Build.Cake.Extensions.Coverage.Cobertura` doesn't exist.

- [ ] **Step 4: Implement `CoberturaPathNormalizer`.**

```csharp
using System.Text.RegularExpressions;

namespace Build.Cake.Extensions.Coverage.Cobertura;

// Each machine records absolute paths (D:\... on Windows, /home/runner/... on Ubuntu). Made repo-relative, the same
// file from several machines is one file to ReportGenerator, so a line counts as covered when any run hit it,
// instead of being counted once per machine.
public static class CoberturaPathNormalizer
{
    private static readonly Regex FileName = new("filename=\"([^\"]*)\"", RegexOptions.Compiled);

    public static string ToRepoRelativePath(string path)
    {
        var normalized = path.Replace('\\', '/');
        var index = Math.Max(normalized.LastIndexOf("/src/", StringComparison.Ordinal), normalized.LastIndexOf("/tests/", StringComparison.Ordinal));
        return index >= 0 ? normalized[(index + 1)..] : normalized;
    }

    public static string ToRepoRelative(string coberturaXml) =>
        FileName.Replace(coberturaXml, match => $"filename=\"{ToRepoRelativePath(match.Groups[1].Value)}\"");

    // Copies every *.cobertura.xml of the source folder into the target folder, normalized; returns how many.
    public static int NormalizeFiles(string sourceDirectory, string targetDirectory)
    {
        var files = Directory.Exists(sourceDirectory) ? Directory.GetFiles(sourceDirectory, "*.cobertura.xml") : [];
        if (files.Length == 0)
        {
            throw new InvalidOperationException(
                $"No *.cobertura.xml files in {sourceDirectory}. Run the tests with --coverage first, or copy CI's coverage artifacts there.");
        }

        Directory.CreateDirectory(targetDirectory);
        foreach (var file in files)
        {
            File.WriteAllText(Path.Combine(targetDirectory, Path.GetFileName(file)), ToRepoRelative(File.ReadAllText(file)));
        }

        return files.Length;
    }
}
```

- [ ] **Step 5: Implement `CoberturaReader`.**

```csharp
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Build.Cake.Extensions.Coverage.Model;

namespace Build.Cake.Extensions.Coverage.Cobertura;

// Reads a Cobertura report (ReportGenerator's merged one) into one entry per class and file.
public static class CoberturaReader
{
    private static readonly Regex Condition = new(@"\((\d+)/(\d+)\)", RegexOptions.Compiled);

    public static IReadOnlyList<ClassCoverage> Read(string coberturaXml)
    {
        var document = XDocument.Parse(coberturaXml);
        var result = new List<ClassCoverage>();
        foreach (var package in document.Root!.Elements("packages").Elements("package"))
        {
            var assembly = Attribute(package, "name");
            foreach (var type in package.Elements("classes").Elements("class"))
            {
                result.Add(new ClassCoverage(assembly, Attribute(type, "name"), RelativeToProduct(Attribute(type, "filename"), assembly), Count(type)));
            }
        }

        return result;
    }

    private static CoverageCounts Count(XElement type)
    {
        int lines = 0, coveredLines = 0, branches = 0, coveredBranches = 0;
        foreach (var line in type.Elements("lines").Elements("line"))
        {
            lines++;
            if (long.TryParse(Attribute(line, "hits"), out var hits) && hits > 0)
            {
                coveredLines++;
            }

            var condition = Condition.Match(Attribute(line, "condition-coverage"));
            if (condition.Success)
            {
                coveredBranches += int.Parse(condition.Groups[1].Value);
                branches += int.Parse(condition.Groups[2].Value);
            }
        }

        var methods = type.Elements("methods").Elements("method").ToList();
        var calledMethods = methods.Count(method => method.Elements("lines").Elements("line").Any(line => line.Attribute("hits") is { Value: not "0" }));
        return new CoverageCounts(coveredLines, lines, coveredBranches, branches, calledMethods, methods.Count);
    }

    // Paths are absolute, or repo-relative (src/<assembly>/...) when merged from several machines.
    private static string RelativeToProduct(string fileName, string assembly)
    {
        var file = fileName.Replace('\\', '/');
        var marker = $"src/{assembly}/";
        if (file.StartsWith(marker, StringComparison.Ordinal))
        {
            return file[marker.Length..];
        }

        var index = file.IndexOf('/' + marker, StringComparison.Ordinal);
        return index >= 0 ? file[(index + 1 + marker.Length)..] : file[(file.LastIndexOf('/') + 1)..];
    }

    private static string Attribute(XElement element, string name) => element.Attribute(name)?.Value ?? "";
}
```

- [ ] **Step 6: Run the tests to verify they pass.**
  - Run: `dotnet test --project build/Build.Cake.Extensions.Tests`
  - Expected: all tests pass.

- [ ] **Step 7: Commit.**

```bash
git add build/Build.Cake.Extensions build/Build.Cake.Extensions.Tests
git commit -m "Port Cobertura path normalization and reading" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Public API catalog and namespace resolution

**Files:**
- Create: `build/Build.Cake.Extensions/Coverage/Reporting/PublicApiCatalog.cs`, `NamespaceResolver.cs`
- Test: `build/Build.Cake.Extensions.Tests/Coverage/Reporting/PublicApiCatalogTests.cs`, `NamespaceResolverTests.cs`

**Interfaces:**
- Produces:
  - `static class PublicApiCatalog`, with:
    - `IReadOnlySet<string> Parse(IEnumerable<string> lines)`
    - `IReadOnlySet<string>? Read(string productDirectory)`, which returns null when the folder has no
      `PublicAPI.*.txt`
  - `static class NamespaceResolver`, with:
    - `string TypeName(string className)`, the name without generic arguments
    - `string Namespace(string className, IReadOnlySet<string> classNames)`
    - `string Area(string relativeFile)`

- [ ] **Step 1: Write the failing tests.**

`PublicApiCatalogTests.cs`:

```csharp
using Build.Cake.Extensions.Coverage.Reporting;

namespace Build.Cake.Extensions.Tests.Coverage.Reporting;

public class PublicApiCatalogTests
{
    [Test]
    public async Task Public_types_are_bare_type_lines_without_generic_arguments()
    {
        var types = PublicApiCatalog.Parse(["#nullable enable", "", "  Alpha.Api.Widget  ", "Alpha.Api.Widget.Run() -> void", "Alpha.Api.Box<T>", "Alpha.Api.Box<T>.Get() -> T"]);

        await Assert.That(types.Order(StringComparer.Ordinal)).IsEquivalentTo(["Alpha.Api.Box", "Alpha.Api.Widget"]);
    }

    [Test]
    public async Task Reads_every_PublicAPI_file_of_a_product()
    {
        var types = PublicApiCatalog.Read(Fixture.Path("EdgeCases", "src", "Alpha"));

        await Assert.That(types).IsNotNull();
        await Assert.That(types!.Contains("Alpha.Root")).IsTrue();
    }

    [Test]
    public async Task A_product_without_PublicAPI_files_has_no_catalog()
    {
        await Assert.That(PublicApiCatalog.Read(Fixture.Path("EdgeCases", "src", "Beta"))).IsNull();
    }
}
```

`NamespaceResolverTests.cs`:

```csharp
using Build.Cake.Extensions.Coverage.Reporting;

namespace Build.Cake.Extensions.Tests.Coverage.Reporting;

public class NamespaceResolverTests
{
    private static readonly HashSet<string> Classes = ["Alpha.Api.Widget", "Alpha.Api.Widget.Inner", "Alpha.Root", "Alpha.Internal.Cache", "Solo"];

    [Test]
    [Arguments("Alpha.Api.Widget", "Alpha.Api")]
    [Arguments("Alpha.Api.Widget.Inner", "Alpha.Api")]
    [Arguments("Alpha.Root", "Alpha")]
    [Arguments("Alpha.Internal.Cache<TKey, TValue>", "Alpha.Internal")]
    [Arguments("Solo", "Solo")]
    [Arguments("Not.In.Report", "Not.In")]
    public async Task Namespace_is_the_prefix_before_the_first_segment_that_is_a_class(string className, string expected)
    {
        await Assert.That(NamespaceResolver.Namespace(className, Classes)).IsEqualTo(expected);
    }

    [Test]
    [Arguments("Api/Widget.cs", "Api")]
    [Arguments("Internal/Deep/Parser.cs", "Internal")]
    [Arguments("Root.cs", "(root)")]
    public async Task Area_is_the_top_level_folder_or_root(string file, string expected)
    {
        await Assert.That(NamespaceResolver.Area(file)).IsEqualTo(expected);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail.**
  - Run: `dotnet test --project build/Build.Cake.Extensions.Tests`
  - Expected: a compile error, because `Build.Cake.Extensions.Coverage.Reporting` doesn't exist.

- [ ] **Step 3: Implement both.**

`PublicApiCatalog.cs`:

```csharp
namespace Build.Cake.Extensions.Coverage.Reporting;

// The public types of a product: bare type lines (no "->") of its PublicAPI.*.txt files, which the PublicAPI analyzer
// keeps complete. Without such files there is no catalog, and the product's classes show as "Unknown" rather than as a
// guess.
public static class PublicApiCatalog
{
    public static IReadOnlySet<string>? Read(string productDirectory)
    {
        var files = Directory.Exists(productDirectory) ? Directory.GetFiles(productDirectory, "PublicAPI.*.txt") : [];
        return files.Length == 0 ? null : Parse(files.SelectMany(File.ReadLines));
    }

    public static IReadOnlySet<string> Parse(IEnumerable<string> lines)
    {
        var types = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            var entry = line.Trim();
            if (entry.Length > 0 && !entry.StartsWith('#') && !entry.Contains("->", StringComparison.Ordinal))
            {
                types.Add(NamespaceResolver.TypeName(entry));
            }
        }

        return types;
    }
}
```

`NamespaceResolver.cs`:

```csharp
namespace Build.Cake.Extensions.Coverage.Reporting;

public static class NamespaceResolver
{
    public static string TypeName(string className) => className.Split('<')[0];

    // The prefix before the first segment that is itself a class in the report, so nested types
    // (Typstium.Capabilities.TypstCliProfileFactory.ProfileKey) land in their outer type's namespace.
    public static string Namespace(string className, IReadOnlySet<string> classNames)
    {
        var parts = TypeName(className).Split('.');
        for (var count = 1; count <= parts.Length; count++)
        {
            if (classNames.Contains(string.Join('.', parts[..count])))
            {
                return count > 1 ? string.Join('.', parts[..(count - 1)]) : parts[0];
            }
        }

        return parts.Length > 1 ? string.Join('.', parts[..^1]) : parts[0];
    }

    // The top-level folder under the product folder; files directly in it form "(root)".
    public static string Area(string relativeFile) => relativeFile.Contains('/') ? relativeFile.Split('/')[0] : "(root)";
}
```

- [ ] **Step 4: Run the tests to verify they pass.**
  - Run: `dotnet test --project build/Build.Cake.Extensions.Tests`
  - Expected: all tests pass.

- [ ] **Step 5: Commit.**

```bash
git add build/Build.Cake.Extensions build/Build.Cake.Extensions.Tests
git commit -m "Port public API and namespace resolution for coverage grouping" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Coverage report builder

**Files:**
- Create: `build/Build.Cake.Extensions/Coverage/Reporting/CoverageTable.cs`, `CoverageReport.cs`, `CoverageReportBuilder.cs`
- Test: `build/Build.Cake.Extensions.Tests/Coverage/Reporting/CoverageReportBuilderTests.cs`
- Modify: `build/Build.Cake.Extensions.Tests/Fixture.cs` (add `BuildReport`)

**Interfaces:**
- Consumes: `ClassCoverage`, `CoverageCounts`, `CoverageSummary`, `NamespaceSummary` (Task 3); `CoberturaReader`
  (Task 4); `PublicApiCatalog`, `NamespaceResolver` (Task 5); `Invariant`, `NameOrder`.
- Produces:
  - `enum CoverageRowKind { Group, Assembly, Total, Item }`
  - `record CoverageRow(CoverageRowKind Kind, string Name, IReadOnlyList<string> Cells)`
  - `record CoverageTable(string Title, IReadOnlyList<string> Headers, IReadOnlyList<CoverageRow> Rows)`
  - `record CoverageReport(IReadOnlyList<CoverageTable> Tables, CoverageSummary Summary)`, with
    `const string Legend`
  - `static class CoverageReportBuilder`:
    `CoverageReport Build(IReadOnlyList<ClassCoverage> classes, IReadOnlyDictionary<string, IReadOnlySet<string>?> publicTypes, int topClasses)`
  - `Fixture.BuildReport(string fixture) : CoverageReport`

- [ ] **Step 1: Add the fixture helper.** Append to the `Fixture` class:

```csharp
    // The fixture's Cobertura.xml grouped exactly as the CoverageReport target does, with PublicAPI files from the
    // fixture's src/<assembly>/ folders.
    public static Build.Cake.Extensions.Coverage.Reporting.CoverageReport BuildReport(string fixture)
    {
        var classes = Build.Cake.Extensions.Coverage.Cobertura.CoberturaReader.Read(Read(fixture, "Cobertura.xml"));
        var publicTypes = classes.Select(c => c.Assembly).Distinct(StringComparer.Ordinal)
            .ToDictionary(assembly => assembly, assembly => Build.Cake.Extensions.Coverage.Reporting.PublicApiCatalog.Read(Path(fixture, "src", assembly)));
        return Build.Cake.Extensions.Coverage.Reporting.CoverageReportBuilder.Build(classes, publicTypes, topClasses: 10);
    }
```

- [ ] **Step 2: Write the failing tests.**

```csharp
using System.Globalization;
using Build.Cake.Extensions.Coverage.Reporting;

namespace Build.Cake.Extensions.Tests.Coverage.Reporting;

public class CoverageReportBuilderTests
{
    [Test]
    [Arguments("Real")]
    [Arguments("EdgeCases")]
    public async Task Summary_matches_the_PowerShell_report(string fixture)
    {
        var report = Fixture.BuildReport(fixture);

        await Assert.That(report.Summary.ToJson() + "\n").IsEqualTo(Fixture.Read(fixture, "expected", "summary.json"));
    }

    [Test]
    public async Task Builds_the_four_views_in_order()
    {
        var titles = Fixture.BuildReport("EdgeCases").Tables.Select(t => t.Title).ToList();

        await Assert.That(titles).IsEquivalentTo(
            ["Coverage by visibility", "Coverage by namespace", "Coverage by area (top-level folder)", "Top 10 classes by missed lines"],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Visibility_follows_the_PublicAPI_files_and_says_when_there_are_none()
    {
        var rows = Fixture.BuildReport("EdgeCases").Tables[0].Rows;
        var names = rows.Select(r => (r.Kind, r.Name)).ToList();

        await Assert.That(names).Contains((CoverageRowKind.Group, "Public API"));
        await Assert.That(names).Contains((CoverageRowKind.Group, "Internal"));
        await Assert.That(names).Contains((CoverageRowKind.Group, "Unknown (no PublicAPI files)"));
        await Assert.That(names).Contains((CoverageRowKind.Assembly, "Beta (assembly)"));
        await Assert.That(rows[^1].Kind).IsEqualTo(CoverageRowKind.Total);
    }

    [Test]
    public async Task Top_classes_only_lists_classes_with_missed_lines()
    {
        var top = Fixture.BuildReport("EdgeCases").Tables[3];

        await Assert.That(top.Headers).IsEquivalentTo(["Class", "Missed", "Gap", "Line", "File"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(top.Rows.All(r => r.Kind == CoverageRowKind.Item && int.Parse(r.Cells[0], CultureInfo.InvariantCulture) > 0)).IsTrue();
        await Assert.That(top.Rows.Any(r => r.Name == "Alpha.Root")).IsFalse();
    }

    [Test]
    public async Task Zero_totals_show_dashes_instead_of_dividing_by_zero()
    {
        var empty = new Build.Cake.Extensions.Coverage.Model.ClassCoverage("Alpha", "Alpha.Empty", "Empty.cs", Build.Cake.Extensions.Coverage.Model.CoverageCounts.Zero);

        var report = CoverageReportBuilder.Build([empty], new Dictionary<string, IReadOnlySet<string>?>(), topClasses: 10);
        var total = report.Tables[0].Rows[^1];

        await Assert.That(total.Cells).IsEquivalentTo(["0", "-", "-", "-", "-"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(report.Tables[3].Rows).IsEmpty();
    }

    [Test]
    public async Task Percentages_use_a_point_on_a_Swedish_machine()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("sv-SE");
        try
        {
            var cells = Fixture.BuildReport("EdgeCases").Tables[0].Rows[^1].Cells;

            await Assert.That(string.Join(" ", cells)).DoesNotContain(",");
            await Assert.That(cells[2]).Contains(".");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail.**
  - Run: `dotnet test --project build/Build.Cake.Extensions.Tests`
  - Expected: a compile error, because `CoverageReportBuilder` doesn't exist.

- [ ] **Step 4: Implement the table types.**

`CoverageTable.cs`:

```csharp
namespace Build.Cake.Extensions.Coverage.Reporting;

public enum CoverageRowKind
{
    // A group within an assembly (a visibility, namespace or area).
    Group,

    // An assembly's subtotal, after its groups.
    Assembly,

    // The total of all assemblies, last.
    Total,

    // One class, in the top-classes view.
    Item,
}

public sealed record CoverageRow(CoverageRowKind Kind, string Name, IReadOnlyList<string> Cells);

// Headers[0] names the rows; Cells line up with Headers[1..]. The first two cell columns are right-aligned numbers.
public sealed record CoverageTable(string Title, IReadOnlyList<string> Headers, IReadOnlyList<CoverageRow> Rows);
```

`CoverageReport.cs`:

```csharp
using Build.Cake.Extensions.Coverage.Model;

namespace Build.Cake.Extensions.Coverage.Reporting;

public sealed record CoverageReport(IReadOnlyList<CoverageTable> Tables, CoverageSummary Summary)
{
    public const string Legend = "Sorted by missed lines. Gap: share of all missed lines. Method (called): methods entered at least once.";
}
```

- [ ] **Step 5: Implement `CoverageReportBuilder`.**

```csharp
using Build.Cake.Extensions.Coverage.Model;

namespace Build.Cake.Extensions.Coverage.Reporting;

// Four views of the same numbers, all sorted by missed (uncovered) lines so the biggest contributor to the gap comes
// first:
//   1. Visibility: the public API against internal code.
//   2. Namespace: the public API (the root namespace) and each internal component.
//   3. Area: each top-level folder of a product, so a feature's public and internal types share one row.
//   4. Top classes: the classes (per file) with the most missed lines, pointing at the code to test next.
// Gap is a row's share of all missed lines.
public static class CoverageReportBuilder
{
    private static readonly string[] SummaryHeaders = ["Missed", "Gap", "Line", "Branch", "Method (called)"];

    private sealed record Row(CoverageRowKind Kind, string Name, CoverageCounts Counts);

    public static CoverageReport Build(IReadOnlyList<ClassCoverage> classes, IReadOnlyDictionary<string, IReadOnlySet<string>?> publicTypes, int topClasses)
    {
        var total = CoverageCounts.Sum(classes.Select(c => c.Counts));
        var totalMissed = total.MissedLines;

        var byVisibility = GroupedRows(classes, (type, _) => Visibility(type, publicTypes.GetValueOrDefault(type.Assembly)));
        var byNamespace = GroupedRows(classes, (type, classNames) => NamespaceResolver.Namespace(type.Name, classNames));
        var byArea = GroupedRows(classes, (type, _) => NamespaceResolver.Area(type.File));
        var worst = ByGap(classes.Where(c => c.Counts.MissedLines > 0), c => c.Counts, c => c.Name).Take(topClasses);

        CoverageTable Summary(string title, string rowHeader, IEnumerable<Row> rows) => new(
            title,
            [rowHeader, .. SummaryHeaders],
            rows.Select(row => new CoverageRow(row.Kind, row.Name, SummaryCells(row.Counts, totalMissed))).ToList());

        var tables = new List<CoverageTable>
        {
            Summary("Coverage by visibility", "Visibility", byVisibility),
            Summary("Coverage by namespace", "Namespace", byNamespace),
            Summary("Coverage by area (top-level folder)", "Area", byArea),
            new(
                Invariant.Format($"Top {topClasses} classes by missed lines"),
                ["Class", "Missed", "Gap", "Line", "File"],
                worst.Select(c => new CoverageRow(
                    CoverageRowKind.Item,
                    c.Name,
                    [Invariant.Format($"{c.Counts.MissedLines}"), Gap(c.Counts.MissedLines, totalMissed), Rate(c.Counts.CoveredLines, c.Counts.Lines), c.File])).ToList()),
        };

        var namespaces = byNamespace.Where(row => row.Kind == CoverageRowKind.Group).Select(row => new NamespaceSummary(row.Name, row.Counts)).ToList();
        return new CoverageReport(tables, new CoverageSummary(total, namespaces));
    }

    private static string Visibility(ClassCoverage type, IReadOnlySet<string>? publicTypes) =>
        publicTypes is null ? "Unknown (no PublicAPI files)"
        : publicTypes.Contains(NamespaceResolver.TypeName(type.Name)) ? "Public API"
        : "Internal";

    // Rows per assembly (biggest gap first): its groups (biggest gap first), then a subtotal; finally the total.
    private static List<Row> GroupedRows(IReadOnlyList<ClassCoverage> classes, Func<ClassCoverage, IReadOnlySet<string>, string> groupOf)
    {
        var rows = new List<Row>();
        var assemblies = classes
            .GroupBy(c => c.Assembly, NameOrder.Lookup)
            .Select(g => (Name: g.First().Assembly, Counts: CoverageCounts.Sum(g.Select(c => c.Counts)), Classes: g.ToList()));

        foreach (var assembly in ByGap(assemblies, a => a.Counts, a => a.Name))
        {
            var classNames = assembly.Classes.Select(c => NamespaceResolver.TypeName(c.Name)).ToHashSet(StringComparer.Ordinal);
            var groups = assembly.Classes
                .GroupBy(c => groupOf(c, classNames), NameOrder.Lookup)
                .Select(g => new Row(CoverageRowKind.Group, g.Key, CoverageCounts.Sum(g.Select(c => c.Counts))));

            rows.AddRange(ByGap(groups, g => g.Counts, g => g.Name));
            rows.Add(new Row(CoverageRowKind.Assembly, $"{assembly.Name} (assembly)", assembly.Counts));
        }

        rows.Add(new Row(CoverageRowKind.Total, "Total", CoverageCounts.Sum(classes.Select(c => c.Counts))));
        return rows;
    }

    // Biggest contributor to the gap first: most missed lines, then most missed branches, then name.
    private static IEnumerable<T> ByGap<T>(IEnumerable<T> items, Func<T, CoverageCounts> counts, Func<T, string> name) =>
        items.OrderByDescending(item => counts(item).MissedLines)
            .ThenByDescending(item => counts(item).MissedBranches)
            .ThenBy(name, NameOrder.Sort);

    private static IReadOnlyList<string> SummaryCells(CoverageCounts counts, int totalMissed) =>
    [
        Invariant.Format($"{counts.MissedLines}"),
        Gap(counts.MissedLines, totalMissed),
        Rate(counts.CoveredLines, counts.Lines),
        Rate(counts.CoveredBranches, counts.Branches),
        Rate(counts.CoveredMethods, counts.Methods),
    ];

    // "  83.0%  (830/1000)": the percentage padded to six characters, so console columns line up.
    private static string Rate(int covered, int total) =>
        total == 0 ? "-" : Invariant.Format($"{Invariant.Format($"{100.0 * covered / total:0.0}%"),6}  ({covered}/{total})");

    private static string Gap(int missed, int totalMissed) =>
        totalMissed == 0 ? "-" : Invariant.Format($"{100.0 * missed / totalMissed:0.0}%");
}
```

- [ ] **Step 6: Run the tests to verify they pass.**
  - Run: `dotnet test --project build/Build.Cake.Extensions.Tests`
  - Expected: all tests pass.
  - If a `Summary_matches...` case fails on order alone, the namespace rows differ in a name tie-break. Compare the
    two lists. Change `NameOrder.Sort` only if the golden file shows `Sort-Object` ordering otherwise, and note it in
    the commit message.

- [ ] **Step 7: Commit.**

```bash
git add build/Build.Cake.Extensions build/Build.Cake.Extensions.Tests
git commit -m "Port the coverage report's four views" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Markdown rendering for the job summary

**Files:**
- Create: `build/Build.Cake.Extensions/Coverage/Rendering/MarkdownTableRenderer.cs`
- Test: `build/Build.Cake.Extensions.Tests/Coverage/Rendering/MarkdownTableRendererTests.cs`

**Interfaces:**
- Consumes: `CoverageReport`, `CoverageTable`, `CoverageRow`, `CoverageRowKind` (Task 6).
- Produces: `static class MarkdownTableRenderer`, with:
  - `string RenderTable(CoverageTable table)`
  - `string RenderJobSummary(CoverageReport report, string heading)`

  Both always use `\n`.

- [ ] **Step 1: Write the failing tests.**

```csharp
using System.Globalization;
using Build.Cake.Extensions.Coverage.Rendering;
using Build.Cake.Extensions.Coverage.Reporting;

namespace Build.Cake.Extensions.Tests.Coverage.Rendering;

public class MarkdownTableRendererTests
{
    private const string Heading = "### Code coverage (unit and integration tests)";

    [Test]
    [Arguments("Real")]
    [Arguments("EdgeCases")]
    public async Task Job_summary_matches_the_PowerShell_report(string fixture)
    {
        var markdown = MarkdownTableRenderer.RenderJobSummary(Fixture.BuildReport(fixture), Heading);

        // Add-Content (PowerShell) and SetStepSummary (Cake) both end the appended text with a newline.
        await Assert.That(markdown + "\n").IsEqualTo(Fixture.Read(fixture, "expected", "job-summary.md"));
    }

    [Test]
    public async Task Rows_are_decorated_by_kind_and_cells_lose_their_padding()
    {
        var table = new CoverageTable("T", ["Name", "Missed", "Gap", "Line", "File"],
        [
            new(CoverageRowKind.Group, "Api", ["1", "50.0%", "  50.0%  (1/2)", "x"]),
            new(CoverageRowKind.Item, "Alpha.Box<T>", ["1", "50.0%", "  50.0%  (1/2)", "Api/Box.cs"]),
            new(CoverageRowKind.Total, "Total", ["2", "100.0%", "  50.0%  (2/4)", "-"]),
        ]);

        var markdown = MarkdownTableRenderer.RenderTable(table);

        await Assert.That(markdown).IsEqualTo(
            "| Name | Missed | Gap | Line | File |\n" +
            "| --- | ---: | ---: | ---: | --- |\n" +
            "| &nbsp;&nbsp;Api | 1 | 50.0% | 50.0% (1/2) | x |\n" +
            "| `Alpha.Box<T>` | 1 | 50.0% | 50.0% (1/2) | Api/Box.cs |\n" +
            "| **Total** | **2** | **100.0%** | **50.0% (2/4)** | **-** |\n");
    }

    [Test]
    public async Task Uses_newlines_only_whatever_the_platform()
    {
        await Assert.That(MarkdownTableRenderer.RenderJobSummary(Fixture.BuildReport("EdgeCases"), Heading)).DoesNotContain("\r");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail.**
  - Run: `dotnet test --project build/Build.Cake.Extensions.Tests`
  - Expected: a compile error, because `MarkdownTableRenderer` doesn't exist.

- [ ] **Step 3: Implement.**

```csharp
using System.Text;
using System.Text.RegularExpressions;
using Build.Cake.Extensions.Coverage.Reporting;

namespace Build.Cake.Extensions.Coverage.Rendering;

// Markdown for GitHub (job summaries, comments). Lines end in \n on every platform, so output is the same wherever
// the build runs.
public static class MarkdownTableRenderer
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    // The first view open, the others folded into <details>, then the legend.
    public static string RenderJobSummary(CoverageReport report, string heading)
    {
        var markdown = new StringBuilder();
        markdown.Append(heading).Append('\n');
        markdown.Append('\n');
        markdown.Append(RenderTable(report.Tables[0])).Append('\n');
        foreach (var table in report.Tables.Skip(1))
        {
            markdown.Append("<details><summary>").Append(table.Title).Append("</summary>\n");
            markdown.Append('\n');
            markdown.Append(RenderTable(table)).Append('\n');
            markdown.Append("</details>\n");
            markdown.Append('\n');
        }

        markdown.Append(CoverageReport.Legend).Append('\n');
        return markdown.ToString();
    }

    public static string RenderTable(CoverageTable table)
    {
        var markdown = new StringBuilder();
        markdown.Append("| ").Append(string.Join(" | ", table.Headers)).Append(" |\n");
        markdown.Append("| --- |");
        foreach (var header in table.Headers.Skip(1))
        {
            markdown.Append(header == "File" ? " --- |" : " ---: |");
        }

        markdown.Append('\n');
        foreach (var row in table.Rows)
        {
            var name = row.Kind switch
            {
                CoverageRowKind.Group => $"&nbsp;&nbsp;{row.Name}",
                CoverageRowKind.Item => $"`{row.Name}`",
                _ => $"**{row.Name}**",
            };
            var bold = row.Kind is CoverageRowKind.Assembly or CoverageRowKind.Total;
            var values = row.Cells.Select(cell => Whitespace.Replace(cell, " ").Trim()).Select(value => bold ? $"**{value}**" : value);
            markdown.Append("| ").Append(name).Append(" | ").Append(string.Join(" | ", values)).Append(" |\n");
        }

        return markdown.ToString();
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass.**
  - Run: `dotnet test --project build/Build.Cake.Extensions.Tests`
  - Expected: all tests pass.

- [ ] **Step 5: Commit.**

```bash
git add build/Build.Cake.Extensions build/Build.Cake.Extensions.Tests
git commit -m "Port the coverage job summary's Markdown" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Spectre.Console tables for the console

**Files:**
- Create: `build/Build.Cake.Extensions/Coverage/Rendering/SpectreTableRenderer.cs`
- Test: `build/Build.Cake.Extensions.Tests/Coverage/Rendering/SpectreTableRendererTests.cs`

**Interfaces:**
- Consumes: `CoverageReport`, `CoverageTable`, `CoverageRowKind` (Task 6).
- Produces: `static class SpectreTableRenderer`, with:
  - `const int MinimumWidth = 160`
  - `void Render(IAnsiConsole console, CoverageReport report)`
  - `Table Build(CoverageTable table)`
  - `IAnsiConsole CreateConsole()`, which uses at least `MinimumWidth` columns when output is redirected

- [ ] **Step 1: Write the failing tests.**

```csharp
using Build.Cake.Extensions.Coverage.Rendering;
using Spectre.Console.Testing;

namespace Build.Cake.Extensions.Tests.Coverage.Rendering;

public class SpectreTableRendererTests
{
    [Test]
    public async Task Renders_every_view_and_the_legend()
    {
        var console = new TestConsole().Width(SpectreTableRenderer.MinimumWidth);

        SpectreTableRenderer.Render(console, Fixture.BuildReport("EdgeCases"));

        await Assert.That(console.Output).Contains("Coverage by visibility");
        await Assert.That(console.Output).Contains("Top 10 classes by missed lines");
        await Assert.That(console.Output).Contains("Unknown (no PublicAPI files)");
        await Assert.That(console.Output).Contains("Sorted by missed lines.");
    }

    [Test]
    public async Task Real_report_fits_the_width_without_wrapping_a_row()
    {
        var console = new TestConsole().Width(SpectreTableRenderer.MinimumWidth);
        var report = Fixture.BuildReport("Real");

        SpectreTableRenderer.Render(console, report);

        var lines = console.Output.Split('\n');
        await Assert.That(lines.Max(line => line.TrimEnd().Length)).IsLessThanOrEqualTo(SpectreTableRenderer.MinimumWidth);
        foreach (var row in report.Tables[3].Rows)
        {
            await Assert.That(lines.Any(line => line.Contains(row.Name, StringComparison.Ordinal) && line.Contains(row.Cells[3], StringComparison.Ordinal))).IsTrue();
        }
    }

    [Test]
    public async Task Names_with_brackets_are_not_read_as_markup()
    {
        var table = new Build.Cake.Extensions.Coverage.Reporting.CoverageTable("[T]", ["Name", "Missed"],
            [new(Build.Cake.Extensions.Coverage.Reporting.CoverageRowKind.Item, "Foo[bar]", ["1"])]);
        var console = new TestConsole().Width(80);

        console.Write(SpectreTableRenderer.Build(table));

        await Assert.That(console.Output).Contains("Foo[bar]");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail.**
  - Run: `dotnet test --project build/Build.Cake.Extensions.Tests`
  - Expected: a compile error, because `SpectreTableRenderer` doesn't exist.

- [ ] **Step 3: Implement.**

```csharp
using Build.Cake.Extensions.Coverage.Reporting;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Build.Cake.Extensions.Coverage.Rendering;

// Coverage tables for the build log. Spectre.Console ships with Cake (through Cake.Cli), so this adds no dependency.
public static class SpectreTableRenderer
{
    // A console without a terminal (CI logs) is assumed 80 columns wide, which would wrap these tables.
    public const int MinimumWidth = 160;

    public static IAnsiConsole CreateConsole()
    {
        var console = AnsiConsole.Create(new AnsiConsoleSettings());
        if (Console.IsOutputRedirected)
        {
            console.Profile.Width = Math.Max(console.Profile.Width, MinimumWidth);
        }

        return console;
    }

    public static void Render(IAnsiConsole console, CoverageReport report)
    {
        foreach (var table in report.Tables)
        {
            console.WriteLine();
            console.Write(Build(table));
        }

        console.WriteLine();
        console.WriteLine(CoverageReport.Legend);
        console.WriteLine();
    }

    public static Table Build(CoverageTable table)
    {
        var result = new Table
        {
            Border = TableBorder.Simple,
            Title = new TableTitle(Markup.Escape(table.Title), new Style(Color.Aqua)),
        };

        for (var column = 0; column < table.Headers.Count; column++)
        {
            var header = new TableColumn(new Text(table.Headers[column])) { NoWrap = true };
            result.AddColumn(column is 1 or 2 ? header.RightAligned() : header);
        }

        foreach (var row in table.Rows)
        {
            var style = row.Kind switch
            {
                CoverageRowKind.Group or CoverageRowKind.Item => new Style(decoration: Decoration.Dim),
                _ => new Style(decoration: Decoration.Bold),
            };
            var name = row.Kind == CoverageRowKind.Group ? "  " + row.Name : row.Name;
            result.AddRow([new Text(name, style), .. row.Cells.Select(cell => (IRenderable)new Text(cell, style))]);
        }

        return result;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass.**
  - Run: `dotnet test --project build/Build.Cake.Extensions.Tests`
  - Expected: all tests pass.
  - If `Real_report_fits...` fails because a class name plus file is wider than 160 columns, raise `MinimumWidth`
    to 200 and say so in the commit message.

- [ ] **Step 5: Commit.**

```bash
git add build/Build.Cake.Extensions build/Build.Cake.Extensions.Tests
git commit -m "Render coverage tables with Spectre.Console" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Coverage comparison

**Files:**
- Create: `build/Build.Cake.Extensions/Coverage/Comparison/CoverageComparisonOptions.cs`, `CoverageComparison.cs`, `CoverageComparer.cs`
- Test: `build/Build.Cake.Extensions.Tests/Coverage/Comparison/CoverageComparerTests.cs`

**Interfaces:**
- Consumes: `CoverageSummary`, `CoverageCounts` (Task 3); `Invariant`, `NameOrder`.
- Produces:
  - `record CoverageComparisonOptions(string Marker, double MaxLineDecrease, string Footer)`
  - `record CoverageComparison(string Conclusion, string Title, string Markdown)`. `Conclusion` is one of
    `"neutral"`, `"success"` or `"failure"`.
  - `static class CoverageComparer`:
    `CoverageComparison Compare(CoverageSummary current, CoverageSummary? baseline, string baselineLabel, CoverageComparisonOptions options)`
  - `internal static class CoverageFormat`: `string Change(double points)` and `string CountChange(int change)`

- [ ] **Step 1: Write the failing tests.**

```csharp
using System.Globalization;
using System.Text.Json;
using Build.Cake.Extensions.Coverage.Comparison;
using Build.Cake.Extensions.Coverage.Model;

namespace Build.Cake.Extensions.Tests.Coverage.Comparison;

public class CoverageComparerTests
{
    private static readonly CoverageComparisonOptions Options = new(
        Marker: "<!-- typstium-coverage -->",
        MaxLineDecrease: 0.5,
        Footer: "<sub>Unit and integration tests, Windows and Ubuntu merged. The full tables are in the CI run's coverage job summary.</sub>");

    private static CoverageSummary Summary(string file) => CoverageSummary.FromJson(Fixture.Read("Summaries", file));

    [Test]
    [Arguments("no-baseline", "current.json", null, "`main`")]
    [Arguments("pass", "current.json", "baseline-pass.json", "`main` at `abc1234` (merge base)")]
    [Arguments("fail", "current.json", "baseline-fail.json", "`main` at `def5678` (latest; the merge base `abc1234` has no coverage)")]
    [Arguments("boundary", "current-boundary.json", "baseline-boundary.json", "`main` at `abc1234` (merge base)")]
    [Arguments("unchanged", "current.json", "current.json", "`main` at `abc1234` (merge base)")]
    public async Task Matches_the_PowerShell_comparison(string name, string current, string? baseline, string label)
    {
        var comparison = CoverageComparer.Compare(Summary(current), baseline is null ? null : Summary(baseline), label, Options);

        using var expected = JsonDocument.Parse(Fixture.Read("Summaries", "expected", name + ".result.json"));
        await Assert.That(comparison.Markdown + "\n").IsEqualTo(Fixture.Read("Summaries", "expected", name + ".md"));
        await Assert.That(comparison.Conclusion).IsEqualTo(expected.RootElement.GetProperty("conclusion").GetString());
        await Assert.That(comparison.Title).IsEqualTo(expected.RootElement.GetProperty("title").GetString());
    }

    [Test]
    [Arguments(0.05, "🟢 +0.1 pp")]
    [Arguments(0.25, "🟢 +0.3 pp")]
    [Arguments(-0.25, "🔴 −0.3 pp")]
    [Arguments(-0.04, "⚪ ±0.0 pp")]
    [Arguments(0.0, "⚪ ±0.0 pp")]
    public async Task Changes_round_half_away_from_zero_with_a_true_minus_sign(double points, string expected)
    {
        await Assert.That(CoverageFormat.Change(points)).IsEqualTo(expected);
    }

    [Test]
    [Arguments(0, "⚪ ±0")]
    [Arguments(-3, "🟢 −3")]
    [Arguments(4, "🔴 +4")]
    public async Task Fewer_missed_lines_is_green(int change, string expected)
    {
        await Assert.That(CoverageFormat.CountChange(change)).IsEqualTo(expected);
    }

    [Test]
    public async Task Starts_with_the_marker_and_ends_with_the_footer()
    {
        var comparison = CoverageComparer.Compare(Summary("current.json"), Summary("baseline-pass.json"), "`main`", Options);

        await Assert.That(comparison.Markdown).StartsWith(Options.Marker + "\n");
        await Assert.That(comparison.Markdown).EndsWith(Options.Footer + "\n");
    }

    [Test]
    public async Task Writes_points_not_commas_on_a_Swedish_machine()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("sv-SE");
        try
        {
            var comparison = CoverageComparer.Compare(Summary("current.json"), Summary("baseline-fail.json"), "`main`", Options with { MaxLineDecrease = 0.75 });

            await Assert.That(comparison.Title).IsEqualTo("83.0% lines (−2.0 pp)");
            await Assert.That(comparison.Markdown).Contains("may drop at most 0.75 pp");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail.**
  - Run: `dotnet test --project build/Build.Cake.Extensions.Tests`
  - Expected: a compile error, because `Build.Cake.Extensions.Coverage.Comparison` doesn't exist.

- [ ] **Step 3: Implement the records.**

`CoverageComparisonOptions.cs`:

```csharp
namespace Build.Cake.Extensions.Coverage.Comparison;

// Marker: the comment's first line, by which the sticky comment is found again. Footer: its last line.
// MaxLineDecrease: how many percentage points (pp) line coverage may drop from the baseline.
public sealed record CoverageComparisonOptions(string Marker, double MaxLineDecrease, string Footer);
```

`CoverageComparison.cs`:

```csharp
namespace Build.Cake.Extensions.Coverage.Comparison;

// Conclusion is a GitHub check-run conclusion: "neutral" (no baseline), "success" or "failure".
public sealed record CoverageComparison(string Conclusion, string Title, string Markdown);
```

- [ ] **Step 4: Implement `CoverageComparer` and `CoverageFormat`.**

```csharp
using System.Text;
using Build.Cake.Extensions.Coverage.Model;

namespace Build.Cake.Extensions.Coverage.Comparison;

// Compares two coverage summaries and writes the pull-request comment. The rule: total line coverage may drop at most
// MaxLineDecrease percentage points from the baseline. Branch and method coverage are shown but not gated: they move
// more between runs of the same code.
public static class CoverageComparer
{
    public static CoverageComparison Compare(CoverageSummary current, CoverageSummary? baseline, string baselineLabel, CoverageComparisonOptions options)
    {
        var now = current.Totals;
        var linePercent = CoverageFormat.Percent(now.CoveredLines, now.Lines);
        var markdown = new StringBuilder();
        Line(markdown, options.Marker);

        string conclusion, title;
        if (baseline is null)
        {
            conclusion = "neutral";
            title = $"{CoverageFormat.Percentage(linePercent)} lines (no baseline)";
            Line(markdown, $"### Coverage: {CoverageFormat.Percentage(linePercent)} lines");
            Line(markdown);
            Line(markdown, $"No baseline coverage for {baselineLabel} yet, so there's nothing to compare with; the rule isn't applied.");
            Line(markdown);
            Line(markdown, "| | This PR |");
            Line(markdown, "| --- | ---: |");
            Line(markdown, $"| Line | {CoverageFormat.Rate(now.CoveredLines, now.Lines)} |");
            Line(markdown, $"| Branch | {CoverageFormat.Rate(now.CoveredBranches, now.Branches)} |");
            Line(markdown, $"| Method (called) | {CoverageFormat.Rate(now.CoveredMethods, now.Methods)} |");
            Line(markdown, Invariant.Format($"| Missed lines | {now.MissedLines} |"));
        }
        else
        {
            var before = baseline.Totals;
            var lineChange = linePercent - CoverageFormat.Percent(before.CoveredLines, before.Lines);
            var passed = lineChange >= -options.MaxLineDecrease;
            conclusion = passed ? "success" : "failure";
            var change = CoverageFormat.Change(lineChange);
            title = $"{CoverageFormat.Percentage(linePercent)} lines ({change[(change.IndexOf(' ') + 1)..]})";
            var rule = Invariant.Format($"Rule: line coverage may drop at most {options.MaxLineDecrease:0.0#} pp");
            var verdict = passed ? "✅ **passed**" : "❌ **failed**";

            Line(markdown, $"### Coverage: {CoverageFormat.Percentage(linePercent)} lines {change}");
            Line(markdown);
            Line(markdown, $"Compared with {baselineLabel}. {rule}: {verdict}");
            Line(markdown);
            Line(markdown, "| | This PR | Baseline | Change |");
            Line(markdown, "| --- | ---: | ---: | ---: |");
            MetricRow(markdown, "Line", now.CoveredLines, now.Lines, before.CoveredLines, before.Lines);
            MetricRow(markdown, "Branch", now.CoveredBranches, now.Branches, before.CoveredBranches, before.Branches);
            MetricRow(markdown, "Method (called)", now.CoveredMethods, now.Methods, before.CoveredMethods, before.Methods);
            Line(markdown, Invariant.Format($"| Missed lines | {now.MissedLines} | {before.MissedLines} | {CoverageFormat.CountChange(now.MissedLines - before.MissedLines)} |"));
            ChangedNamespaces(markdown, current, baseline);
        }

        Line(markdown);
        Line(markdown, options.Footer);
        return new CoverageComparison(conclusion, title, markdown.ToString());
    }

    private static void MetricRow(StringBuilder markdown, string name, int covered, int total, int baseCovered, int baseTotal)
    {
        var change = CoverageFormat.Percent(covered, total) - CoverageFormat.Percent(baseCovered, baseTotal);
        Line(markdown, $"| {name} | {CoverageFormat.Rate(covered, total)} | {CoverageFormat.Rate(baseCovered, baseTotal)} | {CoverageFormat.Change(change)} |");
    }

    // Namespaces whose line counts changed, biggest growth in missed lines first.
    private static void ChangedNamespaces(StringBuilder markdown, CoverageSummary current, CoverageSummary baseline)
    {
        var nowByName = ByName(current);
        var baseByName = ByName(baseline);
        var names = nowByName.Keys.Concat(baseByName.Keys).Distinct(NameOrder.Lookup).Order(NameOrder.Sort);

        var changed = new List<(string Name, string Now, string Base, string Change, int Missed)>();
        foreach (var name in names)
        {
            var now = nowByName.GetValueOrDefault(name);
            var before = baseByName.GetValueOrDefault(name);
            var nowCounts = now ?? CoverageCounts.Zero;
            var baseCounts = before ?? CoverageCounts.Zero;
            if (nowCounts.CoveredLines == baseCounts.CoveredLines && nowCounts.Lines == baseCounts.Lines)
            {
                continue;
            }

            changed.Add((
                name,
                now is null ? "(removed)" : CoverageFormat.Rate(now.CoveredLines, now.Lines),
                before is null ? "(new)" : CoverageFormat.Rate(before.CoveredLines, before.Lines),
                now is not null && before is not null ? CoverageFormat.Change(CoverageFormat.Percent(now.CoveredLines, now.Lines) - CoverageFormat.Percent(before.CoveredLines, before.Lines)) : "",
                nowCounts.MissedLines - baseCounts.MissedLines));
        }

        if (changed.Count == 0)
        {
            return;
        }

        Line(markdown);
        Line(markdown, Invariant.Format($"<details><summary>Namespaces with changed line coverage ({changed.Count})</summary>"));
        Line(markdown);
        Line(markdown, "| Namespace | This PR | Baseline | Change | Missed lines |");
        Line(markdown, "| --- | ---: | ---: | ---: | ---: |");
        foreach (var row in changed.OrderByDescending(row => row.Missed).ThenBy(row => row.Name, NameOrder.Sort))
        {
            Line(markdown, $"| {row.Name} | {row.Now} | {row.Base} | {row.Change} | {CoverageFormat.CountChange(row.Missed)} |");
        }

        Line(markdown);
        Line(markdown, "</details>");
    }

    // Later entries win, as in the PowerShell hashtable this replaces.
    private static Dictionary<string, CoverageCounts> ByName(CoverageSummary summary)
    {
        var byName = new Dictionary<string, CoverageCounts>(NameOrder.Lookup);
        foreach (var entry in summary.Namespaces)
        {
            byName[entry.Name] = entry.Counts;
        }

        return byName;
    }

    private static void Line(StringBuilder markdown, string text = "") => markdown.Append(text).Append('\n');
}

internal static class CoverageFormat
{
    public static double Percent(int covered, int total) => total == 0 ? 100.0 : 100.0 * covered / total;

    public static string Percentage(double value) => Invariant.Format($"{value:0.0}%");

    public static string Rate(int covered, int total) => Invariant.Format($"{Percentage(Percent(covered, total))} ({covered}/{total})");

    // Percentage-point change with a sign and a colour: green up, red down, white within rounding.
    public static string Change(double points)
    {
        var rounded = Math.Round(points, 1, MidpointRounding.AwayFromZero);
        if (rounded == 0)
        {
            return "⚪ ±0.0 pp";
        }

        var text = Invariant.Format($"{Math.Abs(rounded):0.0} pp");
        return rounded > 0 ? $"🟢 +{text}" : $"🔴 −{text}";
    }

    // A change in a count where fewer is better (missed lines).
    public static string CountChange(int change) =>
        change == 0 ? "⚪ ±0" : change < 0 ? Invariant.Format($"🟢 −{-change}") : Invariant.Format($"🔴 +{change}");
}
```

- [ ] **Step 5: Run the tests to verify they pass.**
  - Run: `dotnet test --project build/Build.Cake.Extensions.Tests`
  - Expected: all tests pass.

- [ ] **Step 6: Commit.**

```bash
git add build/Build.Cake.Extensions build/Build.Cake.Extensions.Tests
git commit -m "Port the pull-request coverage comparison" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 10: GitHub pull-request context, runs and payloads

**Files:**
- Create: `build/Build.Cake.Extensions/GitHub/PullRequestEvent.cs`, `PullRequestContext.cs`, `WorkflowRun.cs`, `GitHubJson.cs`
- Test: `build/Build.Cake.Extensions.Tests/GitHub/PullRequestContextTests.cs`, `GitHubJsonTests.cs`

**Interfaces:**
- Produces:
  - `record PullRequestEvent(int Number, string HeadSha, string? HeadRepository, string Author)`, with
    `static PullRequestEvent Parse(string eventJson)`
  - `record PullRequestContext(string Repository, string BaseRef, PullRequestEvent PullRequest)`, with
    `string? ReadOnlyReason`
  - `record WorkflowRun(long Id, string HeadSha)`, with `static IReadOnlyList<WorkflowRun> ParseList(string json)`
  - `static class GitHubJson`, with:
    - `long? FindCommentStartingWith(string slurpedComments, string marker)`
    - `string CommentPayload(string body)`
    - `string CheckRunPayload(string name, string headSha, string conclusion, string title, string summary)`

- [ ] **Step 1: Write the failing tests.**

`PullRequestContextTests.cs`:

```csharp
using Build.Cake.Extensions.GitHub;

namespace Build.Cake.Extensions.Tests.GitHub;

public class PullRequestContextTests
{
    [Test]
    public async Task Reads_number_head_and_author_from_the_event_payload()
    {
        var pullRequest = PullRequestEvent.Parse(Fixture.Read("Events", "pull_request.json"));

        await Assert.That(pullRequest).IsEqualTo(new PullRequestEvent(42, "0123456789abcdef0123456789abcdef01234567", "mgnslndh/Typstium", "mgnslndh"));
    }

    [Test]
    public async Task A_pull_request_from_this_repository_can_write()
    {
        var context = new PullRequestContext("mgnslndh/Typstium", "main", PullRequestEvent.Parse(Fixture.Read("Events", "pull_request.json")));

        await Assert.That(context.ReadOnlyReason).IsNull();
    }

    [Test]
    public async Task A_fork_is_read_only_whatever_the_case_of_its_name()
    {
        var context = new PullRequestContext("mgnslndh/Typstium", "main", new PullRequestEvent(1, "abc", "someone/Typstium", "someone"));
        var sameRepository = context with { PullRequest = new PullRequestEvent(1, "abc", "MGNSLNDH/typstium", "mgnslndh") };

        await Assert.That(context.ReadOnlyReason).IsEqualTo("a pull request from a fork (someone/Typstium)");
        await Assert.That(sameRepository.ReadOnlyReason).IsNull();
    }

    [Test]
    public async Task A_pull_request_from_a_deleted_fork_is_read_only()
    {
        var pullRequest = PullRequestEvent.Parse(Fixture.Read("Events", "pull_request-deleted-fork.json"));
        var context = new PullRequestContext("mgnslndh/Typstium", "main", pullRequest);

        await Assert.That(pullRequest.HeadRepository).IsNull();
        await Assert.That(context.ReadOnlyReason).IsEqualTo("a pull request from a deleted fork");
    }

    [Test]
    public async Task Dependabot_is_read_only()
    {
        var context = new PullRequestContext("mgnslndh/Typstium", "main", new PullRequestEvent(3, "abc", "mgnslndh/Typstium", "dependabot[bot]"));

        await Assert.That(context.ReadOnlyReason).IsEqualTo("a Dependabot pull request");
    }
}
```

`GitHubJsonTests.cs`:

```csharp
using System.Text.Json;
using Build.Cake.Extensions.GitHub;

namespace Build.Cake.Extensions.Tests.GitHub;

public class GitHubJsonTests
{
    [Test]
    public async Task Finds_the_first_comment_starting_with_the_marker_across_pages()
    {
        const string slurped = """
            [
              [ { "id": 1, "body": "Looks good" }, { "id": 2, "body": "see <!-- typstium-coverage --> later" } ],
              [ { "id": 3, "body": "<!-- typstium-coverage -->\n### Coverage" }, { "id": 4, "body": "<!-- typstium-coverage -->" } ]
            ]
            """;

        await Assert.That(GitHubJson.FindCommentStartingWith(slurped, "<!-- typstium-coverage -->")).IsEqualTo(3L);
        await Assert.That(GitHubJson.FindCommentStartingWith("[[]]", "<!-- typstium-coverage -->")).IsNull();
    }

    [Test]
    public async Task Parses_workflow_runs_in_the_order_listed()
    {
        var runs = WorkflowRun.ParseList("""[ { "databaseId": 11, "headSha": "aaa" }, { "databaseId": 10, "headSha": "bbb" } ]""");

        await Assert.That(runs).IsEquivalentTo([new WorkflowRun(11, "aaa"), new WorkflowRun(10, "bbb")], TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Payloads_carry_Markdown_unescaped_and_intact()
    {
        const string markdown = "<!-- m -->\n### Coverage: 83.0% lines 🟢 +1.0 pp\n| a | b |\n";

        using var comment = JsonDocument.Parse(GitHubJson.CommentPayload(markdown));
        using var check = JsonDocument.Parse(GitHubJson.CheckRunPayload("Coverage", "abc", "success", "83.0% lines (+1.0 pp)", markdown));

        await Assert.That(comment.RootElement.GetProperty("body").GetString()).IsEqualTo(markdown);
        await Assert.That(check.RootElement.GetProperty("name").GetString()).IsEqualTo("Coverage");
        await Assert.That(check.RootElement.GetProperty("head_sha").GetString()).IsEqualTo("abc");
        await Assert.That(check.RootElement.GetProperty("status").GetString()).IsEqualTo("completed");
        await Assert.That(check.RootElement.GetProperty("conclusion").GetString()).IsEqualTo("success");
        await Assert.That(check.RootElement.GetProperty("output").GetProperty("title").GetString()).IsEqualTo("83.0% lines (+1.0 pp)");
        await Assert.That(check.RootElement.GetProperty("output").GetProperty("summary").GetString()).IsEqualTo(markdown);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail.**
  - Run: `dotnet test --project build/Build.Cake.Extensions.Tests`
  - Expected: a compile error, because `Build.Cake.Extensions.GitHub` doesn't exist.

- [ ] **Step 3: Implement.**

`PullRequestEvent.cs`:

```csharp
using System.Text.Json;

namespace Build.Cake.Extensions.GitHub;

// The parts of a pull_request event payload (GITHUB_EVENT_PATH) the coverage comparison needs. HeadSha is the pull
// request's head, not GITHUB_SHA (the merge commit). HeadRepository is null when the fork was deleted.
public sealed record PullRequestEvent(int Number, string HeadSha, string? HeadRepository, string Author)
{
    public static PullRequestEvent Parse(string eventJson)
    {
        using var document = JsonDocument.Parse(eventJson);
        var pullRequest = document.RootElement.GetProperty("pull_request");
        var head = pullRequest.GetProperty("head");
        var repository = head.GetProperty("repo");
        return new PullRequestEvent(
            pullRequest.GetProperty("number").GetInt32(),
            head.GetProperty("sha").GetString() ?? "",
            repository.ValueKind == JsonValueKind.Object ? repository.GetProperty("full_name").GetString() : null,
            pullRequest.GetProperty("user").GetProperty("login").GetString() ?? "");
    }
}
```

`PullRequestContext.cs`:

```csharp
namespace Build.Cake.Extensions.GitHub;

// Repository and BaseRef come from the workflow environment (GITHUB_REPOSITORY, GITHUB_BASE_REF); the rest from the
// event payload.
public sealed record PullRequestContext(string Repository, string BaseRef, PullRequestEvent PullRequest)
{
    // Pull requests from forks, deleted forks and Dependabot run with a read-only token: they can't comment or create
    // check runs. Null when the token can write.
    public string? ReadOnlyReason =>
        PullRequest.HeadRepository is null ? "a pull request from a deleted fork"
        : !string.Equals(PullRequest.HeadRepository, Repository, StringComparison.OrdinalIgnoreCase) ? $"a pull request from a fork ({PullRequest.HeadRepository})"
        : string.Equals(PullRequest.Author, "dependabot[bot]", StringComparison.OrdinalIgnoreCase) ? "a Dependabot pull request"
        : null;
}
```

`WorkflowRun.cs`:

```csharp
using System.Text.Json;

namespace Build.Cake.Extensions.GitHub;

// One row of `gh run list --json databaseId,headSha`.
public sealed record WorkflowRun(long Id, string HeadSha)
{
    public static IReadOnlyList<WorkflowRun> ParseList(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateArray()
            .Select(run => new WorkflowRun(run.GetProperty("databaseId").GetInt64(), run.GetProperty("headSha").GetString() ?? ""))
            .ToList();
    }
}
```

`GitHubJson.cs`:

```csharp
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Build.Cake.Extensions.GitHub;

// Request bodies and response parsing for `gh api`. Bodies go through files (--input), so Markdown never has to
// survive command-line quoting.
public static class GitHubJson
{
    private static readonly JsonSerializerOptions Options = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    // The comments of `gh api --paginate --slurp`: an array of pages, each an array of comments.
    public static long? FindCommentStartingWith(string slurpedComments, string marker)
    {
        using var document = JsonDocument.Parse(slurpedComments);
        foreach (var page in document.RootElement.EnumerateArray())
        {
            foreach (var comment in page.EnumerateArray())
            {
                if (comment.GetProperty("body").GetString()?.StartsWith(marker, StringComparison.Ordinal) == true)
                {
                    return comment.GetProperty("id").GetInt64();
                }
            }
        }

        return null;
    }

    public static string CommentPayload(string body) => JsonSerializer.Serialize(new { body }, Options);

    public static string CheckRunPayload(string name, string headSha, string conclusion, string title, string summary) =>
        JsonSerializer.Serialize(new { name, head_sha = headSha, status = "completed", conclusion, output = new { title, summary } }, Options);
}
```

- [ ] **Step 4: Run the tests to verify they pass.**
  - Run: `dotnet test --project build/Build.Cake.Extensions.Tests`
  - Expected: all tests pass.

- [ ] **Step 5: Commit.**

```bash
git add build/Build.Cake.Extensions build/Build.Cake.Extensions.Tests
git commit -m "Read the pull-request context from the event payload and build gh api bodies" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 11: Cake edges: merge, report, `gh`, publish

These units touch processes, files and GitHub. CI exercises them (Tasks 12 and 14), so they have no unit tests. Keep
them thin: every decision lives in the pure units above.

**Files:**
- Create: `build/Build.Cake.Extensions/Coverage/CoverageMerger.cs`, `CoverageReportSettings.cs`, `CoverageAliases.cs`
- Create: `build/Build.Cake.Extensions/GitHub/GitHubCli.cs`, `PullRequestCoverageSettings.cs`, `PullRequestCoverageAliases.cs`

**Interfaces:**
- Consumes: everything from Tasks 3–10.
- Produces:
  - `record CoverageReportSettings(DirectoryPath CoverageDirectory, DirectoryPath SourceDirectory, IReadOnlyList<string> Products, string JobSummaryHeading, bool WriteJobSummary, int TopClasses = 10)`,
    with `RawDirectory` and `SummaryPath`
  - `static class CoverageAliases`: `CoverageSummary ReportCoverage(this ICakeContext context, CoverageReportSettings settings)`
  - `record PullRequestCoverageSettings(FilePath SummaryPath, string Workflow, string ArtifactName, string CheckName, CoverageComparisonOptions Comparison)`
  - `static class PullRequestCoverageAliases`: `CoverageComparison PublishPullRequestCoverage(this ICakeContext context, PullRequestCoverageSettings settings)`

- [ ] **Step 1: `CoverageMerger.cs`.**

```csharp
using Build.Cake.Extensions.Coverage.Cobertura;
using Cake.Common.Diagnostics;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;
using Palmmedia.ReportGenerator.Core;

namespace Build.Cake.Extensions.Coverage;

// Merges every raw Cobertura file (each test project and target framework, from one machine or several) into one
// report: raw/ → relative/ (repo-relative paths) → report/Cobertura.xml, with ReportGenerator in process.
internal static class CoverageMerger
{
    public static FilePath Merge(ICakeContext context, DirectoryPath coverageDirectory, IReadOnlyList<string> products)
    {
        var relative = coverageDirectory.Combine("relative");
        var report = coverageDirectory.Combine("report");
        context.EnsureDirectoryDoesNotExist(relative, new EnsureDirectoryDoesNotExistSettings { Recursive = true, Force = true });
        context.EnsureDirectoryDoesNotExist(report, new EnsureDirectoryDoesNotExistSettings { Recursive = true, Force = true });

        var count = CoberturaPathNormalizer.NormalizeFiles(coverageDirectory.Combine("raw").FullPath, relative.FullPath);
        context.Information("Merging {0} coverage file(s) for {1}", count, string.Join(", ", products));

        var configuration = new ReportConfigurationBuilder().Create(new Dictionary<string, string>
        {
            ["reports"] = relative.CombineWithFilePath("*.cobertura.xml").FullPath,
            ["targetdir"] = report.FullPath,
            ["reporttypes"] = "Cobertura",
            // Only the products count; anything under tests/ never does.
            ["assemblyfilters"] = string.Join(';', products.Select(product => "+" + product)),
            ["verbosity"] = "Warning",
        });

        if (!new Generator().GenerateReport(configuration))
        {
            throw new CakeException($"ReportGenerator couldn't merge the coverage files in {relative.FullPath}.");
        }

        return report.CombineWithFilePath("Cobertura.xml");
    }
}
```

If `EnsureDirectoryDoesNotExistSettings` has no `Recursive` or `Force` properties in Cake 6.3.0, use
`context.DeleteDirectory(path, new DeleteDirectorySettings { Recursive = true, Force = true })` behind
`context.DirectoryExists(path)`.

- [ ] **Step 2: `CoverageReportSettings.cs` and `CoverageAliases.cs`.**

```csharp
using Cake.Core.IO;

namespace Build.Cake.Extensions.Coverage;

// CoverageDirectory holds raw/ (the test runs' Cobertura files) and receives relative/, report/ and summary.json.
// SourceDirectory holds one folder per product (src/<assembly>/) with its PublicAPI.*.txt files.
public sealed record CoverageReportSettings(
    DirectoryPath CoverageDirectory,
    DirectoryPath SourceDirectory,
    IReadOnlyList<string> Products,
    string JobSummaryHeading,
    bool WriteJobSummary,
    int TopClasses = 10)
{
    public DirectoryPath RawDirectory => CoverageDirectory.Combine("raw");

    public FilePath SummaryPath => CoverageDirectory.CombineWithFilePath("summary.json");
}
```

```csharp
using Build.Cake.Extensions.Coverage.Cobertura;
using Build.Cake.Extensions.Coverage.Model;
using Build.Cake.Extensions.Coverage.Rendering;
using Build.Cake.Extensions.Coverage.Reporting;
using Cake.Common.Build;
using Cake.Core;

namespace Build.Cake.Extensions.Coverage;

public static class CoverageAliases
{
    // Merges the raw coverage files, prints the four views, writes summary.json and, on GitHub Actions, the job summary.
    public static CoverageSummary ReportCoverage(this ICakeContext context, CoverageReportSettings settings)
    {
        var cobertura = CoverageMerger.Merge(context, settings.CoverageDirectory, settings.Products);
        var classes = CoberturaReader.Read(File.ReadAllText(cobertura.FullPath));
        var publicTypes = classes.Select(c => c.Assembly).Distinct(StringComparer.Ordinal)
            .ToDictionary(assembly => assembly, assembly => PublicApiCatalog.Read(settings.SourceDirectory.Combine(assembly).FullPath));
        var report = CoverageReportBuilder.Build(classes, publicTypes, settings.TopClasses);

        SpectreTableRenderer.Render(SpectreTableRenderer.CreateConsole(), report);
        report.Summary.Write(settings.SummaryPath.FullPath);

        var gitHubActions = context.GitHubActions();
        if (settings.WriteJobSummary && gitHubActions.IsRunningOnGitHubActions)
        {
            gitHubActions.Commands.SetStepSummary(MarkdownTableRenderer.RenderJobSummary(report, settings.JobSummaryHeading));
        }

        return report.Summary;
    }
}
```

- [ ] **Step 3: `GitHubCli.cs`.**

```csharp
using Cake.Common;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Cake.Extensions.GitHub;

// `gh` and `git` through Cake's process runner: stdout back as text, a CakeException with stderr on failure.
internal sealed class GitHubCli(ICakeContext context)
{
    public string Gh(ProcessArgumentBuilder arguments) => Run("gh", arguments);

    public string Git(ProcessArgumentBuilder arguments) => Run("git", arguments);

    // For calls that may fail by design, e.g. downloading an artifact that expired.
    public bool TryGh(ProcessArgumentBuilder arguments) => Execute("gh", arguments).ExitCode == 0;

    private string Run(string tool, ProcessArgumentBuilder arguments)
    {
        var (exitCode, output, error) = Execute(tool, arguments);
        if (exitCode != 0)
        {
            throw new CakeException($"{tool} {arguments.RenderSafe()} failed with exit code {exitCode}: {error}");
        }

        return output;
    }

    private (int ExitCode, string Output, string Error) Execute(string tool, ProcessArgumentBuilder arguments)
    {
        var executable = context.Tools.Resolve(OperatingSystem.IsWindows() ? tool + ".exe" : tool)
            ?? throw new CakeException($"{tool} isn't on PATH.");
        var settings = new ProcessSettings { Arguments = arguments, RedirectStandardOutput = true, RedirectStandardError = true, Silent = true };
        var exitCode = context.StartProcess(executable, settings, out var output, out var error);
        return (exitCode, string.Join('\n', output), string.Join('\n', error));
    }
}
```

- [ ] **Step 4: `PullRequestCoverageSettings.cs` and `PullRequestCoverageAliases.cs`.** `Cake.Core.IO` also defines
  `File`, `Directory` and `Path`. In files that import it, `System.IO` types are written fully qualified, as below.

```csharp
using Build.Cake.Extensions.Coverage.Comparison;
using Cake.Core.IO;

namespace Build.Cake.Extensions.GitHub;

// Workflow: the workflow file whose runs hold baselines (e.g. ci.yml). ArtifactName: the artifact holding a run's
// summary.json. CheckName: the check run created on the pull request's head.
public sealed record PullRequestCoverageSettings(
    FilePath SummaryPath,
    string Workflow,
    string ArtifactName,
    string CheckName,
    CoverageComparisonOptions Comparison);
```

```csharp
using Build.Cake.Extensions.Coverage.Comparison;
using Build.Cake.Extensions.Coverage.Model;
using Cake.Common.Build;
using Cake.Common.Diagnostics;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Cake.Extensions.GitHub;

public static class PullRequestCoverageAliases
{
    // On a pull_request run: compares this run's coverage summary with the one from the pull request's merge base on
    // the base branch, then posts (or updates) one sticky comment and creates an advisory check run.
    // Baseline: the successful run of the workflow at the merge-base commit; if that has no summary (never built, or
    // the artifact expired), the latest successful push run of the base branch, and the comment says so.
    // Needs gh (GH_TOKEN with pull-requests: write, checks: write, actions: read) and full git history.
    public static CoverageComparison PublishPullRequestCoverage(this ICakeContext context, PullRequestCoverageSettings settings)
    {
        var gitHubActions = context.GitHubActions();
        if (!gitHubActions.IsRunningOnGitHubActions || !gitHubActions.Environment.PullRequest.IsPullRequest)
        {
            throw new CakeException("The coverage comparison runs on GitHub Actions, for pull_request events only.");
        }

        var workflow = gitHubActions.Environment.Workflow;
        var pullRequest = new PullRequestContext(workflow.Repository, workflow.BaseRef, PullRequestEvent.Parse(System.IO.File.ReadAllText(workflow.EventPath.FullPath)));
        context.Information(
            "Pull request #{0} in {1}: head {2} from {3} by {4}, base {5}",
            pullRequest.PullRequest.Number, pullRequest.Repository, pullRequest.PullRequest.HeadSha,
            pullRequest.PullRequest.HeadRepository ?? "(deleted)", pullRequest.PullRequest.Author, pullRequest.BaseRef);

        var cli = new GitHubCli(context);
        var work = context.Environment.GetSpecialPath(SpecialPath.LocalTemp).Combine("coverage-comparison-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(work.FullPath);

        var (baseline, label) = FindBaseline(cli, pullRequest, settings, work);
        var comparison = CoverageComparer.Compare(CoverageSummary.Read(settings.SummaryPath.FullPath), baseline, label, settings.Comparison);
        gitHubActions.Commands.SetStepSummary(comparison.Markdown);

        if (pullRequest.ReadOnlyReason is { } reason)
        {
            gitHubActions.Commands.Notice($"Coverage {comparison.Conclusion}, {comparison.Title}. This is {reason}, whose token is read-only, so the comparison is in the job summary only.");
            return comparison;
        }

        var repository = pullRequest.Repository;
        var comments = cli.Gh(new ProcessArgumentBuilder().Append("api").Append("--paginate").Append("--slurp").Append($"repos/{repository}/issues/{pullRequest.PullRequest.Number}/comments"));
        var commentBody = WriteJson(work, "comment.json", GitHubJson.CommentPayload(comparison.Markdown));
        var existing = GitHubJson.FindCommentStartingWith(comments, settings.Comparison.Marker);
        cli.Gh(existing is { } id
            ? new ProcessArgumentBuilder().Append("api").Append("--method").Append("PATCH").Append($"repos/{repository}/issues/comments/{id}").Append("--input").AppendQuoted(commentBody)
            : new ProcessArgumentBuilder().Append("api").Append("--method").Append("POST").Append($"repos/{repository}/issues/{pullRequest.PullRequest.Number}/comments").Append("--input").AppendQuoted(commentBody));

        // Advisory: nothing requires this check, so a failure is visible but doesn't block merging.
        var checkRun = WriteJson(work, "check-run.json", GitHubJson.CheckRunPayload(settings.CheckName, pullRequest.PullRequest.HeadSha, comparison.Conclusion, comparison.Title, comparison.Markdown));
        cli.Gh(new ProcessArgumentBuilder().Append("api").Append("--method").Append("POST").Append($"repos/{repository}/check-runs").Append("--input").AppendQuoted(checkRun));

        context.Information("Coverage comparison posted: {0}, {1}", comparison.Conclusion, comparison.Title);
        return comparison;
    }

    private static (CoverageSummary? Baseline, string Label) FindBaseline(GitHubCli cli, PullRequestContext pullRequest, PullRequestCoverageSettings settings, DirectoryPath work)
    {
        var baseRef = pullRequest.BaseRef;
        var mergeBase = cli.Git(new ProcessArgumentBuilder().Append("merge-base").Append($"origin/{baseRef}").Append(pullRequest.PullRequest.HeadSha)).Trim();
        var shortMergeBase = mergeBase[..Math.Min(7, mergeBase.Length)];

        foreach (var run in ListRuns(cli, pullRequest.Repository, settings.Workflow, "--commit", mergeBase, limit: 5))
        {
            if (Download(cli, pullRequest.Repository, run, settings.ArtifactName, work) is { } summary)
            {
                return (summary, $"`{baseRef}` at `{shortMergeBase}` (merge base)");
            }
        }

        foreach (var run in ListRuns(cli, pullRequest.Repository, settings.Workflow, "--branch", baseRef, limit: 10, pushOnly: true))
        {
            if (Download(cli, pullRequest.Repository, run, settings.ArtifactName, work) is { } summary)
            {
                return (summary, $"`{baseRef}` at `{run.HeadSha[..Math.Min(7, run.HeadSha.Length)]}` (latest; the merge base `{shortMergeBase}` has no coverage)");
            }
        }

        return (null, $"`{baseRef}`");
    }

    private static IReadOnlyList<WorkflowRun> ListRuns(GitHubCli cli, string repository, string workflow, string filter, string value, int limit, bool pushOnly = false)
    {
        var arguments = new ProcessArgumentBuilder().Append("run").Append("list").Append("--repo").Append(repository)
            .Append("--workflow").Append(workflow).Append(filter).Append(value);
        if (pushOnly)
        {
            arguments.Append("--event").Append("push");
        }

        arguments.Append("--status").Append("success").Append("--limit").Append(limit.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("--json").Append("databaseId,headSha");
        return WorkflowRun.ParseList(cli.Gh(arguments));
    }

    // A run's coverage summary, or null when the run has none (or it expired).
    private static CoverageSummary? Download(GitHubCli cli, string repository, WorkflowRun run, string artifactName, DirectoryPath work)
    {
        var target = work.Combine(run.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var downloaded = cli.TryGh(new ProcessArgumentBuilder().Append("run").Append("download").Append(run.Id.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Append("--repo").Append(repository).Append("--name").Append(artifactName).Append("--dir").AppendQuoted(target.FullPath));
        var path = target.CombineWithFilePath("summary.json").FullPath;
        return downloaded && System.IO.File.Exists(path) ? CoverageSummary.Read(path) : null;
    }

    private static string WriteJson(DirectoryPath work, string name, string json)
    {
        var path = work.CombineWithFilePath(name).FullPath;
        System.IO.File.WriteAllText(path, json);
        return path;
    }
}
```

- [ ] **Step 5: Build and run the tests.**
  - Run: `dotnet build build/Build.slnx`. Expected: 0 warnings, 0 errors.
  - Fix any Cake API name that differs in 6.3.0 by looking it up in `Cake.Common.xml` or
    https://cakebuild.net/api/. Don't work around it.
  - Run: `dotnet test --project build/Build.Cake.Extensions.Tests`. Expected: all tests still pass.

- [ ] **Step 6: Commit.**

```bash
git add build/Build.Cake.Extensions
git commit -m "Add the Cake edges: coverage merge and report, gh, pull-request publishing" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 12: The Typstium build, bootstraps and a local side-by-side run

**Files:**
- Modify: `build/Build/Program.cs`, `build/Build/Build.csproj`
- Create: `build/Build/BuildParameters.cs`, `build/Build/TypstiumCoverage.cs`
- Create: `build/Build/Tasks/BuildTasks.cs`, `TestTasks.cs`, `CoverageTasks.cs`, `DefaultTasks.cs`
- Modify: `build.ps1` (rewritten as a bootstrap)
- Create: `build.sh`
- Modify: `.gitattributes`

**Interfaces:**
- Consumes:
  - `CoverageAliases.ReportCoverage`, `CoverageReportSettings` (Task 11)
  - `PullRequestCoverageAliases.PublishPullRequestCoverage`, `PullRequestCoverageSettings` (Task 11)
  - `CoverageComparisonOptions` (Task 9)
- Produces: the targets and arguments in Global Constraints. CI uses them in Task 13.

- [ ] **Step 1: Global usings for the build project.** Add to `build/Build/Build.csproj`:

```xml
  <ItemGroup>
    <Using Include="Build.Cake.Extensions.Coverage" />
    <Using Include="Build.Cake.Extensions.Coverage.Comparison" />
    <Using Include="Build.Cake.Extensions.GitHub" />
  </ItemGroup>
```

- [ ] **Step 2: `BuildParameters.cs`.**

```csharp
using System.Globalization;

// The build's inputs, read once in Setup from the command line (--name=value).
public sealed record BuildParameters(
    string Target,
    string Configuration,
    bool Coverage,
    bool JobSummary,
    double MaxLineDecrease,
    DirectoryPath CoverageDirectory)
{
    public DirectoryPath RawCoverageDirectory => CoverageDirectory.Combine("raw");

    public static BuildParameters Create(ICakeContext context, string target) => new(
        target,
        context.Argument("configuration", "Release"),
        context.HasArgument("coverage"),
        !context.HasArgument("no-job-summary"),
        // Parsed here, not by Cake's converter, so --max-line-decrease=0.5 means the same on a Swedish machine.
        double.Parse(context.Argument("max-line-decrease", "0.5"), NumberStyles.Float, CultureInfo.InvariantCulture),
        context.MakeAbsolute(context.Directory("artifacts/coverage")));
}
```

- [ ] **Step 3: `TypstiumCoverage.cs`.**

```csharp
// What makes the coverage report and comparison Typstium's: the products, the comment's marker and footer, and the
// CI workflow that keeps the baselines.
public static class TypstiumCoverage
{
    public const string Marker = "<!-- typstium-coverage -->";
    public const string Footer = "<sub>Unit and integration tests, Windows and Ubuntu merged. The full tables are in the CI run's coverage job summary.</sub>";
    public const string JobSummaryHeading = "### Code coverage (unit and integration tests)";
    public const string Workflow = "ci.yml";
    public const string SummaryArtifact = "coverage-summary";
    public const string CheckName = "Coverage";

    // The products are the projects under src/; anything under tests/ never counts.
    public static CoverageReportSettings ReportSettings(ICakeContext context, BuildParameters parameters) => new(
        parameters.CoverageDirectory,
        context.MakeAbsolute(context.Directory("src")),
        context.GetFiles("src/**/*.csproj").Select(project => project.GetFilenameWithoutExtension().FullPath).Order(StringComparer.Ordinal).ToList(),
        JobSummaryHeading,
        parameters.JobSummary);

    public static PullRequestCoverageSettings ComparisonSettings(BuildParameters parameters) => new(
        parameters.CoverageDirectory.CombineWithFilePath("summary.json"),
        Workflow,
        SummaryArtifact,
        CheckName,
        new CoverageComparisonOptions(Marker, parameters.MaxLineDecrease, Footer));
}
```

- [ ] **Step 4: The tasks.**

`Tasks/BuildTasks.cs`:

```csharp
public static partial class Program
{
    static void RegisterBuildTasks()
    {
        Task("Build")
            .IsDependentOn("BuildTests")
            .Does<BuildParameters>(parameters =>
                DotNetBuild("Typstium.slnx", new DotNetBuildSettings { Configuration = parameters.Configuration }));
    }
}
```

`Tasks/TestTasks.cs`:

```csharp
public static partial class Program
{
    // The build's own tests first (fast), then the products': unit, integration, and the timing tests last and alone,
    // because they assert wall-clock bounds that other compiles running alongside would disturb.
    static void RegisterTestTasks()
    {
        Task("BuildTests")
            .Does<BuildParameters>(parameters =>
                DotNetTest("build/Build.Cake.Extensions.Tests", new DotNetTestSettings
                {
                    PathType = DotNetTestPathType.Project,
                    Configuration = parameters.Configuration,
                }));

        Task("UnitTests")
            .IsDependentOn("Build")
            .Does<BuildParameters>(parameters => RunProductTests("tests/Typstium.UnitTests", parameters));

        Task("IntegrationTests")
            .IsDependentOn("UnitTests")
            .Does<BuildParameters>(parameters => RunProductTests("tests/Typstium.IntegrationTests", parameters));

        // Never with coverage: instrumentation would distort the timings.
        Task("TimingTests")
            .IsDependentOn("IntegrationTests")
            .Does<BuildParameters>(parameters =>
                DotNetTest("tests/Typstium.TimingTests", new DotNetTestSettings
                {
                    PathType = DotNetTestPathType.Project,
                    Configuration = parameters.Configuration,
                    NoBuild = true,
                    ArgumentCustomization = arguments => arguments.Append("--max-parallel-test-modules").Append("1"),
                }));
    }

    // With --coverage each run writes Cobertura into the raw folder under a GUID name, so target frameworks of one
    // project don't overwrite each other.
    static void RunProductTests(string project, BuildParameters parameters)
    {
        var settings = new DotNetTestSettings { PathType = DotNetTestPathType.Project, Configuration = parameters.Configuration, NoBuild = true };
        var arguments = new ProcessArgumentBuilder();
        if (parameters.Coverage)
        {
            settings.ResultsDirectory = parameters.RawCoverageDirectory;
            arguments.Append("--coverage").Append("--coverage-output-format").Append("cobertura");
        }

        DotNetTest(project, arguments, settings);
    }
}
```

`Tasks/CoverageTasks.cs`:

```csharp
public static partial class Program
{
    static void RegisterCoverageTasks()
    {
        // In Default only with --coverage (after every test passed); as the target, it merges raw files already in
        // artifacts/coverage/raw/, e.g. CI's Windows and Ubuntu artifacts copied together.
        Task("CoverageReport")
            .WithCriteria<BuildParameters>(parameters => parameters.Coverage || parameters.Target == "CoverageReport")
            .Does<BuildParameters>((context, parameters) => context.ReportCoverage(TypstiumCoverage.ReportSettings(context, parameters)));

        // CI only, on pull requests: compares with the merge base, comments, and creates the Coverage check.
        Task("CoverageComparison")
            .Does<BuildParameters>((context, parameters) => context.PublishPullRequestCoverage(TypstiumCoverage.ComparisonSettings(parameters)));
    }
}
```

`Tasks/DefaultTasks.cs`:

```csharp
public static partial class Program
{
    // What CI runs on every platform, and what a local ./build.sh runs.
    static void RegisterDefaultTask()
    {
        Task("Default")
            .IsDependentOn("TimingTests")
            .IsDependentOn("CoverageReport");
    }
}
```

- [ ] **Step 5: Rewrite `build/Build/Program.cs`.** If `EnsureDirectoryDoesNotExistSettings` lacks `Recursive` or
  `Force` in Cake 6.3.0, use the same fallback as Task 11 Step 1.

```csharp
// Builds and tests Typstium exactly as CI does; CI runs this build, so the two can't drift apart.
// See docs/specs/cake-sdk-build.md. Arguments: --target=<task> (Default), --configuration=<c> (Release), --coverage,
// --no-job-summary, --max-line-decrease=<pp> (0.5).
var target = Argument("target", "Default");

Setup(context =>
{
    var parameters = BuildParameters.Create(context, target);

    // A fresh coverage run starts empty; the CoverageReport target works on raw files someone put there.
    if (parameters.Coverage && parameters.Target != "CoverageReport")
    {
        EnsureDirectoryDoesNotExist(parameters.CoverageDirectory, new EnsureDirectoryDoesNotExistSettings { Recursive = true, Force = true });
    }

    return parameters;
});

RegisterBuildTasks();
RegisterTestTasks();
RegisterCoverageTasks();
RegisterDefaultTask();

RunTarget(target);
```

- [ ] **Step 6: Build it.**
  - Run: `dotnet build build/Build.slnx`. Expected: 0 warnings.
  - If `partial class Program` conflicts with Cake.Sdk's generated declaration, match the generated modifiers, as the
    Cake.Sdk example does with `public static partial class Program`.

- [ ] **Step 7: Rewrite `build.ps1` as a bootstrap.**

```powershell
#!/usr/bin/env pwsh
# Runs the build (build/Build, Cake.Sdk); arguments pass through, e.g. --coverage or --target=CoverageReport.
# See docs/specs/cake-sdk-build.md. build.sh does the same without PowerShell.
dotnet run --project "$PSScriptRoot/build/Build/Build.csproj" -- @args
exit $LASTEXITCODE
```

- [ ] **Step 8: Create `build.sh`**, then make it executable and LF.

```bash
#!/usr/bin/env bash
# Runs the build (build/Build, Cake.Sdk); arguments pass through, e.g. --coverage or --target=CoverageReport.
# See docs/specs/cake-sdk-build.md. build.ps1 does the same in PowerShell.
set -euo pipefail
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
exec dotnet run --project "$script_dir/build/Build/Build.csproj" -- "$@"
```

In `.gitattributes`, under the `build.ps1` line:

```
build.sh text eol=lf
```

Then run: `git add build.sh .gitattributes && git update-index --chmod=+x build.sh`

- [ ] **Step 9: Run the whole build with coverage, side by side with the PowerShell run from Task 2.**
  - Run (pwsh): `./build.ps1 --coverage`
  - Expected:
    - tasks run in the order BuildTests, Build, UnitTests, IntegrationTests, TimingTests, CoverageReport, all
      succeeding
    - four Spectre tables are printed
    - `artifacts/coverage/summary.json` exists
  - Then compare: `git diff --no-index artifacts/coverage-powershell/summary.json artifacts/coverage/summary.json`
  - Expected: no difference, or only differences explained by coverage that varies between runs (for example timing
    paths). Inspect any difference and record it in the commit message.

- [ ] **Step 10: Check the report-only target and the job summary output.**

```powershell
$env:GITHUB_ACTIONS = 'true'
$env:GITHUB_STEP_SUMMARY = "$PWD/artifacts/job-summary.md"
Remove-Item $env:GITHUB_STEP_SUMMARY -ErrorAction SilentlyContinue
./build.ps1 --target=CoverageReport
Remove-Item Env:GITHUB_ACTIONS, Env:GITHUB_STEP_SUMMARY
Get-Content artifacts/job-summary.md -TotalCount 12
```

Expected: only CoverageReport runs, and the raw files are kept. `artifacts/job-summary.md` starts with
`### Code coverage (unit and integration tests)` followed by the visibility table.

- [ ] **Step 11: Check the arguments and the error cases.**
  - `./build.ps1 --target=BuildTests --configuration=Debug`. Expected: only BuildTests runs, in Debug.
  - `cd tests; ../build.ps1 --target=BuildTests; cd ..`. Expected: it succeeds, because paths resolve from the repo
    root (Review Focus 5).
  - `Move-Item artifacts/coverage/raw artifacts/raw-aside; ./build.ps1 --target=CoverageReport`. Expected: it fails
    with `No *.cobertura.xml files in …/artifacts/coverage/raw. Run the tests with --coverage first…`.
    Then restore: `Move-Item artifacts/raw-aside artifacts/coverage/raw`.
  - With a Swedish culture: `pwsh -Command '[cultureinfo]::CurrentCulture = "sv-SE"; ./build.ps1 --target=CoverageReport --max-line-decrease=0.5'`.
    Expected: it succeeds, and the tables show `.` as the decimal separator.
  - If WSL with the .NET 10 SDK is available: `wsl ./build.sh --target=BuildTests`. Expected: it succeeds. Without
    WSL, CI's Ubuntu job covers `build.sh` (Task 14).

- [ ] **Step 12: Commit.**

```bash
git add build/Build build.ps1 build.sh .gitattributes
git commit -m "Run the build through Cake.Sdk with build.ps1 and build.sh bootstraps" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 13: CI, Dependabot, docs, and retiring the PowerShell scripts

**Files:**
- Modify: `.github/workflows/ci.yml`
- Modify: `.github/dependabot.yml`
- Modify: `README.md` (Building section)
- Delete: `build/Compare-Coverage.ps1`, `build/Publish-CoverageComparison.ps1`, `build/Write-CoverageReport.ps1`

**Interfaces:**
- Consumes: the targets and arguments from Task 12.

- [ ] **Step 1: The build-matrix job in `ci.yml`.** Replace the `- run: ./build.ps1 -Coverage -NoJobSummary` step and
  its comment with:

```yaml
      # The same steps as a local run: the build's own tests, build, unit, integration, then the timing tests alone.
      # --coverage prints this platform's coverage tables to the log; the coverage job below merges both platforms for
      # the job summary. Each platform starts the build with its own bootstrap, so CI exercises both.
      - if: runner.os == 'Windows'
        run: ./build.ps1 --coverage --no-job-summary
        shell: pwsh
      - if: runner.os != 'Windows'
        run: ./build.sh --coverage --no-job-summary
        shell: bash
```

- [ ] **Step 2: The coverage job.**
  - Replace `- run: ./build.ps1 -CoverageReportOnly` (and its `shell: pwsh`) with:

```yaml
      - run: ./build.sh --target=CoverageReport
        shell: bash
```

  - Replace the "Compare with the merge base" step with:

```yaml
      # The pull request's number, head and author come from the event payload; the repository and base branch from
      # GITHUB_REPOSITORY and GITHUB_BASE_REF.
      - name: Compare with the merge base
        if: github.event_name == 'pull_request'
        run: ./build.sh --target=CoverageComparison --max-line-decrease=$MAX_LINE_DECREASE
        shell: bash
        env:
          GH_TOKEN: ${{ github.token }}
```

- [ ] **Step 3: The latest-Typst job.** Replace `- run: ./build.ps1` (and its `shell: pwsh`) with:

```yaml
      # The same steps as a local run: the build's own tests, build, unit, integration, then the timing tests alone.
      - run: ./build.sh
        shell: bash
```

- [ ] **Step 4: Validate the workflow.**
  - Run: `gh workflow view ci.yml --yaml > /dev/null` if the workflow is reachable. Otherwise run
    `pwsh -c "Get-Content .github/workflows/ci.yml -Raw | ConvertFrom-Yaml"` if the module exists.
  - At minimum, read the diff: every `build.ps1 -X` flag is gone (`git grep -n "\-Coverage\|CoverageReportOnly\|NoJobSummary" .github`
    shows nothing), and no step still sets `PR_NUMBER`, `HEAD_SHA`, `HEAD_REPOSITORY`, `PR_AUTHOR` or `BASE_REF`.

- [ ] **Step 5: Dependabot.** In `.github/dependabot.yml`, in the `nuget` entry:
  - under `groups:`, add:

```yaml
      # The build (build/): Cake.Sdk in global.json's msbuild-sdks and the Cake packages move together.
      cake:
        patterns: ['Cake.*']
```

  - add `'ReportGenerator.Core'` to the `build-tooling` patterns:
    `patterns: ['Polyfill', 'Microsoft.CodeAnalysis.*', 'ReportGenerator.Core']`
  - under `ignore:`, add:

```yaml
      # Spectre.Console is pre-1.0 and Cake.Cli is compiled against one version of it: it moves only with Cake.
      - dependency-name: 'Spectre.Console*'
```

- [ ] **Step 6: Delete the PowerShell helpers.**

```bash
git rm build/Compare-Coverage.ps1 build/Publish-CoverageComparison.ps1 build/Write-CoverageReport.ps1
git grep -n "Write-CoverageReport\|Compare-Coverage\|Publish-CoverageComparison" -- . ':!docs/specs' ':!docs/plans'
```

Expected: no matches outside the spec, the plans, and comments in `build/Build.Cake.Extensions` that name a script.
Rewrite those comments to describe the behavior instead (e.g. "as the PowerShell report did" → "in the format CI's
baselines use").

- [ ] **Step 7: README.** Replace the paragraph after the code block in "## Building" with:

```markdown
To run exactly what CI runs (the build's own tests, build, unit, integration, then the timing tests alone), use
`./build.sh` or `./build.ps1`. Both start the Cake build in `build/Build` and pass their arguments on. Add
`--coverage` for code-coverage tables of the unit and integration tests (by visibility, namespace and area, plus the
classes with the most missed lines), as CI prints them. `--target=CoverageReport` merges and reports raw coverage
files already in `artifacts/coverage/raw/`, such as CI's Windows and Ubuntu artifacts copied together. Other
arguments: `--configuration=Debug`, and `--target=<task>` for a single step (`BuildTests`, `Build`, `UnitTests`,
`IntegrationTests`, `TimingTests`).
```

- [ ] **Step 8: Run the build once more and commit.**
  - Run: `./build.ps1`. Expected: every task succeeds.

```bash
git add .github README.md build
git commit -m "Switch CI to the Cake build and retire the PowerShell scripts" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 14: Verify in CI with a pull request

This is outward-facing: **ask the user before pushing and before opening the pull request.**

- [ ] **Step 1: Temporarily dump the GitHub Actions environment.** Add this step to the coverage job, just before
  "Compare with the merge base", and commit it as `Temporarily print CI's environment`:

```yaml
      # TEMPORARY: compare what Cake reads (GitHubActionsWorkflowInfo) with what the runner sets. Removed before merging.
      - run: env | sort | grep -E '^(GITHUB_|RUNNER_|CI=)' ; jq '{number: .pull_request.number, head: .pull_request.head.sha, repo: .pull_request.head.repo.full_name, user: .pull_request.user.login}' "$GITHUB_EVENT_PATH"
        shell: bash
```

- [ ] **Step 2: With the user's go-ahead, push and open the pull request.**

```bash
git push -u origin chore/cake-sdk
gh pr create --title "Move the build from PowerShell to Cake.Sdk" --body-file <file with summary, test plan, and the 🤖 Generated with [Claude Code](https://claude.com/claude-code) line>
```

- [ ] **Step 3: Check the run.** Use `gh run watch` or `gh pr checks --watch`. Expected:
  - The Windows job ran `build.ps1` and the Ubuntu job ran `build.sh`. Both pass and upload `coverage-*` artifacts.
  - The logs show Cake's task groups (Cake.BuildSystems.Module) and unwrapped Spectre tables.
  - The coverage job's job summary shows the Markdown tables.
  - The step prints: `Pull request #<n> in mgnslndh/Typstium: head <sha> from mgnslndh/Typstium by mgnslndh, base main`.
    These values match the `env` and `jq` dump: `GITHUB_REPOSITORY`, `GITHUB_BASE_REF`, and the payload's
    number, head and user.
  - A sticky coverage comment is posted by the new code. Its baseline is `main`'s PowerShell-written summary, labelled
    as the merge base. A `Coverage` check run is created on the head SHA.
  - The latest-Typst job passes, or fails only for Typst reasons, since it's `continue-on-error`.

- [ ] **Step 4: Remove the temporary step and push.**

```bash
git revert --no-edit <sha of "Temporarily print CI's environment">
git push
```

Expected: CI is green again. The comment is updated in place, not duplicated, which proves the sticky lookup.

- [ ] **Step 5: Hand back** with the PR link. Point out any difference in the comment compared with earlier
  PowerShell-made comments, which should be none.
