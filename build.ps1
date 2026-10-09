#!/usr/bin/env pwsh
# Builds and tests Typstium exactly as CI does; CI runs this script, so the two can't drift apart.
# Order: build, unit tests, integration tests (both in parallel internally), then the timing tests alone,
# because they assert wall-clock bounds that other compiles running alongside would disturb.
# Integration and timing tests need Typst; set TYPSTIUM_REQUIRE_TYPST=1 to fail instead of skip without it.
#
# -Coverage collects code coverage from the unit and integration tests (not the timing tests: instrumentation
# would distort their timings), merges every test project and target framework, and prints a table per namespace
# of the products under src/ once all tests have passed. On GitHub Actions the table also goes to the job summary.
[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [switch] $Coverage
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

# The type's namespace: the prefix before the first segment that is itself a class in the report, so nested
# types (Typstium.Capabilities.TypstCliProfileFactory.ProfileKey) land in their outer type's namespace.
function Get-Namespace([string] $ClassName, [System.Collections.Generic.HashSet[string]] $ClassNames) {
    $name = ($ClassName -split '<')[0]
    $parts = $name.Split('.')
    for ($i = 1; $i -le $parts.Count; $i++) {
        if ($ClassNames.Contains($parts[0..($i - 1)] -join '.')) {
            return $(if ($i -gt 1) { $parts[0..($i - 2)] -join '.' } else { $parts[0] })
        }
    }

    return $(if ($parts.Count -gt 1) { $parts[0..($parts.Count - 2)] -join '.' } else { $name })
}

function New-Counts {
    [pscustomobject]@{ CoveredLines = 0; Lines = 0; CoveredBranches = 0; Branches = 0; CoveredMethods = 0; Methods = 0 }
}

function Add-Counts($Target, $Source) {
    $Target.CoveredLines += $Source.coveredlines
    $Target.Lines += $Source.coverablelines
    $Target.CoveredBranches += $Source.coveredbranches
    $Target.Branches += $Source.totalbranches
    $Target.CoveredMethods += $Source.coveredmethods
    $Target.Methods += $Source.totalmethods
}

function Format-Rate([int] $Covered, [int] $Total) {
    if ($Total -eq 0) {
        return '-'
    }

    $percent = [string]::Format([cultureinfo]::InvariantCulture, '{0:0.0}%', 100.0 * $Covered / $Total)
    return '{0,6}  ({1}/{2})' -f $percent, $Covered, $Total
}

# Rows: per assembly, its namespaces then a subtotal; finally the total over all assemblies.
function Get-CoverageRows($Summary) {
    $rows = [System.Collections.Generic.List[object]]::new()
    $total = New-Counts
    foreach ($assembly in $Summary.coverage.assemblies | Sort-Object name) {
        $classNames = [System.Collections.Generic.HashSet[string]]::new([string[]]@($assembly.classesinassembly | ForEach-Object { ($_.name -split '<')[0] }))
        $namespaces = [ordered]@{}
        foreach ($class in $assembly.classesinassembly) {
            $namespace = Get-Namespace $class.name $classNames
            if (-not $namespaces.Contains($namespace)) {
                $namespaces[$namespace] = New-Counts
            }

            Add-Counts $namespaces[$namespace] $class
        }

        foreach ($namespace in $namespaces.Keys | Sort-Object) {
            $rows.Add([pscustomobject]@{ Kind = 'Namespace'; Name = $namespace; Counts = $namespaces[$namespace] })
        }

        $subtotal = New-Counts
        Add-Counts $subtotal $assembly
        Add-Counts $total $assembly
        $rows.Add([pscustomobject]@{ Kind = 'Assembly'; Name = "$($assembly.name) (assembly)"; Counts = $subtotal })
    }

    $rows.Add([pscustomobject]@{ Kind = 'Total'; Name = 'Total'; Counts = $total })
    return $rows
}

function Write-CoverageTable($Rows) {
    $nameWidth = [Math]::Max(9, ($Rows | ForEach-Object { $_.Name.Length + 2 } | Measure-Object -Maximum).Maximum)
    $cells = $Rows | ForEach-Object {
        @((Format-Rate $_.Counts.CoveredLines $_.Counts.Lines), (Format-Rate $_.Counts.CoveredBranches $_.Counts.Branches), (Format-Rate $_.Counts.CoveredMethods $_.Counts.Methods))
    }
    $columnWidth = [Math]::Max(6, ($cells | ForEach-Object { $_.Length } | Measure-Object -Maximum).Maximum)
    $rule = ('-' * $nameWidth) + '  ' + (@(1..3 | ForEach-Object { '-' * $columnWidth }) -join '  ')

    Write-Host ''
    Write-Host ('{0}  {1}  {2}  {3}' -f 'Namespace'.PadRight($nameWidth), 'Line'.PadRight($columnWidth), 'Branch'.PadRight($columnWidth), 'Method')
    Write-Host $rule
    foreach ($row in $Rows) {
        $name = $(if ($row.Kind -eq 'Namespace') { '  ' + $row.Name } else { $row.Name })
        $line = '{0}  {1}  {2}  {3}' -f $name.PadRight($nameWidth),
            (Format-Rate $row.Counts.CoveredLines $row.Counts.Lines).PadRight($columnWidth),
            (Format-Rate $row.Counts.CoveredBranches $row.Counts.Branches).PadRight($columnWidth),
            (Format-Rate $row.Counts.CoveredMethods $row.Counts.Methods)
        if ($row.Kind -eq 'Total') {
            Write-Host $rule
        }

        Write-Host $line.TrimEnd() -ForegroundColor $(if ($row.Kind -eq 'Namespace') { 'Gray' } else { 'White' })
    }

    Write-Host ''
}

function Write-CoverageJobSummary($Rows) {
    if (-not $env:GITHUB_STEP_SUMMARY) {
        return
    }

    $markdown = [System.Text.StringBuilder]::new()
    [void]$markdown.AppendLine('### Code coverage (unit and integration tests)')
    [void]$markdown.AppendLine()
    [void]$markdown.AppendLine('| Namespace | Line | Branch | Method |')
    [void]$markdown.AppendLine('| --- | ---: | ---: | ---: |')
    foreach ($row in $Rows) {
        $name = $(if ($row.Kind -eq 'Namespace') { "&nbsp;&nbsp;$($row.Name)" } else { "**$($row.Name)**" })
        $rates = @(
            (Format-Rate $row.Counts.CoveredLines $row.Counts.Lines),
            (Format-Rate $row.Counts.CoveredBranches $row.Counts.Branches),
            (Format-Rate $row.Counts.CoveredMethods $row.Counts.Methods)) | ForEach-Object { ($_ -replace '\s+', ' ').Trim() }
        if ($row.Kind -ne 'Namespace') {
            $rates = $rates | ForEach-Object { "**$_**" }
        }

        [void]$markdown.AppendLine("| $name | $($rates -join ' | ') |")
    }

    Add-Content -Path $env:GITHUB_STEP_SUMMARY -Value $markdown.ToString() -Encoding utf8
}

function Invoke-CoverageReport {
    # The products are the projects under src/; anything under tests/ never counts.
    $products = Get-ChildItem -Path src -Filter *.csproj -Recurse | ForEach-Object { $_.BaseName }
    $filters = ($products | ForEach-Object { "+$_" }) -join ';'
    $report = Join-Path $CoverageDirectory 'report'
    dnx $ReportGenerator --yes -- "-reports:$(Join-Path $CoverageDirectory 'raw/*.cobertura.xml')" "-targetdir:$report" '-reporttypes:JsonSummary' "-assemblyfilters:$filters" '-verbosity:Warning'
    if ($LASTEXITCODE -ne 0) {
        return
    }

    $summary = Get-Content (Join-Path $report 'Summary.json') -Raw | ConvertFrom-Json
    $rows = Get-CoverageRows $summary
    Write-CoverageTable $rows
    Write-CoverageJobSummary $rows
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
