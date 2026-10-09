#!/usr/bin/env pwsh
# Runs the build (build/Build, Cake.Sdk); arguments pass through, e.g. --coverage or --target=CoverageReport.
# See docs/specs/cake-sdk-build.md. build.sh does the same without PowerShell.
$ErrorActionPreference = 'Stop'
dotnet run --project "$PSScriptRoot/build/Build/Build.csproj" -- @args
exit $LASTEXITCODE
