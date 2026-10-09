# Build: from PowerShell to Cake.Sdk

**Status:** Proposed · Oct 9, 2026 · Magnus Lindhe
**Part of:** [Typstium design overview](../design.md)
**Replaces:** `build.ps1` (logic), `build/Write-CoverageReport.ps1`, `build/Compare-Coverage.ps1`,
`build/Publish-CoverageComparison.ps1`

## 1. Purpose and scope

The build is the one place where a local run and CI meet. CI runs the build script, so the two can't drift. Today it is
about 600 lines of PowerShell, and half of that is coverage logic that has no tests. This spec moves the build to
[Cake.Sdk](https://www.nuget.org/packages/Cake.Sdk/) (project-based, script-style API). It splits the build into:

- a Typstium-specific build project
- a Typstium-agnostic library of reusable build logic
- tests for that library

**Goals**

- Same behavior as today: the same steps in the same order, the same flags, and the same artifacts. The
  Markdown and JSON outputs are byte-identical (job summary, pull-request comment, check run, `summary.json`).
- All PowerShell build logic ported to C#, split into small units with one purpose each.
- Pure logic covered by TUnit tests, including golden files produced by the current scripts.
- `build.ps1` and `build.sh` at the root as thin bootstraps. CI calls them, as it calls `build.ps1` today.
- Cake features used where they exist: arguments, environment variables, GitHub Actions helpers, `DotNetTest`.
  Spectre.Console renders console tables.

**Out of scope**

- Packing and publishing NuGet packages, and versioning (GitVersion).
- Changes to which tests run or how (projects, parallelism, `TYPSTIUM_REQUIRE_TYPST`).
- Changes to the coverage rule, the PR comment's content, or the baseline lookup.
- `Cake.Typstium` (the product addin in [the design overview](../design.md)). It is unrelated to
  `Build.Cake.Extensions`, which is internal and never packed.

## 2. Decisions

| Decision | Choice | Why |
|---|---|---|
| Cake flavor | Project-based Cake.Sdk (`<Project Sdk="Cake.Sdk">`) | Full IDE support, inherits `Directory.Build.props`, can be referenced by tests. |
| Folder | `build/` | Used by Cake Frosting's template, NUKE and the Cake.Sdk example. The repo already has it. `eng/` is the dotnet/arcade convention and only pays off with arcade. |
| Split | `Build` (Typstium) + `Build.Cake.Extensions` (agnostic) + `Build.Cake.Extensions.Tests` | Typstium specifics are configuration passed to reusable logic. |
| Cake.Sdk version | `global.json` → `msbuild-sdks` → `Cake.Sdk: 6.3.0` | As in the Cake.Sdk example. Cake.Sdk pins Cake.Core and Cake.Common 6.3.0 with `VersionOverride`, so it works with central package management. |
| ReportGenerator | `ReportGenerator.Core` package, called in process | Replaces `dnx dotnet-reportgenerator-globaltool@5.5.11`, a version Dependabot can't see. |
| Console tables | Spectre.Console (0.57.2, as shipped by Cake.Cli) | Already loaded by Cake. Proper tables instead of hand-padded columns. |
| Tests | TUnit on Microsoft.Testing.Platform, like the product tests | One test stack in the repo. |
| Bootstrap in CI | `build.ps1` on Windows, `build.sh` on Ubuntu | CI exercises both bootstraps. |

## 3. Layout

```
build.ps1                          bootstrap (pwsh, shebang, eol=lf, executable)
build.sh                           bootstrap (bash, eol=lf, executable)
global.json                        + "msbuild-sdks": { "Cake.Sdk": "6.3.0" }
Directory.Packages.props           + Cake.Common, Cake.BuildSystems.Module, ReportGenerator.Core,
                                     Spectre.Console, Spectre.Console.Testing
build/
  Build.slnx                       the three build projects; not part of Typstium.slnx
  Directory.Build.props            imports the root props; IsPackable=false
  Build/
    Build.csproj                   Sdk="Cake.Sdk"; net10.0; RunWorkingDirectory = repo root
    Program.cs                     Setup (→ BuildParameters), task registration, RunTarget
    BuildParameters.cs             typed record built from arguments, environment and paths
    TypstiumCoverage.cs            the Typstium values for the coverage options
    Tasks/
      BuildTasks.cs                Build
      TestTasks.cs                 BuildTests, UnitTests, IntegrationTests, TimingTests
      CoverageTasks.cs             CoverageReport, CoverageComparison
      DefaultTasks.cs              Default
  Build.Cake.Extensions/
    Build.Cake.Extensions.csproj   class library; Cake.Common, ReportGenerator.Core, Spectre.Console
    Coverage/
      Cobertura/                   CoberturaPathNormalizer, CoberturaReader
      Model/                       CoverageCounts, ClassCoverage, CoverageSummary (+ JSON)
      Reporting/                   PublicApiCatalog, NamespaceResolver, CoverageReportBuilder, CoverageTable
      Rendering/                   SpectreTableRenderer, MarkdownTableRenderer
      Comparison/                  CoverageComparer, CoverageComparisonOptions, CoverageComparison
      Cobertura/SourceLayout.cs    the repository's code folders (src, tests) and products folder (src)
      CoverageLayout.cs            raw/, relative/, report/ and summary.json under one root
      CoverageMerger.cs            ReportGenerator.Core in process (edge)
      CoverageReportSettings.cs    inputs of the report
      CoverageAliases.cs           ReportCoverage: section 6 end to end (edge)
    GitHub/
      GitHubCli.cs                 `gh`/`git` through Cake's process runner (edge)
      PullRequestEvent.cs          number, head SHA, head repository, author from the event payload
      PullRequestContext.cs        the event plus repository and base ref; whether the token is read-only
      WorkflowRun.cs               rows of `gh run list --json`
      GitHubJson.cs                `gh api` request bodies; finding the sticky comment
      StepSummary.cs               appends to the job summary when GITHUB_STEP_SUMMARY exists (edge)
      PullRequestCoverageSettings.cs
      PullRequestCoverageAliases.cs PublishPullRequestCoverage: baseline lookup, sticky comment, check run (edge)
  Build.Cake.Extensions.Tests/
    Build.Cake.Extensions.Tests.csproj   TUnit, Spectre.Console.Testing
    Fixtures/                      Cobertura and summary inputs; expected outputs (golden files)
```

Within this layout, the folders under `Coverage/` follow the data flow (section 6). File names are the main type they
hold. The PowerShell scripts under `build/` are deleted at the end of the migration.

## 4. Boundaries

- **`Build.Cake.Extensions` never mentions Typstium or this repository's layout.** Everything specific to the repo
  arrives through options, including the folder layout (`SourceLayout`, `CoverageLayout`) and the hint shown when
  there are no raw coverage files:
  - the comment marker
  - the footer text
  - the product list
  - the workflow and artifact names
  - the threshold
- **Pure units take no `ICakeContext`.** They work on strings, streams, file paths they are given, and the model
  types:
  - `CoberturaPathNormalizer`, `CoberturaReader`, `PublicApiCatalog`, `NamespaceResolver`
  - `CoverageReportBuilder`, `MarkdownTableRenderer`, `CoverageComparer`, `CoverageSummary`

  `SpectreTableRenderer` takes an `IAnsiConsole`, so tests use Spectre's `TestConsole`.
- **Edges take `ICakeContext`** and use Cake for files, processes, logging, environment variables and GitHub
  Actions. These are `CoverageMerger`, `GitHubCli` and `PullRequestCoverage`. They are not unit-tested: CI runs them
  (section 9).
- **Tasks in `Build` only wire.** They read `BuildParameters`, call Cake aliases (`DotNetBuild`, `DotNetTest`) or
  the extensions, and contain no logic worth testing. Anything that grows a branch moves into the extensions
  library.
- Extensions are exposed as plain classes and `ICakeContext` extension methods. They don't depend on Cake.Generator
  picking up aliases from a project reference.

## 5. Command line

### Bootstraps

- `build.ps1`: `dotnet run --project build/Build -- @args`, then `exit $LASTEXITCODE`. It keeps the
  `#!/usr/bin/env pwsh` shebang, its `.gitattributes` entry (`eol=lf`) and the executable bit.
- `build.sh`: `#!/usr/bin/env bash`, `set -euo pipefail`, then `exec dotnet run --project build/Build -- "$@"`. It
  gets an `eol=lf` entry and the executable bit (`git update-index --chmod=+x`).
- `Build.csproj` sets `RunWorkingDirectory` to the repo root. Every path in the build is then relative to the root,
  wherever the bootstrap is called from.

### Arguments

These are Cake arguments (`--name=value`, flags without a value), read with `Argument`/`HasArgument` in `Setup` into
`BuildParameters`.

| Today | New | Default |
|---|---|---|
| `./build.ps1` | `./build.sh` or `./build.ps1` | target `Default` |
| `-Configuration Debug` | `--configuration=Debug` | `Release` |
| `-Coverage` | `--coverage` | off |
| `-NoJobSummary` | `--no-job-summary` | off |
| `-CoverageReportOnly` | `--target=CoverageReport` | — |
| `build/Publish-CoverageComparison.ps1 -MaxLineDecrease 0.5` | `--target=CoverageComparison --max-line-decrease=0.5` | `0.5` |

### Targets

```
Default ─► TimingTests ─► IntegrationTests ─► UnitTests ─► Build ─► BuildTests
   └─► CoverageReport     (in Default only with --coverage; always when it is the target)

CoverageReport            no dependencies: merge artifacts/coverage/raw → report, summary.json, job summary
CoverageComparison        no dependencies; CI on pull requests: compare, comment, check run
```

| Task | Does |
|---|---|
| `BuildTests` | `DotNetTest` on `build/Build.Cake.Extensions.Tests`, without coverage arguments. It runs first and is fast. The running `Build` process loads its own copy of the extensions DLL from `build/Build/bin`, so building the test project doesn't touch a locked file. |
| `Build` | `DotNetBuild` of the repo root (`Typstium.slnx`) with the configuration. |
| `UnitTests` | `DotNetTest --project tests/Typstium.UnitTests --no-build`, plus coverage arguments with `--coverage`. |
| `IntegrationTests` | Same, for `tests/Typstium.IntegrationTests`. |
| `TimingTests` | `DotNetTest --project tests/Typstium.TimingTests --no-build --max-parallel-test-modules 1`, never with coverage, since instrumentation would distort timings. |
| `CoverageReport` | Section 6: normalize, merge, report, `summary.json`, job summary. |
| `CoverageComparison` | Section 7. |

- With `--coverage`, `Setup` deletes `artifacts/coverage` before anything runs.
- The coverage arguments are, as today: `--results-directory artifacts/coverage/raw -- --coverage --coverage-output-format cobertura`.
- `TimingTests` runs after the other two, alone, because it asserts wall-clock bounds.
- A failing task stops the run, and Cake exits non-zero. So the coverage report, as today, appears only when every
  test passed.

## 6. Coverage report

```
artifacts/coverage/raw/*.cobertura.xml          MTP --coverage, one GUID-named file per run
  │ 1. CoberturaPathNormalizer (pure)   filename="…" → repo-relative: \ → /, cut before the last /src/ or /tests/
  ▼
artifacts/coverage/relative/                     (deleted and recreated)
  │ 2. CoverageMerger (edge)            ReportGenerator.Core, report type Cobertura,
  │                                     assembly filters "+<product>" for each product
  ▼
artifacts/coverage/report/Cobertura.xml          (deleted and recreated)
  │ 3. CoberturaReader (pure)           → ClassCoverage(Assembly, Name, File, Counts) per class and file
  │ 4. PublicApiCatalog (pure)          src/<assembly>/PublicAPI.*.txt → public type names, or none
  │ 5. CoverageReportBuilder (pure)     → CoverageReport: 4 CoverageTables + CoverageSummary
  ├─► SpectreTableRenderer              console
  ├─► MarkdownTableRenderer             job summary, via GitHubActions.Commands.SetStepSummary
  └─► CoverageSummary.Write             artifacts/coverage/summary.json
```

**Path normalization.** This is unchanged from `ConvertTo-RepoRelativeCoverage`. Each machine records absolute
paths. Without normalization, ReportGenerator would count a file once per machine instead of merging its lines.

**Reading.** This is unchanged from `Read-CoverageClasses`:

- one entry per class per file
- lines counted, and covered when `hits > 0`
- branches from `condition-coverage="…(c/t)"`
- a method counts as called when any of its lines has `hits != 0`
- the file is made relative to `src/<assembly>/`, or reduced to its leaf name when it lies outside that folder

**Grouping.** This is unchanged from the script. There are four tables, all sorted by missed lines, then missed
branches, then name:

1. **Visibility.** Classes are "Public API", "Internal", or "Unknown (no PublicAPI files)". A public type is a bare
   type line (no `->`, not a comment, generic arguments dropped) in the product's `PublicAPI.*.txt` files.
2. **Namespace.** The namespace is the prefix before the first segment that is itself a class in the report, so
   nested types land in their outer type's namespace.
3. **Area.** The top-level folder under the product folder. Files directly in it form `(root)`.
4. **Top classes.** The `TopClasses` (default 10) classes with the most missed lines, among those with any.

Tables 1–3 list each assembly's groups, then an assembly subtotal, then a final total. Gap is a row's share of all
missed lines.

**Rendering.**

- **Console (Spectre.Console).**
  - One `Table` per view, with the title above it.
  - Group rows are indented and dimmed. Assembly and total rows are bold.
  - Number columns are right-aligned.
  - The legend line follows the tables.
  - On a console without a terminal (CI), Spectre would assume 80 columns and wrap the tables. The edge therefore
    creates the console with a fixed width (`AnsiConsoleSettings`, at least 160 columns) when output is redirected.
  - The console layout is new; it is not required to match the PowerShell padding.
- **Markdown (job summary).** Byte-identical to today:
  - the heading `### Code coverage (unit and integration tests)`
  - the visibility table open, the other three in `<details>`
  - the same name decoration (`&nbsp;&nbsp;` for groups, backticks for items, bold for assembly and total)
  - the same alignment row
  - the legend

  The heading text is a parameter, because "unit and integration tests" is Typstium-specific.
- Markdown always uses `
` line endings, whatever the platform.
- The job summary is written only on GitHub Actions (`GitHubActions.IsRunningOnGitHubActions`) and not with
  `--no-job-summary`.

**`summary.json`.** The schema is unchanged:

```json
{ "totals": { "coveredLines": 0, "lines": 0, "coveredBranches": 0, "branches": 0, "coveredMethods": 0, "methods": 0 },
  "namespaces": [ { "name": "…", "coveredLines": 0, "lines": 0, "coveredBranches": 0, "branches": 0, "coveredMethods": 0, "methods": 0 } ] }
```

- It is UTF-8 without a BOM. Property order is as above.
- `namespaces` holds the namespace table's group rows.
- The reader must accept summaries written by the PowerShell version, because baselines on `main` remain
  PowerShell-written for up to 90 days.

**Products.** `Build` passes the product list: the names of `src/*/*.csproj`, found with Cake's `GetFiles`. This list
feeds the assembly filters and the `PublicAPI` lookup. Anything under `tests/` never counts.

## 7. Coverage comparison (CI, pull requests)

`CoverageComparer` is pure. It turns `(current, baseline?, options)` into a `CoverageComparison(Conclusion, Title,
Markdown)`. Its rules and output are unchanged from `Compare-Coverage.ps1`:

- **No baseline.** The conclusion is `neutral` and the title is `<x>% lines (no baseline)`. The comment has a
  single-column table.
- **Baseline.**
  - The conclusion is `success` when `linePercent − baselineLinePercent ≥ −MaxLineDecrease`, otherwise `failure`.
  - The title is `<x>% lines (<±change>)`.
  - The comment has a four-column table (line, branch, method, missed lines).
  - It also has a `<details>` block of namespaces whose line counts changed, with the most new missed lines first,
    then by name.
- **Formatting.**
  - `Math.Round(points, 1, MidpointRounding.AwayFromZero)` for changes, and invariant `0.0` formats.
  - `U+2212` (`−`) as the minus sign.
  - 🟢 / 🔴 / ⚪ markers, and ✅ / ❌ for the verdict.
  - 0 lines counts as 100%.
- The comment starts with `options.Marker`, used to find the sticky comment, and ends with `options.Footer`.
- **Name comparisons.** Ordering (tie-breaks, the sorted union of namespace names) uses
  `StringComparer.InvariantCultureIgnoreCase`, closest to `Sort-Object`. Lookups use `StringComparer.OrdinalIgnoreCase`,
  like PowerShell hashtables. The golden files decide.

`PublishPullRequestCoverage` is the edge. Its behavior is unchanged from `Publish-CoverageComparison.ps1`, except
where noted.

1. **Context.**
   - From Cake: `GitHubActions.Environment.Workflow.Repository` and `.BaseRef`.
   - From the event payload at `GitHubActions.Environment.Workflow.EventPath` (`GITHUB_EVENT_PATH`):
     `pull_request.number`, `.head.sha`, `.head.repo.full_name` and `.user.login`. Cake's `Workflow.Sha` is the merge
     commit, not the head, so the payload is the source. `ci.yml` no longer passes these as environment variables.
   - Outside a GitHub Actions `pull_request` run, the task fails with a clear message.
2. **Read-only token.** A pull request from a fork, from a fork that was since deleted (`head.repo` is `null`), or one
   authored by `dependabot[bot]` has a read-only token. In that case:
   - the comparison goes to the step summary
   - `GitHubActions.Commands.Notice(…)` reports the result
   - the task ends successfully
3. **Baseline.**
   - First, from the successful runs of the workflow (`ci.yml`) at the merge base (`git merge-base origin/<base>
     <head>`), via `gh run list` and `gh run download --name coverage-summary`.
   - Failing that, from the latest successful push run of the base branch. The label then says that the merge base
     has no coverage.
   - Downloads go to a fresh temporary folder.
4. **Publishing.**
   - Step summary.
   - The sticky comment: PATCH the comment that starts with the marker, else POST a new one.
   - A completed `Coverage` check run on the head SHA with the conclusion, the title and the Markdown as its
     summary.
   - Failures from `gh` fail the task.
   - Request bodies (comment, check run) are JSON built in C# and passed with `gh api --input <file>`, so Markdown
     never goes through command-line quoting. Existing comments are listed with `gh api --paginate --slurp` and
     searched in C#.

`GitHubCli` wraps `gh` and `git` through Cake's process runner (`StartProcess` with redirected output). It returns
stdout and throws `CakeException` on non-zero exits, except for calls documented as allowed to fail
(`gh run download`).

## 8. Typstium configuration

`Build` passes these values (in `TypstiumCoverage.cs`):

| Option | Value |
|---|---|
| Products | names of `src/*/*.csproj` |
| Comment marker | `<!-- typstium-coverage -->` |
| Comment footer | `<sub>Unit and integration tests, Windows and Ubuntu merged. The full tables are in the CI run's coverage job summary.</sub>` |
| Job summary heading | `### Code coverage (unit and integration tests)` |
| Workflow file | `ci.yml` |
| Summary artifact name | `coverage-summary` |
| Max line decrease | `--max-line-decrease`, default `0.5` |
| Top classes | 10 |

## 9. Testing

`Build.Cake.Extensions.Tests` uses TUnit on Microsoft.Testing.Platform, with versions from
`Directory.Packages.props`. The `Default` target runs it as `BuildTests`.

**Golden files, for parity.** Before the PowerShell scripts are deleted, they run on the fixtures, and their outputs
are committed as expected files:

- `Write-CoverageReport.ps1` with `GITHUB_STEP_SUMMARY` pointed at a file, producing the job-summary Markdown and
  `summary.json`
- `Compare-Coverage.ps1`, producing the comment Markdown and the result JSON (conclusion and title)

The C# ports must reproduce these files byte for byte.

**Fixtures:**

- A real merged `Cobertura.xml` from a local `--coverage` run, with matching `PublicAPI.*.txt` files copied under
  `Fixtures/src/Typstium/`.
- Small handwritten Cobertura files:
  - nested types
  - generic types (`Foo<T>`)
  - a partial class split across files
  - a product without PublicAPI files
  - a file outside `src/<assembly>/`
  - zero totals
- Summary pairs for comparison:
  - no baseline
  - passing
  - failing
  - exactly −0.5 pp, which passes
  - a namespace added and one removed
  - unchanged
- One baseline `summary.json` written by the PowerShell version, to prove the reader accepts it.

**Focused unit tests:**

- path normalization (Windows path, Linux path, `/tests/`, neither)
- condition-coverage parsing
- "method called"
- namespace resolution for nested and generic types
- area for root files
- public-type parsing (comments, members with `->`, generics)
- threshold boundary
- rounding at .x5

**Console rendering.** Spectre's `TestConsole` with a fixed width checks that each table renders its rows, and that
nothing wraps at the configured width.

**Not unit-tested:** `CoverageMerger`, `GitHubCli` and `PullRequestCoverage`. The pull request for this change is
their end-to-end test:

- Its CI runs both bootstraps.
- It merges coverage across platforms.
- It compares against a PowerShell-written baseline on `main`, so its sticky comment and check run come from the new
  code.

## 10. CI and dependencies

**`ci.yml`:**

- Build matrix:
  - Windows: `./build.ps1 --coverage --no-job-summary` (shell `pwsh`)
  - Ubuntu: `./build.sh --coverage --no-job-summary` (shell `bash`)

  These are two steps, selected with `if: runner.os == …`.
- Coverage job:
  - `./build.sh --target=CoverageReport`
  - then, on pull requests, `./build.sh --target=CoverageComparison --max-line-decrease=$MAX_LINE_DECREASE`, with
    only `GH_TOKEN`. The pull request's details come from the event payload, and the repository and base branch from
    `GITHUB_REPOSITORY` and `GITHUB_BASE_REF`.
- Latest-Typst job: `./build.sh`.
- Unchanged: artifact names and paths, permissions, pinned action SHAs.
- The comments in `ci.yml` are updated to the new commands.

**`Directory.Packages.props`.** Add:

- `Cake.Common` (6.3.0, matching Cake.Sdk)
- `Cake.BuildSystems.Module`, which groups each task in the GitHub Actions log
- `ReportGenerator.Core` (5.5.11)
- `Spectre.Console` and `Spectre.Console.Testing` (0.57.2, the version Cake.Cli ships)

**`dependabot.yml`.** Add a `cake` group to the NuGet ecosystem with patterns `Cake.*` and `Spectre.Console*`, so
Cake.Sdk (in `global.json` `msbuild-sdks`), Cake.Common and Spectre.Console move together. ReportGenerator.Core
joins the `build-tooling` group.

Spectre.Console is pre-1.0, and Cake.Cli is compiled against 0.57.2. `build/Build` references it directly, so NuGet
fails the build (NU1605, a package downgrade) when a Cake update needs a newer Spectre.Console than the pinned one.
The skew can't pass silently.

**`.gitattributes`.** Add `build.sh text eol=lf`.

**README.** The "Building" section shows the new commands.

## 11. Migration

On branch `chore/cake-sdk`, one pull request:

1. This spec, then an implementation plan in `docs/plans/`.
2. Fixtures, and golden files generated by the current PowerShell scripts.
3. `Build.Cake.Extensions` and its tests: pure units test-first, until the golden files are green.
4. The `Build` project, the bootstraps, the `global.json`, `Directory.Packages.props` and `.gitattributes` changes.
   Then a local run of `./build.ps1 --coverage` and `./build.sh` (WSL), side by side with the old script's results.
5. CI switched over, and `dependabot.yml` updated.
6. The PowerShell scripts under `build/` deleted, and the README updated.
7. A pull request whose own coverage comment and check run are the end-to-end check.

## 12. Risks

| Risk | Mitigation |
|---|---|
| Spectre.Console version skew with Cake.Cli breaks binary compatibility | Pinned to Cake.Cli's version; moved only with Cake. |
| Spectre wraps tables in CI logs | Fixed console width when output is redirected; tested with `TestConsole`. |
| `ReportGenerator.Core`'s in-process API differs from the CLI's behavior | Same arguments (`reports`, `targetdir`, `reporttypes:Cobertura`, `assemblyfilters`, `verbosity:Warning`). The golden report input is produced by the CLI. A local run is compared with the old script's `report/Cobertura.xml`. |
| `dotnet run` adds start-up time to every build | Incremental builds of `build/Build` take seconds. Acceptable next to the test suites. |
| The root `Directory.Build.props` (warnings as errors, latest C#) trips on Cake.Sdk's generated code | Fix in `build/Directory.Build.props` with narrowly scoped `NoWarn`, never by turning off `TreatWarningsAsErrors`. |
| Baselines written by the PowerShell version on `main` | Schema unchanged. A test reads a PowerShell-written summary. |
