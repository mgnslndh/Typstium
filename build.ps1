#!/usr/bin/env pwsh
# Builds and tests Typstium exactly as CI does; CI runs this script, so the two can't drift apart.
# Order: build, unit tests, integration tests (both in parallel internally), then the timing tests alone,
# because they assert wall-clock bounds that other compiles running alongside would disturb.
# Integration and timing tests need Typst; set TYPSTIUM_REQUIRE_TYPST=1 to fail instead of skip without it.
#
# -Coverage collects code coverage from the unit and integration tests (not the timing tests: instrumentation
# would distort their timings), merges every test project and target framework with ReportGenerator, and once all
# tests have passed prints coverage tables for the products under src/ (build/Write-CoverageReport.ps1). On GitHub
# Actions the tables also go to the job summary, unless -NoJobSummary.
#
# -CoverageReportOnly skips build and tests and only merges and reports the raw coverage files already in
# artifacts/coverage/raw/, e.g. from several machines: CI's coverage job merges Windows and Ubuntu this way, so code
# counts as covered when any platform ran it.
[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [switch] $Coverage,
    [switch] $CoverageReportOnly,
    [switch] $NoJobSummary
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Set-Location $PSScriptRoot

$ReportGenerator = 'dotnet-reportgenerator-globaltool@5.5.11'
$CoverageDirectory = Join-Path $PSScriptRoot 'artifacts/coverage'

function Invoke-Step([string] $Name, [scriptblock] $Command) {
    Write-Host "==> $Name" -ForegroundColor Cyan
    & $Command
    if ($LASTEXITCODE -ne 0) {
        Write-Host "$Name failed (exit code $LASTEXITCODE)." -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

# Arguments that make a test run write Cobertura coverage; each run gets a GUID-named file, so target
# frameworks of one project don't overwrite each other.
function Get-CoverageArguments {
    if (-not $Coverage) {
        return @()
    }

    return @('--results-directory', (Join-Path $CoverageDirectory 'raw'), '--', '--coverage', '--coverage-output-format', 'cobertura')
}

# Copies the raw files with every source path made repo-relative (src/Typstium/X.cs). Each machine records absolute
# paths (D:\... on Windows, /home/runner/... on Ubuntu); without this, ReportGenerator would see one file per
# machine and count its lines twice, instead of merging them so a line counts as covered when any run hit it.
function ConvertTo-RepoRelativeCoverage([string] $Source, [string] $Target) {
    New-Item -ItemType Directory -Force $Target | Out-Null
    foreach ($file in Get-ChildItem -Path $Source -Filter *.cobertura.xml) {
        $text = [System.IO.File]::ReadAllText($file.FullName)
        $text = [regex]::Replace($text, 'filename="([^"]*)"', {
            param($match)
            $path = $match.Groups[1].Value -replace '\\', '/'
            $index = [Math]::Max($path.LastIndexOf('/src/'), $path.LastIndexOf('/tests/'))
            'filename="{0}"' -f $(if ($index -ge 0) { $path.Substring($index + 1) } else { $path })
        })
        [System.IO.File]::WriteAllText((Join-Path $Target $file.Name), $text)
    }
}

function Invoke-CoverageReport {
    # The products are the projects under src/; anything under tests/ never counts.
    $products = Get-ChildItem -Path src -Filter *.csproj -Recurse | ForEach-Object { $_.BaseName }
    $filters = ($products | ForEach-Object { "+$_" }) -join ';'
    $relative = Join-Path $CoverageDirectory 'relative'
    $report = Join-Path $CoverageDirectory 'report'
    foreach ($folder in $relative, $report) {
        if (Test-Path $folder) {
            Remove-Item -Recurse -Force $folder
        }
    }

    ConvertTo-RepoRelativeCoverage (Join-Path $CoverageDirectory 'raw') $relative
    dnx $ReportGenerator --yes -- "-reports:$(Join-Path $relative '*.cobertura.xml')" "-targetdir:$report" '-reporttypes:Cobertura' "-assemblyfilters:$filters" '-verbosity:Warning'
    if ($LASTEXITCODE -ne 0) {
        return
    }

    $reportArguments = @{ CoberturaPath = (Join-Path $report 'Cobertura.xml'); SourceDirectory = (Join-Path $PSScriptRoot 'src') }
    & (Join-Path $PSScriptRoot 'build/Write-CoverageReport.ps1') @reportArguments -NoJobSummary:$NoJobSummary
}

if ($CoverageReportOnly) {
    Invoke-Step 'Code coverage (merged raw files)' { Invoke-CoverageReport }
    exit 0
}

if ($Coverage -and (Test-Path $CoverageDirectory)) {
    Remove-Item -Recurse -Force $CoverageDirectory
}

$coverageArguments = Get-CoverageArguments
Invoke-Step 'Build' { dotnet build --configuration $Configuration }
Invoke-Step 'Unit tests' { dotnet test --project tests/Typstium.UnitTests --configuration $Configuration --no-build @coverageArguments }
Invoke-Step 'Integration tests' { dotnet test --project tests/Typstium.IntegrationTests --configuration $Configuration --no-build @coverageArguments }
Invoke-Step 'Timing tests' { dotnet test --project tests/Typstium.TimingTests --configuration $Configuration --no-build --max-parallel-test-modules 1 }

if ($Coverage) {
    Invoke-Step 'Code coverage (unit and integration tests)' { Invoke-CoverageReport }
}
