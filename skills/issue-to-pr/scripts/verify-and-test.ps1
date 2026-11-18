<#
.SYNOPSIS
    Verification and test helper for the /issue-to-pr workflow (Stages 4 and 5).
.DESCRIPTION
    1. Checks and (unless -CheckOnly is set) enforces UTF-8 with BOM on all added or
       modified C# (.cs) files to satisfy .editorconfig on Windows.
    2. Runs `git diff --check` across working tree, staged changes, and BaseRef...HEAD.
    3. Runs `dotnet test UAI.ZeroVP.sln --configuration Release` and extracts structured
       compiler diagnostics (CSxxxx), warnings, and xUnit test failure details.
#>
[CmdletBinding()]
param(
    [string]$BaseRef = "main",
    [switch]$CheckOnly,
    [string]$Filter = "",
    [switch]$NoBuild,
    [switch]$SkipDiffCheck,
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

$repoRoot = (& git rev-parse --show-toplevel 2>$null)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($repoRoot)) {
    Write-Error "Not inside a Git repository."
    exit 1
}
$repoRoot = $repoRoot.Trim()

function Get-ChangedCsFiles {
    param([string]$Base)

    $candidates = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

    $baseExists = $false
    & git rev-parse --verify --quiet $Base *> $null
    if ($LASTEXITCODE -eq 0) {
        $baseExists = $true
    }

    $commands = @(
        @("diff", "--name-only", "--diff-filter=AM", "--", "*.cs"),
        @("diff", "--cached", "--name-only", "--diff-filter=AM", "--", "*.cs"),
        @("ls-files", "--others", "--exclude-standard", "--", "*.cs")
    )
    if ($baseExists) {
        $commands += ,@("diff", "--name-only", "--diff-filter=AM", "$Base...HEAD", "--", "*.cs")
    }

    foreach ($args in $commands) {
        $lines = & git @args 2>$null
        if ($LASTEXITCODE -eq 0 -and $lines) {
            foreach ($line in $lines) {
                $trimmed = $line.Trim()
                if (-not [string]::IsNullOrWhiteSpace($trimmed)) {
                    [void]$candidates.Add($trimmed)
                }
            }
        }
    }

    return @($candidates | Sort-Object)
}

Write-Host "=== 1. UTF-8 BOM check (.cs) ==="
$csFiles = Get-ChangedCsFiles -Base $BaseRef
$missingBom = [System.Collections.Generic.List[string]]::new()
$fixedBom = [System.Collections.Generic.List[string]]::new()

foreach ($relPath in $csFiles) {
    $fullPath = Join-Path $repoRoot $relPath
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        continue
    }

    $bytes = [System.IO.File]::ReadAllBytes($fullPath)
    $hasBom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)

    if (-not $hasBom) {
        if ($CheckOnly) {
            $missingBom.Add($relPath)
            Write-Host "  [MISSING BOM] $relPath"
        }
        else {
            $newBytes = New-Object byte[] ($bytes.Length + 3)
            $newBytes[0] = 0xEF
            $newBytes[1] = 0xBB
            $newBytes[2] = 0xBF
            if ($bytes.Length -gt 0) {
                [System.Buffer]::BlockCopy($bytes, 0, $newBytes, 3, $bytes.Length)
            }
            [System.IO.File]::WriteAllBytes($fullPath, $newBytes)
            $fixedBom.Add($relPath)
            Write-Host "  [BOM FIXED]   $relPath"
        }
    }
}

if ($csFiles.Count -eq 0) {
    Write-Host "  No modified or untracked .cs files relative to '$BaseRef'."
}
elseif ($missingBom.Count -eq 0 -and $fixedBom.Count -eq 0) {
    Write-Host "  All $($csFiles.Count) changed .cs file(s) already have UTF-8 BOM."
}

$bomFailed = ($missingBom.Count -gt 0)

$diffFailed = $false
if (-not $SkipDiffCheck) {
    Write-Host "`n=== 2. Git whitespace / conflict check ==="
    $diffTargets = @(
        @("diff", "--check"),
        @("diff", "--cached", "--check")
    )
    & git rev-parse --verify --quiet $BaseRef *> $null
    if ($LASTEXITCODE -eq 0) {
        $diffTargets += ,@("diff", "--check", "$BaseRef...HEAD")
    }

    foreach ($args in $diffTargets) {
        $diffOut = & git @args 2>&1
        if ($LASTEXITCODE -ne 0 -or ($diffOut -and ($diffOut | Out-String).Trim().Length -gt 0)) {
            $diffFailed = $true
            $diffOut | ForEach-Object { Write-Host "  $_" }
        }
    }

    if (-not $diffFailed) {
        Write-Host "  git diff --check: clean"
    }
}

$testFailed = $false
$errors = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
$warnings = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
$failedTests = [System.Collections.Generic.List[string]]::new()
$passedCount = 0
$failedCount = 0
$skippedCount = 0
$totalCount = 0

if (-not $SkipTests) {
    Write-Host "`n=== 3. dotnet test UAI.ZeroVP.sln --configuration Release ==="
    $dotnetArgs = @("test", "UAI.ZeroVP.sln", "--configuration", "Release")
    if ($NoBuild) {
        $dotnetArgs += "--no-build"
    }
    if (-not [string]::IsNullOrWhiteSpace($Filter)) {
        $dotnetArgs += @("--filter", $Filter)
    }

    $previousErrorAction = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    $rawOutput = & dotnet @dotnetArgs 2>&1
    $dotnetExit = $LASTEXITCODE
    $ErrorActionPreference = $previousErrorAction

    foreach ($entry in $rawOutput) {
        $line = $entry.ToString()
        Write-Host $line

        if ($line -match '\berror\s+[A-Z]{2}\d{4}\b') {
            [void]$errors.Add($line.Trim())
        }
        elseif ($line -match '\bwarning\s+[A-Z]{2}\d{4}\b') {
            [void]$warnings.Add($line.Trim())
        }

        if ($line -match '^\s*(?:Failed|Ne\u00fasp\u011b\u0161n\u00e9)\s+([^\[\s].+?)(?:\s+\[\d+.*)?$') {
            $testName = $Matches[1].Trim()
            if (-not $failedTests.Contains($testName)) {
                $failedTests.Add($testName)
            }
        }

        # Match English and Czech dotnet test summary lines (ASCII escapes work regardless of PowerShell codepage)
        if ($line -match '(?:Failed|Ne\u00fasp\u011b\u0161n\u00e9):\s*(\d+),\s*(?:Passed|\u00dasp\u011b\u0161n\u00e9):\s*(\d+),\s*(?:Skipped|P\u0159esko\u010deno):\s*(\d+),\s*(?:Total|Celkem):\s*(\d+)') {
            $failedCount += [int]$Matches[1]
            $passedCount += [int]$Matches[2]
            $skippedCount += [int]$Matches[3]
            $totalCount += [int]$Matches[4]
        }
    }

    if ($dotnetExit -ne 0 -or $errors.Count -gt 0 -or $failedCount -gt 0) {
        $testFailed = $true
    }
}

Write-Host "`n=== verify-and-test summary ==="
Write-Host ("BOM check:      {0} (checked: {1}, fixed: {2}, missing: {3})" -f $(if ($bomFailed) { "FAIL" } else { "OK" }), $csFiles.Count, $fixedBom.Count, $missingBom.Count)
if (-not $SkipDiffCheck) {
    Write-Host ("Diff check:     {0}" -f $(if ($diffFailed) { "FAIL" } else { "OK" }))
}
if (-not $SkipTests) {
    Write-Host ("Build errors:   {0}" -f $errors.Count)
    Write-Host ("Warnings:       {0}" -f $warnings.Count)
    Write-Host ("Tests:          {0}/{1} passed (failed: {2}, skipped: {3})" -f $passedCount, $totalCount, $failedCount, $skippedCount)

    if ($errors.Count -gt 0) {
        Write-Host "`n--- Compiler errors (deduplicated) ---"
        foreach ($err in $errors) { Write-Host "  $err" }
    }
    if ($warnings.Count -gt 0) {
        Write-Host "`n--- Compiler warnings (deduplicated) ---"
        foreach ($warn in $warnings) { Write-Host "  $warn" }
    }
    if ($failedTests.Count -gt 0) {
        Write-Host "`n--- Failing tests ---"
        foreach ($t in $failedTests) { Write-Host "  $t" }
    }
}

$overallFailed = ($bomFailed -or $diffFailed -or $testFailed)
Write-Host ("Status:         {0}" -f $(if ($overallFailed) { "FAIL" } else { "PASS" }))

if ($overallFailed) {
    exit 1
}
exit 0
