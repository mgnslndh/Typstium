#!/usr/bin/env bash
# Runs the build (build/Build, Cake.Sdk); arguments pass through, e.g. --coverage or --target=CoverageReport.
# See docs/specs/cake-sdk-build.md. build.ps1 does the same in PowerShell.
set -euo pipefail
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
exec dotnet run --project "$script_dir/build/Build/Build.csproj" -- "$@"
