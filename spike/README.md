# Spike (throwaway)

Everything in this folder is throwaway code for the CLI and Markdown spike. It isn't part of the library and
won't be maintained. Findings are in [`docs/spikes/2026-10-07-cli-and-markdown.md`](../docs/spikes/2026-10-07-cli-and-markdown.md).

Run from the repo root with Git Bash. Pass the path to `typst` if it isn't on `PATH`.

| Script | What it does |
| --- | --- |
| `diagnostics/run.sh [typst]` | Compiles each broken document in `diagnostics/root/` and captures stderr in `human` and `short` format to `diagnostics/output/` |
| `bench/bench.sh [typst] [outdir]` | hyperfine latency benchmarks for the samples (Windows results in `bench/results-windows/`) |
| `bench/linux.sh` | Run inside WSL/Linux: downloads the musl build, verifies SHA256 and times compiles |
| `scriban/render.cs` | .NET 10 file-based app: Scriban template + JSON model → Markdown, laid out in a sandbox folder |
| `security/root/` | raw-typst injection probe for cmarker (`default.typ`, `scoped.typ`, `wrapped.typ`, `safe.typ`) |

`bench.sh` expects cmarker vendored in `spike/out/vendored/path/` (see the offline section of the findings doc).
Generated files go to `spike/out/`, which is git-ignored.
