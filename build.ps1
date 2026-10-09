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

function Get-MissedLines($Counts) {
    return $Counts.Lines - $Counts.CoveredLines
}

# Biggest contributor to the coverage gap first: most uncovered lines, then most uncovered branches, then name.
function Sort-ByGap($Items, [scriptblock] $Counts, [scriptblock] $Name) {
    return $Items | Sort-Object `
        @{ Expression = { $c = & $Counts $_; $c.Lines - $c.CoveredLines }; Descending = $true },
        @{ Expression = { $c = & $Counts $_; $c.Branches - $c.CoveredBranches }; Descending = $true },
        @{ Expression = $Name }
}

# Rows: per assembly (biggest gap first), its namespaces (biggest gap first) then a subtotal; finally the total.
function Get-CoverageRows($Summary) {
    $rows = [System.Collections.Generic.List[object]]::new()
    $total = New-Counts
    $assemblies = foreach ($assembly in $Summary.coverage.assemblies) {
        $subtotal = New-Counts
        Add-Counts $subtotal $assembly
        [pscustomobject]@{ Assembly = $assembly; Counts = $subtotal }
    }

    foreach ($entry in Sort-ByGap $assemblies { param($e) $e.Counts } { $_.Assembly.name }) {
        $assembly = $entry.Assembly
        $classNames = [System.Collections.Generic.HashSet[string]]::new([string[]]@($assembly.classesinassembly | ForEach-Object { ($_.name -split '<')[0] }))
        $namespaces = [ordered]@{}
        foreach ($class in $assembly.classesinassembly) {
            $namespace = Get-Namespace $class.name $classNames
            if (-not $namespaces.Contains($namespace)) {
                $namespaces[$namespace] = New-Counts
            }

            Add-Counts $namespaces[$namespace] $class
        }

        foreach ($namespace in Sort-ByGap @($namespaces.Keys) { param($n) $namespaces[$n] } { $_ }) {
            $rows.Add([pscustomobject]@{ Kind = 'Namespace'; Name = $namespace; Counts = $namespaces[$namespace] })
        }

        Add-Counts $total $assembly
        $rows.Add([pscustomobject]@{ Kind = 'Assembly'; Name = "$($assembly.name) (assembly)"; Counts = $entry.Counts })
    }

    $rows.Add([pscustomobject]@{ Kind = 'Total'; Name = 'Total'; Counts = $total })
    return $rows
}

# A row's share of all uncovered lines: what closing its gap would do for the total.
function Format-Gap([int] $Missed, [int] $TotalMissed) {
    if ($TotalMissed -eq 0) {
        return '-'
    }

    return [string]::Format([cultureinfo]::InvariantCulture, '{0:0.0}%', 100.0 * $Missed / $TotalMissed)
}

function Get-RowCells($Row, [int] $TotalMissed) {
    $missed = Get-MissedLines $Row.Counts
    return @(
        [string]$missed,
        (Format-Gap $missed $TotalMissed),
        (Format-Rate $Row.Counts.CoveredLines $Row.Counts.Lines),
        (Format-Rate $Row.Counts.CoveredBranches $Row.Counts.Branches),
        (Format-Rate $Row.Counts.CoveredMethods $Row.Counts.Methods))
}

$CoverageHeaders = @('Missed', 'Gap', 'Line', 'Branch', 'Method (called)')

function Write-CoverageTable($Rows) {
    $totalMissed = Get-MissedLines ($Rows | Where-Object Kind -eq 'Total').Counts
    $names = $Rows | ForEach-Object { $(if ($_.Kind -eq 'Namespace') { '  ' + $_.Name } else { $_.Name }) }
    $cells = @($Rows | ForEach-Object { , (Get-RowCells $_ $totalMissed) })
    $nameWidth = [Math]::Max(9, ($names | Measure-Object -Property Length -Maximum).Maximum)
    $widths = for ($c = 0; $c -lt $CoverageHeaders.Count; $c++) {
        [Math]::Max($CoverageHeaders[$c].Length, ($cells | ForEach-Object { $_[$c].Length } | Measure-Object -Maximum).Maximum)
    }

    # Missed and Gap are right-aligned numbers; the rate columns are left-aligned text.
    function Format-Line([string] $Name, [string[]] $Values) {
        $parts = for ($c = 0; $c -lt $Values.Count; $c++) {
            $(if ($c -lt 2) { $Values[$c].PadLeft($widths[$c]) } else { $Values[$c].PadRight($widths[$c]) })
        }

        return ($Name.PadRight($nameWidth) + '  ' + ($parts -join '  ')).TrimEnd()
    }

    $rule = ('-' * $nameWidth) + '  ' + (@($widths | ForEach-Object { '-' * $_ }) -join '  ')
    Write-Host ''
    Write-Host (Format-Line 'Namespace' $CoverageHeaders)
    Write-Host $rule
    for ($r = 0; $r -lt $Rows.Count; $r++) {
        if ($Rows[$r].Kind -eq 'Total') {
            Write-Host $rule
        }

        Write-Host (Format-Line $names[$r] $cells[$r]) -ForegroundColor $(if ($Rows[$r].Kind -eq 'Namespace') { 'Gray' } else { 'White' })
    }

    Write-Host ''
    Write-Host 'Sorted by missed lines. Gap: share of all uncovered lines. Method (called): methods entered at least once.'
    Write-Host ''
}

function Write-CoverageJobSummary($Rows) {
    if (-not $env:GITHUB_STEP_SUMMARY) {
        return
    }

    $totalMissed = Get-MissedLines ($Rows | Where-Object Kind -eq 'Total').Counts
    $markdown = [System.Text.StringBuilder]::new()
    [void]$markdown.AppendLine('### Code coverage (unit and integration tests)')
    [void]$markdown.AppendLine()
    [void]$markdown.AppendLine("| Namespace | $($CoverageHeaders -join ' | ') |")
    [void]$markdown.AppendLine('| --- | ---: | ---: | ---: | ---: | ---: |')
    foreach ($row in $Rows) {
        $name = $(if ($row.Kind -eq 'Namespace') { "&nbsp;&nbsp;$($row.Name)" } else { "**$($row.Name)**" })
        $values = Get-RowCells $row $totalMissed | ForEach-Object { ($_ -replace '\s+', ' ').Trim() }
        if ($row.Kind -ne 'Namespace') {
            $values = $values | ForEach-Object { "**$_**" }
        }

        [void]$markdown.AppendLine("| $name | $($values -join ' | ') |")
    }

    [void]$markdown.AppendLine()
    [void]$markdown.AppendLine('Sorted by missed lines. *Gap*: share of all uncovered lines. *Method (called)*: methods entered at least once.')
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
