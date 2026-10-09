#!/usr/bin/env bash
# THROWAWAY SPIKE SCRIPT. Measures per-call Typst CLI latency with hyperfine.
# Usage: spike/bench/bench.sh [path-to-typst] [output-dir]   (run from repo root)
set -eu
TYPST="${1:-typst}"
OUT="${2:-spike/bench/results}"
mkdir -p "$OUT" spike/out/bench

LIC=samples/templates/license
RN=samples/templates/release-notes
FONTS=samples/fonts
PKG=spike/out/vendored/path   # cmarker vendored by the offline probe
EMPTY=spike/out/vendored/emptycache
NOSYS="--ignore-system-fonts --font-path $FONTS"
WITHSYS="--font-path $FONTS"
LICARGS="--root $LIC"
RNARGS="--root $RN --package-path $PKG --package-cache-path $EMPTY"

printf '= Hello\nWorld\n' > spike/out/bench/hello.typ

common=(--warmup 3 --runs 30 -N)

# 1. Floor: process start, and the smallest possible document.
hyperfine "${common[@]}" --export-markdown "$OUT/1-floor.md" --export-json "$OUT/1-floor.json" \
  -n "typst --version" "$TYPST --version" \
  -n "hello, system fonts" "$TYPST compile spike/out/bench/hello.typ spike/out/bench/hello.pdf" \
  -n "hello, no system fonts" "$TYPST compile --ignore-system-fonts spike/out/bench/hello.typ spike/out/bench/hello.pdf"

# 2. License certificate (use case B): font strategy, format and I/O variants.
hyperfine "${common[@]}" --export-markdown "$OUT/2-license.md" --export-json "$OUT/2-license.json" \
  -n "license pdf, system fonts" "$TYPST compile $LICARGS $WITHSYS $LIC/license.typ spike/out/bench/l.pdf" \
  -n "license pdf, no system fonts" "$TYPST compile $LICARGS $NOSYS $LIC/license.typ spike/out/bench/l.pdf" \
  -n "license pdf, no system/embedded fonts" "$TYPST compile $LICARGS $NOSYS --ignore-embedded-fonts $LIC/license.typ spike/out/bench/l.pdf" \
  -n "license pdf, no system fonts, -j 1" "$TYPST compile -j 1 $LICARGS $NOSYS $LIC/license.typ spike/out/bench/l.pdf" \
  -n "license pdf, PDF/A-2b" "$TYPST compile $LICARGS $NOSYS --pdf-standard a-2b $LIC/license.typ spike/out/bench/l.pdf" \
  -n "license png p1, no system fonts" "$TYPST compile $LICARGS $NOSYS --pages 1 $LIC/license.typ spike/out/bench/l.png"

# 3. Stdin/stdout vs files (what the core would do with virtual files).
hyperfine "${common[@]}" --export-markdown "$OUT/3-stdio.md" --export-json "$OUT/3-stdio.json" \
  -n "license file -> file" "$TYPST compile $LICARGS $NOSYS $LIC/license.typ spike/out/bench/l.pdf" \
  --input "$LIC/license.typ" --output pipe \
  -n "license stdin -> stdout" "$TYPST compile $LICARGS $NOSYS - -"

# 4. Release notes (use case A): cmarker WASM plugin + Markdown.
hyperfine "${common[@]}" --export-markdown "$OUT/4-release-notes.md" --export-json "$OUT/4-release-notes.json" \
  -n "release notes, system fonts" "$TYPST compile $RNARGS $WITHSYS $RN/release-notes.typ spike/out/bench/rn.pdf" \
  -n "release notes, no system fonts" "$TYPST compile $RNARGS $NOSYS $RN/release-notes.typ spike/out/bench/rn.pdf"
