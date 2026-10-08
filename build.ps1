#!/usr/bin/env pwsh
# Builds and tests Typstium exactly as CI does; CI runs this script, so the two can't drift apart.
# Order: build, unit tests, integration tests (both in parallel internally), then the timing tests alone,
# because they assert wall-clock bounds that other compiles running alongside would disturb.
# Integration and timing tests need Typst; set TYPSTIUM_REQUIRE_TYPST=1 to fail instead of skip without it.
[CmdletBinding()]
param(
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Set-Location $PSScriptRoot

function Invoke-Step([string] $Name, [scriptblock] $Command) {
    Write-Host "==> $Name" -ForegroundColor Cyan
    & $Command
    if ($LASTEXITCODE -ne 0) {
        Write-Host "$Name failed (exit code $LASTEXITCODE)." -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

Invoke-Step 'Build' { dotnet build --configuration $Configuration }
Invoke-Step 'Unit tests' { dotnet test --project tests/Typstium.UnitTests --configuration $Configuration --no-build }
Invoke-Step 'Integration tests' { dotnet test --project tests/Typstium.IntegrationTests --configuration $Configuration --no-build }
Invoke-Step 'Timing tests' { dotnet test --project tests/Typstium.TimingTests --configuration $Configuration --no-build --max-parallel-test-modules 1 }
