#!/usr/bin/env pwsh
# Compares two coverage summaries (artifacts/coverage/summary.json, written by Write-CoverageReport.ps1) and writes
# the pull-request comment as Markdown plus a result (check conclusion and title) as JSON.
# The rule: total line coverage may drop at most -MaxLineDecrease percentage points (pp) from the baseline. Branch
# and method coverage are shown but not gated: they move more between runs of the same code.
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Current,
    # Missing or empty when there is no baseline yet; the result is then neutral.
    [string] $Baseline,
    # What the baseline is, for the comment, e.g. "main at abc1234 (merge base)".
    [string] $BaselineLabel = 'the baseline',
    [double] $MaxLineDecrease = 0.5,
    [Parameter(Mandatory)] [string] $MarkdownPath,
    [Parameter(Mandatory)] [string] $ResultPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$Marker = '<!-- typstium-coverage -->'
$Invariant = [cultureinfo]::InvariantCulture

function Get-Percent([int] $Covered, [int] $Total) {
    return $(if ($Total -eq 0) { 100.0 } else { 100.0 * $Covered / $Total })
}

function Format-Percent([double] $Value) {
    return [string]::Format($Invariant, '{0:0.0}%', $Value)
}

function Format-Rate([int] $Covered, [int] $Total) {
    return '{0} ({1}/{2})' -f (Format-Percent (Get-Percent $Covered $Total)), $Covered, $Total
}

# Percentage-point change with a sign and a colour: green up, red down, white within rounding.
function Format-Change([double] $Points) {
    $rounded = [Math]::Round($Points, 1, [MidpointRounding]::AwayFromZero)
    if ($rounded -eq 0) {
        return '⚪ ±0.0 pp'
    }

    $text = [string]::Format($Invariant, '{0:0.0} pp', [Math]::Abs($rounded))
    return $(if ($rounded -gt 0) { "🟢 +$text" } else { "🔴 −$text" })
}

# A change in a count where fewer is better (missed lines).
function Format-CountChange([int] $Change) {
    return $(if ($Change -eq 0) { '⚪ ±0' } elseif ($Change -lt 0) { "🟢 −$(-$Change)" } else { "🔴 +$Change" })
}

$now = Get-Content $Current -Raw | ConvertFrom-Json
$base = $(if ($Baseline -and (Test-Path $Baseline)) { Get-Content $Baseline -Raw | ConvertFrom-Json } else { $null })

$t = $now.totals
$linePercent = Get-Percent $t.coveredLines $t.lines
$markdown = [System.Text.StringBuilder]::new()
[void]$markdown.AppendLine($Marker)

if ($null -eq $base) {
    $conclusion = 'neutral'
    $title = "$(Format-Percent $linePercent) lines (no baseline)"
    [void]$markdown.AppendLine("### Coverage: $(Format-Percent $linePercent) lines")
    [void]$markdown.AppendLine()
    [void]$markdown.AppendLine("No baseline coverage for $BaselineLabel yet, so there's nothing to compare with; the rule isn't applied.")
    [void]$markdown.AppendLine()
    [void]$markdown.AppendLine('| | This PR |')
    [void]$markdown.AppendLine('| --- | ---: |')
    [void]$markdown.AppendLine("| Line | $(Format-Rate $t.coveredLines $t.lines) |")
    [void]$markdown.AppendLine("| Branch | $(Format-Rate $t.coveredBranches $t.branches) |")
    [void]$markdown.AppendLine("| Method (called) | $(Format-Rate $t.coveredMethods $t.methods) |")
    [void]$markdown.AppendLine("| Missed lines | $($t.lines - $t.coveredLines) |")
}
else {
    $b = $base.totals
    $lineChange = $linePercent - (Get-Percent $b.coveredLines $b.lines)
    $passed = $lineChange -ge -$MaxLineDecrease
    $conclusion = $(if ($passed) { 'success' } else { 'failure' })
    $change = Format-Change $lineChange
    $title = "$(Format-Percent $linePercent) lines ($($change.Substring($change.IndexOf(' ') + 1)))"
    $rule = [string]::Format($Invariant, 'Rule: line coverage may drop at most {0:0.0#} pp', $MaxLineDecrease)
    $verdict = $(if ($passed) { '✅ **passed**' } else { '❌ **failed**' })

    [void]$markdown.AppendLine("### Coverage: $(Format-Percent $linePercent) lines $change")
    [void]$markdown.AppendLine()
    [void]$markdown.AppendLine("Compared with $BaselineLabel. ${rule}: $verdict")
    [void]$markdown.AppendLine()
    [void]$markdown.AppendLine('| | This PR | Baseline | Change |')
    [void]$markdown.AppendLine('| --- | ---: | ---: | ---: |')
    foreach ($metric in @(
            @{ Name = 'Line'; Covered = 'coveredLines'; Total = 'lines' },
            @{ Name = 'Branch'; Covered = 'coveredBranches'; Total = 'branches' },
            @{ Name = 'Method (called)'; Covered = 'coveredMethods'; Total = 'methods' })) {
        $nowRate = Get-Percent $t.($metric.Covered) $t.($metric.Total)
        $baseRate = Get-Percent $b.($metric.Covered) $b.($metric.Total)
        [void]$markdown.AppendLine("| $($metric.Name) | $(Format-Rate $t.($metric.Covered) $t.($metric.Total)) | $(Format-Rate $b.($metric.Covered) $b.($metric.Total)) | $(Format-Change ($nowRate - $baseRate)) |")
    }

    $missedNow = $t.lines - $t.coveredLines
    $missedBase = $b.lines - $b.coveredLines
    [void]$markdown.AppendLine("| Missed lines | $missedNow | $missedBase | $(Format-CountChange ($missedNow - $missedBase)) |")

    # Namespaces whose line counts changed, biggest growth in missed lines first.
    $baseByName = @{}
    foreach ($namespace in $base.namespaces) {
        $baseByName[$namespace.name] = $namespace
    }

    $nowByName = @{}
    foreach ($namespace in $now.namespaces) {
        $nowByName[$namespace.name] = $namespace
    }

    $changed = foreach ($name in @($nowByName.Keys) + @($baseByName.Keys) | Sort-Object -Unique) {
        $n = $nowByName[$name]
        $o = $baseByName[$name]
        $nowCovered = $(if ($n) { $n.coveredLines } else { 0 }); $nowLines = $(if ($n) { $n.lines } else { 0 })
        $oldCovered = $(if ($o) { $o.coveredLines } else { 0 }); $oldLines = $(if ($o) { $o.lines } else { 0 })
        if ($nowCovered -ne $oldCovered -or $nowLines -ne $oldLines) {
            [pscustomobject]@{
                Name = $name
                Now = $(if ($n) { Format-Rate $nowCovered $nowLines } else { '(removed)' })
                Base = $(if ($o) { Format-Rate $oldCovered $oldLines } else { '(new)' })
                Change = $(if ($n -and $o) { Format-Change ((Get-Percent $nowCovered $nowLines) - (Get-Percent $oldCovered $oldLines)) } else { '' })
                Missed = ($nowLines - $nowCovered) - ($oldLines - $oldCovered)
            }
        }
    }

    if ($changed) {
        [void]$markdown.AppendLine()
        [void]$markdown.AppendLine("<details><summary>Namespaces with changed line coverage ($(@($changed).Count))</summary>")
        [void]$markdown.AppendLine()
        [void]$markdown.AppendLine('| Namespace | This PR | Baseline | Change | Missed lines |')
        [void]$markdown.AppendLine('| --- | ---: | ---: | ---: | ---: |')
        foreach ($row in $changed | Sort-Object @{ Expression = 'Missed'; Descending = $true }, Name) {
            [void]$markdown.AppendLine("| $($row.Name) | $($row.Now) | $($row.Base) | $($row.Change) | $(Format-CountChange $row.Missed) |")
        }

        [void]$markdown.AppendLine()
        [void]$markdown.AppendLine('</details>')
    }
}

[void]$markdown.AppendLine()
[void]$markdown.AppendLine('<sub>Unit and integration tests, Windows and Ubuntu merged. The full tables are in the CI run''s coverage job summary.</sub>')

Set-Content -Path $MarkdownPath -Value $markdown.ToString() -Encoding utf8
[ordered]@{ conclusion = $conclusion; title = $title } | ConvertTo-Json | Set-Content -Path $ResultPath -Encoding utf8
Write-Host "Coverage check: $conclusion, $title"
