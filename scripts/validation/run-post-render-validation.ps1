#Requires -Version 5.1
<#
.SYNOPSIS
    Runs all post-render validation scripts in sequence.

.DESCRIPTION
    Runs ordering, meeting-date, and rendered-output validation in that order.
    All validators are attempted, and this script exits with code 1 if any fail.
#>

$validators = @(
    "validate-ordering.ps1",
    "validate-meeting-dates.ps1",
    "validate-output.ps1"
)

$results = [System.Collections.Generic.List[PSCustomObject]]::new()

foreach ($validator in $validators) {
    $scriptPath = Join-Path $PSScriptRoot $validator

    Write-Host ""
    Write-Host ("=" * 70) -ForegroundColor Cyan
    Write-Host "Running $validator" -ForegroundColor Cyan
    Write-Host ("=" * 70) -ForegroundColor Cyan

    if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) {
        Write-Host "ERROR: Validation script not found: $scriptPath" -ForegroundColor Red
        [void]$results.Add([PSCustomObject]@{ Name = $validator; ExitCode = 1 })
        continue
    }

    $LASTEXITCODE = 0
    try {
        & $scriptPath
        $exitCode = $LASTEXITCODE
    } catch {
        Write-Host "ERROR: $validator failed: $($_.Exception.Message)" -ForegroundColor Red
        $exitCode = 1
    }

    [void]$results.Add([PSCustomObject]@{ Name = $validator; ExitCode = $exitCode })
}

Write-Host ""
Write-Host ("=" * 70) -ForegroundColor Cyan
Write-Host "Post-render validation summary" -ForegroundColor Cyan
Write-Host ("=" * 70) -ForegroundColor Cyan

foreach ($result in $results) {
    $status = if ($result.ExitCode -eq 0) { "PASS" } else { "FAIL" }
    $color = if ($result.ExitCode -eq 0) { "Green" } else { "Red" }
    Write-Host ("{0,-36} {1}" -f $result.Name, $status) -ForegroundColor $color
}

$failedValidators = @($results | Where-Object { $_.ExitCode -ne 0 })
if ($failedValidators.Count -gt 0) {
    Write-Host ""
    Write-Host "$($failedValidators.Count) validation script(s) failed." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "All post-render validations passed." -ForegroundColor Green
exit 0