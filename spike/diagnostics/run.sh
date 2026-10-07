#!/usr/bin/env bash
# THROWAWAY SPIKE SCRIPT. Captures Typst CLI diagnostics in both formats.
# Usage: spike/diagnostics/run.sh [path-to-typst]   (run from repo root)
set -u
TYPST="${1:-typst}"
ROOT=spike/diagnostics/root
OUT=spike/diagnostics/output
mkdir -p "$OUT" spike/out/diag
# Empty package dirs so case 09 never hits a real cache; network may still be tried.
PKG=spike/out/empty-packages
mkdir -p "$PKG/path" "$PKG/cache"

for f in "$ROOT"/*.typ; do
  name=$(basename "$f" .typ)
  for fmt in human short; do
    start=$(date +%s%N)
    timeout 30 "$TYPST" --color never compile --root "$ROOT" --diagnostic-format "$fmt" \
      --package-path "$PKG/path" --package-cache-path "$PKG/cache" \
      "$f" "spike/out/diag/$name.pdf" >"$OUT/$name.$fmt.stdout" 2>"$OUT/$name.$fmt.stderr"
    code=$?
    ms=$(( ($(date +%s%N) - start) / 1000000 ))
    echo "exit=$code elapsed_ms=$ms" >"$OUT/$name.$fmt.meta"
  done
  echo "== $name ($(cat "$OUT/$name.short.meta"))"
  cat "$OUT/$name.short.stderr"
done
