#!/usr/bin/env pwsh
# Prints code-coverage tables from a merged Cobertura report (ReportGenerator's), and on GitHub Actions also writes
# them to the job summary. Four views of the same numbers, all sorted by missed (uncovered) lines so the biggest
# contributor to the gap comes first:
#   1. Visibility: the public API against internal code. Public types are the bare type lines of the product's
#      PublicAPI.*.txt files, which the PublicAPI analyzer keeps complete; every other class is internal.
#   2. Namespace: the public API (the root namespace) and each internal component.
#   3. Area: each top-level folder of a product, so a feature's public and internal types share one row.
#   4. Top classes: the classes (per file) with the most missed lines, pointing at the code to test next.
# Gap is a row's share of all missed lines. A method counts as called once any of its lines ran.
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $CoberturaPath,
    # Holds one folder per product (src/<assembly>/), with its PublicAPI.*.txt files.
    [Parameter(Mandatory)] [string] $SourceDirectory,
    [int] $TopClasses = 10,
    # Skip the GitHub Actions job summary, e.g. in jobs whose coverage another job merges and reports.
    [switch] $NoJobSummary,
    # Also write the counts as JSON (totals and per namespace), for comparing runs (build/Compare-Coverage.ps1).
    [string] $SummaryPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function New-Counts {
    [pscustomobject]@{ CoveredLines = 0; Lines = 0; CoveredBranches = 0; Branches = 0; CoveredMethods = 0; Methods = 0 }
}

function Add-Counts($Target, $Source) {
    $Target.CoveredLines += $Source.CoveredLines
    $Target.Lines += $Source.Lines
    $Target.CoveredBranches += $Source.CoveredBranches
    $Target.Branches += $Source.Branches
    $Target.CoveredMethods += $Source.CoveredMethods
    $Target.Methods += $Source.Methods
}

function Get-Missed($Counts) {
    return $Counts.Lines - $Counts.CoveredLines
}

# One entry per class and file (partial classes are split per file), with its path relative to the product folder.
function Read-CoverageClasses([string] $Path) {
    $document = [xml](Get-Content $Path -Raw)
    foreach ($package in $document.SelectNodes('/coverage/packages/package')) {
        $assembly = $package.GetAttribute('name')
        foreach ($class in $package.SelectNodes('classes/class')) {
            $counts = New-Counts
            foreach ($line in $class.SelectNodes('lines/line')) {
                $counts.Lines++
                if ([int]$line.GetAttribute('hits') -gt 0) {
                    $counts.CoveredLines++
                }

                if ($line.GetAttribute('condition-coverage') -match '\((\d+)/(\d+)\)') {
                    $counts.CoveredBranches += [int]$Matches[1]
                    $counts.Branches += [int]$Matches[2]
                }
            }

            foreach ($method in $class.SelectNodes('methods/method')) {
                $counts.Methods++
                if ($method.SelectNodes('lines/line[@hits!="0"]').Count -gt 0) {
                    $counts.CoveredMethods++
                }
            }

            $file = $class.GetAttribute('filename') -replace '\\', '/'
            # Paths are absolute, or repo-relative (src/<assembly>/...) when merged from several machines.
            $marker = "src/$assembly/"
            $index = $(if ($file.StartsWith($marker)) { 0 } else { $file.IndexOf('/' + $marker) + 1 })
            $relative = $(if ($index -gt 0 -or $file.StartsWith($marker)) { $file.Substring($index + $marker.Length) } else { Split-Path $file -Leaf })
            [pscustomobject]@{ Assembly = $assembly; Name = $class.GetAttribute('name'); File = $relative; Counts = $counts }
        }
    }
}

# The type's namespace: the prefix before the first segment that is itself a class in the report, so nested
# types (Typstium.Capabilities.TypstCliProfileFactory.ProfileKey) land in their outer type's namespace.
function Get-Namespace([string] $ClassName, [System.Collections.Generic.HashSet[string]] $ClassNames) {
    $parts = ($ClassName -split '<')[0].Split('.')
    for ($i = 1; $i -le $parts.Count; $i++) {
        if ($ClassNames.Contains($parts[0..($i - 1)] -join '.')) {
            return $(if ($i -gt 1) { $parts[0..($i - 2)] -join '.' } else { $parts[0] })
        }
    }

    return $(if ($parts.Count -gt 1) { $parts[0..($parts.Count - 2)] -join '.' } else { $parts[0] })
}

# The public types of a product: bare type lines (no "->") of its PublicAPI.*.txt files, without generic arguments.
# Null when the product has no such files, so its classes show as "Unknown" rather than as a guess.
function Get-PublicTypes([string] $Assembly) {
    $files = @(Get-ChildItem -Path (Join-Path $SourceDirectory $Assembly) -Filter 'PublicAPI.*.txt' -ErrorAction SilentlyContinue)
    if ($files.Count -eq 0) {
        return $null
    }

    $types = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($line in $files | Get-Content) {
        $entry = $line.Trim()
        if ($entry.Length -gt 0 -and -not $entry.StartsWith('#') -and -not $entry.Contains('->')) {
            [void]$types.Add(($entry -split '<')[0])
        }
    }

    return , $types
}

# The top-level folder under the product folder; files directly in it form "(root)".
function Get-Area([string] $RelativeFile) {
    return $(if ($RelativeFile.Contains('/')) { $RelativeFile.Split('/')[0] } else { '(root)' })
}

# Biggest contributor to the gap first: most missed lines, then most missed branches, then name.
function Sort-ByGap($Items) {
    return $Items | Sort-Object `
        @{ Expression = { $_.Counts.Lines - $_.Counts.CoveredLines }; Descending = $true },
        @{ Expression = { $_.Counts.Branches - $_.Counts.CoveredBranches }; Descending = $true },
        @{ Expression = { $_.Name } }
}

# Rows per assembly (biggest gap first): its groups (biggest gap first), then a subtotal; finally the total.
function Get-GroupedRows($Classes, [scriptblock] $GroupOf) {
    $rows = [System.Collections.Generic.List[object]]::new()
    $total = New-Counts
    $assemblies = foreach ($assemblyClasses in $Classes | Group-Object Assembly) {
        $subtotal = New-Counts
        $assemblyClasses.Group | ForEach-Object { Add-Counts $subtotal $_.Counts }
        [pscustomobject]@{ Name = $assemblyClasses.Name; Counts = $subtotal; Classes = $assemblyClasses.Group }
    }

    foreach ($assembly in Sort-ByGap $assemblies) {
        $classNames = [System.Collections.Generic.HashSet[string]]::new([string[]]@($assembly.Classes | ForEach-Object { ($_.Name -split '<')[0] }))
        $groups = foreach ($group in $assembly.Classes | Group-Object { & $GroupOf $_ $classNames }) {
            $counts = New-Counts
            $group.Group | ForEach-Object { Add-Counts $counts $_.Counts }
            [pscustomobject]@{ Kind = 'Group'; Name = $group.Name; Counts = $counts }
        }

        foreach ($group in Sort-ByGap $groups) {
            $rows.Add($group)
        }

        Add-Counts $total $assembly.Counts
        $rows.Add([pscustomobject]@{ Kind = 'Assembly'; Name = "$($assembly.Name) (assembly)"; Counts = $assembly.Counts })
    }

    $rows.Add([pscustomobject]@{ Kind = 'Total'; Name = 'Total'; Counts = $total })
    return $rows
}

function Format-Rate([int] $Covered, [int] $Total) {
    if ($Total -eq 0) {
        return '-'
    }

    $percent = [string]::Format([cultureinfo]::InvariantCulture, '{0:0.0}%', 100.0 * $Covered / $Total)
    return '{0,6}  ({1}/{2})' -f $percent, $Covered, $Total
}

function Format-Gap([int] $Missed, [int] $TotalMissed) {
    if ($TotalMissed -eq 0) {
        return '-'
    }

    return [string]::Format([cultureinfo]::InvariantCulture, '{0:0.0}%', 100.0 * $Missed / $TotalMissed)
}

function Get-SummaryCells($Counts, [int] $TotalMissed) {
    $missed = Get-Missed $Counts
    return @([string]$missed, (Format-Gap $missed $TotalMissed),
        (Format-Rate $Counts.CoveredLines $Counts.Lines),
        (Format-Rate $Counts.CoveredBranches $Counts.Branches),
        (Format-Rate $Counts.CoveredMethods $Counts.Methods))
}

# A table: a title, headers (the first names the rows), and rows of Kind (Group, Assembly, Total, Item), Name and
# Cells. The first two cell columns are right-aligned numbers; the rest are left-aligned.
function New-Table([string] $Title, [string[]] $Headers, $Rows) {
    [pscustomobject]@{ Title = $Title; Headers = $Headers; Rows = @($Rows) }
}

function Write-Table($Table) {
    $names = @($Table.Rows | ForEach-Object { $(if ($_.Kind -eq 'Group') { '  ' + $_.Name } else { $_.Name }) })
    $nameWidth = [Math]::Max($Table.Headers[0].Length, ($names | Measure-Object -Property Length -Maximum).Maximum)
    $columns = $Table.Headers.Count - 1
    $widths = for ($c = 0; $c -lt $columns; $c++) {
        [Math]::Max($Table.Headers[$c + 1].Length, ($Table.Rows | ForEach-Object { $_.Cells[$c].Length } | Measure-Object -Maximum).Maximum)
    }

    $format = {
        param([string] $Name, [string[]] $Values)
        $parts = for ($c = 0; $c -lt $Values.Count; $c++) {
            $(if ($c -lt 2) { $Values[$c].PadLeft($widths[$c]) } else { $Values[$c].PadRight($widths[$c]) })
        }

        ($Name.PadRight($nameWidth) + '  ' + ($parts -join '  ')).TrimEnd()
    }

    $rule = ('-' * $nameWidth) + '  ' + (@($widths | ForEach-Object { '-' * $_ }) -join '  ')
    Write-Host ''
    Write-Host $Table.Title -ForegroundColor Cyan
    Write-Host (& $format $Table.Headers[0] $Table.Headers[1..$columns])
    Write-Host $rule
    for ($r = 0; $r -lt $Table.Rows.Count; $r++) {
        if ($Table.Rows[$r].Kind -eq 'Total') {
            Write-Host $rule
        }

        Write-Host (& $format $names[$r] $Table.Rows[$r].Cells) -ForegroundColor $(if ($Table.Rows[$r].Kind -in 'Group', 'Item') { 'Gray' } else { 'White' })
    }
}

function Get-MarkdownTable($Table) {
    $markdown = [System.Text.StringBuilder]::new()
    [void]$markdown.AppendLine("| $($Table.Headers -join ' | ') |")
    $alignments = $Table.Headers[1..($Table.Headers.Count - 1)] | ForEach-Object { $(if ($_ -eq 'File') { ' --- |' } else { ' ---: |' }) }
    [void]$markdown.AppendLine('| --- |' + ($alignments -join ''))
    foreach ($row in $Table.Rows) {
        $name = $(if ($row.Kind -eq 'Group') { "&nbsp;&nbsp;$($row.Name)" } elseif ($row.Kind -eq 'Item') { "``$($row.Name)``" } else { "**$($row.Name)**" })
        $values = $row.Cells | ForEach-Object { ($_ -replace '\s+', ' ').Trim() }
        if ($row.Kind -in 'Assembly', 'Total') {
            $values = $values | ForEach-Object { "**$_**" }
        }

        [void]$markdown.AppendLine("| $name | $($values -join ' | ') |")
    }

    return $markdown.ToString()
}

$classes = @(Read-CoverageClasses $CoberturaPath)
$total = New-Counts
$classes | ForEach-Object { Add-Counts $total $_.Counts }
$totalMissed = Get-Missed $total
$summaryHeaders = @('Missed', 'Gap', 'Line', 'Branch', 'Method (called)')

$publicTypes = @{}
foreach ($assembly in $classes.Assembly | Sort-Object -Unique) {
    $publicTypes[$assembly] = Get-PublicTypes $assembly
}

$byVisibility = Get-GroupedRows $classes {
    param($class, $classNames)
    $types = $publicTypes[$class.Assembly]
    $(if ($null -eq $types) { 'Unknown (no PublicAPI files)' } elseif ($types.Contains(($class.Name -split '<')[0])) { 'Public API' } else { 'Internal' })
}
$byNamespace = Get-GroupedRows $classes { param($class, $classNames) Get-Namespace $class.Name $classNames }
$byArea = Get-GroupedRows $classes { param($class, $classNames) Get-Area $class.File }
$worstClasses = Sort-ByGap ($classes | Where-Object { (Get-Missed $_.Counts) -gt 0 }) | Select-Object -First $TopClasses

$tables = @(
    (New-Table 'Coverage by visibility' (@('Visibility') + $summaryHeaders) ($byVisibility | ForEach-Object {
        [pscustomobject]@{ Kind = $_.Kind; Name = $_.Name; Cells = Get-SummaryCells $_.Counts $totalMissed } })),
    (New-Table 'Coverage by namespace' (@('Namespace') + $summaryHeaders) ($byNamespace | ForEach-Object {
        [pscustomobject]@{ Kind = $_.Kind; Name = $_.Name; Cells = Get-SummaryCells $_.Counts $totalMissed } })),
    (New-Table 'Coverage by area (top-level folder)' (@('Area') + $summaryHeaders) ($byArea | ForEach-Object {
        [pscustomobject]@{ Kind = $_.Kind; Name = $_.Name; Cells = Get-SummaryCells $_.Counts $totalMissed } })),
    (New-Table "Top $TopClasses classes by missed lines" @('Class', 'Missed', 'Gap', 'Line', 'File') ($worstClasses | ForEach-Object {
        $missed = Get-Missed $_.Counts
        [pscustomobject]@{ Kind = 'Item'; Name = $_.Name; Cells = @([string]$missed, (Format-Gap $missed $totalMissed), (Format-Rate $_.Counts.CoveredLines $_.Counts.Lines), $_.File) } })))

if ($SummaryPath) {
    $toJson = { param($counts) [ordered]@{ coveredLines = $counts.CoveredLines; lines = $counts.Lines; coveredBranches = $counts.CoveredBranches; branches = $counts.Branches; coveredMethods = $counts.CoveredMethods; methods = $counts.Methods } }
    $summary = [ordered]@{
        totals = & $toJson $total
        namespaces = @($byNamespace | Where-Object Kind -eq 'Group' | ForEach-Object { $entry = & $toJson $_.Counts; $entry.Insert(0, 'name', $_.Name); $entry })
    }
    New-Item -ItemType Directory -Force (Split-Path $SummaryPath) | Out-Null
    $summary | ConvertTo-Json -Depth 4 | Set-Content -Path $SummaryPath -Encoding utf8
}

foreach ($table in $tables) {
    Write-Table $table
}

$legend = 'Sorted by missed lines. Gap: share of all missed lines. Method (called): methods entered at least once.'
Write-Host ''
Write-Host $legend
Write-Host ''

if ($env:GITHUB_STEP_SUMMARY -and -not $NoJobSummary) {
    $markdown = [System.Text.StringBuilder]::new()
    [void]$markdown.AppendLine('### Code coverage (unit and integration tests)')
    [void]$markdown.AppendLine()
    [void]$markdown.AppendLine((Get-MarkdownTable $tables[0]))
    foreach ($table in $tables[1..($tables.Count - 1)]) {
        [void]$markdown.AppendLine("<details><summary>$($table.Title)</summary>")
        [void]$markdown.AppendLine()
        [void]$markdown.AppendLine((Get-MarkdownTable $table))
        [void]$markdown.AppendLine('</details>')
        [void]$markdown.AppendLine()
    }

    [void]$markdown.AppendLine($legend)
    Add-Content -Path $env:GITHUB_STEP_SUMMARY -Value $markdown.ToString() -Encoding utf8
}
