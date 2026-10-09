# Foundation follow-ups

Deferred findings and execution rulings from the subagent-driven implementation of
[the foundation plan](2026-10-08-foundation.md) (Oct 8, 2026). None blocks the foundation; triage before the first NuGet release.

## Rulings made during execution

- Ruling: Implicit usings enabled in src although spec section 3 says off — plan deviation 7; all plan code depends on them — if wrong, add explicit usings per file (mechanical).
- Ruling: Plan deviations 1–6 (canonical roots, job Terminate, WinGet/PATH locator scope, CompileAsync default token, TYPST1008 scope, public result factories) are followed as written — they fix real defects found while planning; spec decisions table to be updated D22+ — if wrong, each is a localized revert.
- Ruling: Work in the main checkout on spike/cli-and-markdown rather than a new worktree — user approved this branch for the work; no main-branch commits — if wrong, cherry-pick onto a worktree branch.
- Ruling: Commit trailer stays "Claude Opus 5.5" (plan + this session's attribution) although subagents' harness suggests Sonnet — the controller session's attribution governs commits for this branch — if wrong, amend trailers before merge.
- Ruling: Add PolyArgumentExceptions=true to Directory.Build.props (plan gap in Task 1) — Polyfill only compiles ArgumentException/ArgumentNullException/ArgumentOutOfRangeException guard helpers with this switch; every later task uses them on netstandard2.0 — if wrong, replace guards with explicit checks per file.
- Ruling: Commit trailers name the model that wrote the commit (subagent harness attribution), superseding the Task 1 trailer ruling — accurate attribution; if wrong, a trailer rewrite before merge.
- Ruling: Regex validators anchor with \z, not $ (plan-mandated finding, Task 2) — $ accepts a trailing newline; applies to every later validating regex (Task 7 PdfStandard, Task 10/11/13 parsers where they validate input) — if wrong, cosmetic only.
- Ruling: Order-sensitive collection assertions use CollectionOrdering.Matching (TUnit IsEquivalentTo ignores order by default; plan tests intended order) — added to global-constraints.md for all later tasks — if wrong, tests are merely stricter.
- Ruling: Partial <param> docs on records get the missing <param> tags (CS1573 breaks the build) — plan text omitted them in several records — if wrong, doc-only.
- Ruling: Sandbox paths are scrubbed from diagnostic message/hint/trace text, root+separator replaced by '/' (Task 11 Important; plan protected only locations) — spec 4.12 'sandbox paths never appear'; if wrong, messages slightly less literal than Typst's.
- Ruling: netstandard2.0 build on non-Windows (no job object, non-tree Kill) is out of scope — .NET 8+ hosts there get the net8.0 build with Kill(entireProcessTree); .NET Framework is Windows-only — if wrong, grandchildren could outlive a timeout on Mono.
- Ruling: CliTypstCompilerSettings is an allowed exception to the Typst-prefix rule, alongside CliTypstCompiler (plan-mandated pair naming) — if wrong, a rename before first release.
- Ruling: Per-target PublicAPI.Unshipped.<tfm>.txt for record <Clone>$ differences — compiler-generated, unavoidable — if wrong, cosmetic.
- (Superseded in PR #1 review: suppression removed; overload families now use explicit no-token / required-token pairs, spec D36.) Ruling: RS0026/RS0027 suppressed in Typstium.csproj — spec 4.6 deliberately uses CancellationToken = default overload families (HttpClient pattern) — if wrong, API redesign of extension overloads before 1.0.
- Final: parked — root-containment test skips on CI (needs a non-writing variant) — Ruling: production fix is a one-liner covered by canonicalizer tests; add the always-running test as a follow-up — cost if wrong: a regression in drive-root file sources goes unnoticed.
- Final: parked — quoted TYPST_PATH gives a misleading "(relative paths are not allowed)" message; char.IsLetter accepts non-ASCII drive letters — Ruling: safe-side behavior, follow-up — cost if wrong: confusing error text.
- Final: parked — spec body text still says WinGet "newest first" and PATH "PATHEXT" (contradicts D24); ~~TypstTemplateName accepts trailing-dot segments~~ (fixed with review #3, D43) — Ruling: follow-up — cost if wrong: doc inconsistency / name aliasing on Windows.

## Deferred minor findings (per task)

- Task 1: minor (deferred): Typstium.Testing references src before use; PublicApiAnalyzers version declared before use (both plan-mandated, used later)
- Task 2: minor (deferred): TypstTemplateName accepts all-dots segments like "..."; TypstDiagnosticId rejection test asserts only non-null; no tests for TypstTemplateVersion/TypstPosition; no XML docs on Segments/FileName/Value
- Task 3: minor (deferred): WindowsArgumentEscaperTests case "a\\b" was weakened to "a\b" (restore brief literal); sync Assert.Throws; ToValues allocates per call
- Task 4: minor (deferred): TypstDiagnostic record equality compares ImmutableArray Hints/Trace by reference; exception ctors lack XML docs; untested branches (FeatureNotSupported with null required, OutputException with inner)
- Task 5: minor (deferred): TypstTemplate enumerates files 3x before snapshot; FromBytes keeps caller memory (mutable source); MemoryTemplateFile copies on every open; untested null/empty/blank cases
- Task 6: minor (deferred): FromBytes keeps caller memory (replay can change); record equality by reference for memory/arrays; record hierarchy closable via copy ctor (theoretical); VirtualFileSet internal in root namespace (plan); test gaps (null entry, FromBytes, root guard, ParamName)
- Task 7: minor (deferred): records with ImmutableArray compare by reference (TypstPageSelection/TypstPdfOptions/TypstDocument); rejection tests lack ParamName/edge cases; Paged.SaveAsync partial-write behavior undocumented; PdfStandard lacks ==
- Task 8: minor (deferred): untested null Inputs value / MemoryLimitBytes=0 / negative Jobs; EnsureSuccess doc could cref TypstCompileException; record equality by reference for FontPaths/Inputs
- Task 9: minor (deferred): builder fluent methods duplicated across two builders (plan-mandated); public builder/extension members lack XML docs; thin coverage for JsonTypeInfo/options overloads, ForFile(path,root), direct CompileAsync(source), null guards
- Task 10: minor (deferred): capture-diagnostics.sh lacks a post-check that {ROOT} replaced; Linux captures use / separators (document); ParseLocation endColumnCarets param always null (dead code, brief-verbatim); Header regex uses $ (non-validating); DiagnosticFixtures throws if a typst-* folder lacks diagnostics/; 10-multiple-errors capture holds one error
- Task 11: minor (deferred): Scrub skips bare root followed by "." (sentence end); unmapped trace-frame locations lose location without file-name fallback; duplicated mapper setup in assembler tests
- Task 12: minor (deferred): canonicalizer TrimEnd breaks filesystem roots (C:\ or /); ResolveLinkTarget does not re-walk target path; no PathCanonicalizer test; cleanup exceptions other than IO/UnauthorizedAccess fail first compile; ~~cleaner age relies on dir mtime (orphanAge must exceed timeouts)~~ (the lease makes age irrelevant for live folders, D44)
- Task 13: minor (deferred): feature-name semantics (disabled features still listed) worth a comment; no ParseVersionOutput failure test; sync Assert.Throws
- Task 14: minor (deferred): CommandLineLimits ParamName "request" misleading for direct callers; MaxLength constant only indirectly asserted; env remove list not de-duplicated
- Task 15: minor (deferred): natural-exit vs timeout race reports TimedOut; stderr truncation may split UTF-8 char; spec values (Timeout, MemoryLimit on 32-bit) validated only after Start; int casts on huge limits; ~~ns2.0 pipes block thread-pool threads~~ (fixed in PR #1, all target frameworks); tests lack memory-limit/boundary/late-cancel cases
- Task 16: minor (deferred): no test that cancelling one of two concurrent callers leaves the other served (probe once); default: branch message says timeout for OutputLimitExceeded too; unbounded cache / case-sensitive path keys; OCE-faulted probe edge; exit 2 heuristic for missing info
- Task 16: minor (deferred): eviction test uses fixed 200ms margin (could gate on TaskCompletionSource)
- Task 17: minor (deferred, PRIORITY for final triage): PATH locator accepts drive-relative (C:foo) and root-relative (\foo) entries and returns non-fully-qualified candidates; relative TYPST_PATH resolves against cwd — binary-planting residue vs spec
- Task 17: minor (deferred): [UnsupportedOSPlatform] on EnsureExecutable instead of OperatingSystem.IsWindows() guard; EnsureExecutable no-op on ns2.0 Unix; missing tests (WellKnownLocations, CreateDefault(explicit) chain)
- Task 18: minor (deferred, PRIORITY for final triage): paged outputs fully read into memory before MaxOutputBytes check (Untrusted preset bound not honored for many-page PNG/SVG) — sum FileInfo.Length first
- Task 18: minor (deferred): command line always Windows-quoted for display; untested paths (unsupported PDF standard, paged limit, no pages written, cancel while waiting for semaphore, telemetry Fail); info probe outside concurrency limit (comment)
- Task 19: minor (deferred): no tests for name.typ-over-main.typ precedence, nested template names, nested files via directory provider; hasher cancellation only via ReadAsync; provider ctors lack XML docs
- Task 20: minor (deferred): memory-cap test accepts any non-success (should exclude TimedOut); release-notes test needs network on first run (CI opt-in/skip); executable lookup cached statically across tests
- Task 20: minor (deferred): page-order test does not assert the three pages differ
- Task 21: minor (deferred): (NoWarn comment obsolete — suppression removed) actions pinned only to major tags

## Parked after the final review

- Final: parked — root-containment test skips on CI (needs a non-writing variant) — Ruling: production fix is a one-liner covered by canonicalizer tests; add the always-running test as a follow-up — cost if wrong: a regression in drive-root file sources goes unnoticed.
- Final: parked — quoted TYPST_PATH gives a misleading "(relative paths are not allowed)" message; char.IsLetter accepts non-ASCII drive letters — Ruling: safe-side behavior, follow-up — cost if wrong: confusing error text.
- Final: parked — spec body text still says WinGet "newest first" and PATH "PATHEXT" (contradicts D24); ~~TypstTemplateName accepts trailing-dot segments~~ (fixed with review #3, D43) — Ruling: follow-up — cost if wrong: doc inconsistency / name aliasing on Windows.

## Open gates

- External review (2026-10-08): all five findings fixed in PR #1; the review document was deleted once they were.
  - ~~#1 [P1] quadratic trace-frame regex in the diagnostic parser~~ Fixed: hand-written linear parser (D42), with a 1 MB unit test and an end-to-end timing test that timed out on the old parser.
  - ~~#2 [P2] orphan cleanup can delete live workspaces of another compiler sharing `TempRoot`~~ Fixed: a `.lease` file held open while a scratch folder is in use, and the workspace prepared inside the concurrency slot (D44).
  - ~~#3 [P2] Windows path aliases (trailing dots and spaces) and reserved device names~~ Fixed: portable-name rules for virtual files, template bundles and template names, plus `CreateNew` in the sandbox (D43).
  - ~~#4 [P3] extended UNC (`\\?\UNC\`) diagnostic paths are neither mapped nor scrubbed~~ Fixed: `SourcePathMapper` converts verbatim forms to ordinary paths and scrubs `\\?\UNC\` roots; an integration test compiles on `\\localhost\<drive>$` (skipped where the admin share isn't reachable).
  - ~~#5 [P3] the template version hash framing is ambiguous when file contents contain NUL bytes~~ Fixed: length-prefixed framing with a format tag, file count and per-file content hashes (spec 4.9).

- ~~CI has not run yet~~ Done in PR #1: green on windows-latest, ubuntu-latest and the latest-Typst job after fixing relative diagnostic paths on Linux (`setup-typst@v5` confirmed). macOS is still untested.
- Decide record value-equality semantics across the public API before the first release. (The XML-docs pass is done in PR #1: every public member is documented, and with the CS1591 suppression removed, the build fails on any undocumented public member.)
- Open question (before v1): a virtual file with the same path as a template file silently replaces it, because virtual files are written after the bundle. Keep this as a documented override, or reject the collision? Note also that a path differing only by case replaces the file on Windows and macOS but sits beside it on Linux.
- Later (only if a child-spawning scenario appears): a process-tree walk for the netstandard2.0 build on Windows (`CreateToolhelp32Snapshot` by parent ID, on the kill's dedicated thread). Today a child started between `Start()` and the job attach escapes the job there and can hold the pipes until it exits; Typst starts no children. `Timeout_kills_children_started_before_the_process_joined_its_containment` is skipped on .NET Framework until then.
- Later: a queueing decorator over `ITypstCompiler` (in the style of `DelegatingHandler`), if hosts need policy the `MaxConcurrency` semaphore lacks: a bounded queue that rejects work when full (backpressure), ordering (interactive before batch, fairness between tenants), and queue metrics (wait time, length). Not a background thread: compiles wait on a process, so no thread needs to be held. Use "priority" for queue ordering there; the OS setting is `ProcessPriority` (D39).
- Later: computed input values, `WithInput(key, Func<string>)` on the builders, resolved at `Build()` (D38). Add when a concrete use case appears; an async variant would need `BuildAsync`.
- Later: a per-input way to mark a value safe to log (keys stay visible; values are redacted today), if diagnosing compiles needs it (D38).
- Later (only if it shows up): Unicode normalization in duplicate detection. macOS can treat composed and decomposed forms of a name (`é`) as the same file; `CreateNew` in the sandbox already turns such a collision into an error rather than an overwrite.
- Later: a Cake build that replaces the body of `build.ps1` (same step order as CI; D41).
- Later: publish API reference and guides to GitHub Pages, generated from the XML docs (for example with docfx), and build it in CI so broken doc references fail the build.
- ~~Investigate slow timeouts on windows-latest~~ Found and fixed in PR #1. Pipe reads used `ReadAsync` on the process's synchronous pipe streams (on every target framework, not only netstandard2.0), so each running compile blocked two thread-pool threads. With every concurrency slot busy, the pool starved, and the timer callback that kills Typst on timeout fired late: CI showed `process 8510 ms` for a 2 s timeout, and locally (32 cores) a second wave of compiles ran for 36 s. Pipe I/O now runs on dedicated threads (`DedicatedThread`), and `Timeouts_fire_on_time_when_every_concurrency_slot_is_busy` guards it. A second cause showed up on a 2-core run: `Process.Kill(entireProcessTree: true)` snapshots every process on the machine and took seconds under load, holding up other compiles' timeout callbacks. On Windows the job object is now terminated first (one system call) and the tree walk is skipped. The remaining delay was CPU oversubscription in the test run itself, so `LimitsTests` runs `[NotInParallel]` and CI runs test modules one at a time. Typstium doesn't touch `ThreadPool.SetMinThreads`: that's the host's setting.
- Open question (before v1): trim the `(x, output, options)` convenience overloads in `TypstCompilerExtensions` and leave options to the builders, roughly halving the extension surface (spec 4.6).
