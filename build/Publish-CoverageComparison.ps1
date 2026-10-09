#!/usr/bin/env pwsh
# CI only, on pull requests: compares this run's coverage summary with the one from the pull request's merge base
# on the base branch, then posts (or updates) one sticky comment and creates an advisory "Coverage" check run.
# Baseline: the successful CI run of the merge-base commit; if that has no summary (never built, or the artifact
# expired), the latest successful push run of the base branch, and the comment says so.
# Needs: gh (GH_TOKEN with pull-requests: write, checks: write, actions: read), git history (fetch-depth: 0), and
# the environment below.
[CmdletBinding()]
param(
    [string] $Summary = 'artifacts/coverage/summary.json',
    [double] $MaxLineDecrease = 0.5,
    [string] $Workflow = 'ci.yml'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repository = $env:GITHUB_REPOSITORY
$pullRequest = $env:PR_NUMBER
$headSha = $env:HEAD_SHA
$baseRef = $env:BASE_REF
$work = Join-Path ([System.IO.Path]::GetTempPath()) 'typstium-coverage-comparison'
Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
New-Item -ItemType Directory $work | Out-Null

# A pull request from a fork gets a read-only token: report in the log only.
if ($env:HEAD_REPOSITORY -and $env:HEAD_REPOSITORY -ne $repository) {
    Write-Host "::warning::Pull request from a fork ($env:HEAD_REPOSITORY): the token can't comment or create checks, so the coverage comparison is skipped."
    exit 0
}

# Downloads a run's coverage summary; returns its path, or null when the run has none (or it expired).
function Get-RunSummary([string] $RunId) {
    $target = Join-Path $work $RunId
    gh run download $RunId --repo $repository --name coverage-summary --dir $target 2>$null | Out-Null
    $path = Join-Path $target 'summary.json'
    return $(if ($LASTEXITCODE -eq 0 -and (Test-Path $path)) { $path } else { $null })
}

$baseline = $null
$label = "``$baseRef``"
$mergeBase = (git merge-base "origin/$baseRef" $headSha).Trim()
$runs = gh run list --repo $repository --workflow $Workflow --commit $mergeBase --status success --limit 5 --json databaseId | ConvertFrom-Json
foreach ($run in $runs) {
    if ($path = Get-RunSummary $run.databaseId) {
        $baseline = $path
        $label = "``$baseRef`` at ``$($mergeBase.Substring(0, 7))`` (merge base)"
        break
    }
}

if (-not $baseline) {
    $latest = gh run list --repo $repository --workflow $Workflow --branch $baseRef --event push --status success --limit 10 --json databaseId,headSha | ConvertFrom-Json
    foreach ($run in $latest) {
        if ($path = Get-RunSummary $run.databaseId) {
            $baseline = $path
            $label = "``$baseRef`` at ``$($run.headSha.Substring(0, 7))`` (latest; the merge base ``$($mergeBase.Substring(0, 7))`` has no coverage)"
            break
        }
    }
}

$markdown = Join-Path $work 'comment.md'
$result = Join-Path $work 'result.json'
$compareArguments = @{ Current = $Summary; BaselineLabel = $label; MaxLineDecrease = $MaxLineDecrease; MarkdownPath = $markdown; ResultPath = $result }
if ($baseline) {
    $compareArguments.Baseline = $baseline
}

& (Join-Path $PSScriptRoot 'Compare-Coverage.ps1') @compareArguments
$verdict = Get-Content $result -Raw | ConvertFrom-Json

# One sticky comment, found by the marker Compare-Coverage.ps1 puts first.
$existing = gh api "repos/$repository/issues/$pullRequest/comments" --paginate --jq '.[] | select(.body | startswith("<!-- typstium-coverage -->")) | .id' | Select-Object -First 1
if ($existing) {
    gh api --method PATCH "repos/$repository/issues/comments/$existing" -F "body=@$markdown" | Out-Null
}
else {
    gh api --method POST "repos/$repository/issues/$pullRequest/comments" -F "body=@$markdown" | Out-Null
}

if ($LASTEXITCODE -ne 0) {
    throw 'Posting the coverage comment failed.'
}

# Advisory: nothing requires this check yet, so a failure is visible but doesn't block merging.
gh api --method POST "repos/$repository/check-runs" `
    -f name=Coverage -f "head_sha=$headSha" -f status=completed -f "conclusion=$($verdict.conclusion)" `
    -f "output[title]=$($verdict.title)" -F "output[summary]=@$markdown" | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw 'Creating the Coverage check run failed.'
}

if ($env:GITHUB_STEP_SUMMARY) {
    Get-Content $markdown | Add-Content -Path $env:GITHUB_STEP_SUMMARY -Encoding utf8
}

Write-Host "Coverage comparison posted: $($verdict.conclusion), $($verdict.title)"
