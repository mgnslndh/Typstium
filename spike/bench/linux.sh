#!/usr/bin/env bash
# THROWAWAY SPIKE SCRIPT. Run inside Linux (e.g. WSL) from the repo root:
#   wsl -d Ubuntu -- bash spike/bench/linux.sh
# Downloads the official musl build, verifies its SHA256 and times compiles.
set -eu
REPO="$(pwd)"
D=/tmp/typstium-spike
VERSION=0.15.1
SHA=a6d077d0a95eed5a2eba715b2dae06be954f624ccbf85758a03f389ded33118c
rm -rf "$D"; mkdir -p "$D/bin" "$D/work"

cd "$D/bin"
curl -sSfL -o typst.tar.xz "https://github.com/typst/typst/releases/download/v$VERSION/typst-x86_64-unknown-linux-musl.tar.xz"
echo "$SHA  typst.tar.xz" | sha256sum -c -
tar -xJf typst.tar.xz --strip-components=1
T="$D/bin/typst"
"$T" --version
file "$T" 2>/dev/null || true
ldd "$T" 2>&1 | head -1 || true
grep PRETTY_NAME /etc/os-release
ldd --version | head -1

# Copy inputs into the Linux filesystem; /mnt/c I/O would skew timings.
cp -r "$REPO/samples" "$D/work/"
mkdir -p "$D/work/pkg"
cp -r "$REPO/spike/out/vendored/path/." "$D/work/pkg/"
cd "$D/work"
L=samples/templates/license
R=samples/templates/release-notes

time_it() { # name, runs, command...
  local name=$1 runs=$2; shift 2
  "$@" >/dev/null 2>&1 # warmup
  local start end
  start=$(date +%s%N)
  for _ in $(seq 1 "$runs"); do "$@" >/dev/null; done
  end=$(date +%s%N)
  echo "$name: $(( (end - start) / runs / 1000000 )) ms/compile (mean of $runs)"
}

echo "system font families: $("$T" fonts | wc -l)"
time_it "typst --version" 30 "$T" --version
time_it "license pdf, system fonts" 30 "$T" compile --root $L --font-path samples/fonts $L/license.typ l.pdf
time_it "license pdf, no system fonts" 30 "$T" compile --root $L --ignore-system-fonts --font-path samples/fonts $L/license.typ l.pdf
time_it "release notes, no system fonts" 30 "$T" compile --root $R --package-path pkg --package-cache-path empty --ignore-system-fonts --font-path samples/fonts $R/release-notes.typ rn.pdf
