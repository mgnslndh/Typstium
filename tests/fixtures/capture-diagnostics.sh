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
