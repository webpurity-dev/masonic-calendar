# Renders PDFs for all units from the units CSV file.
# Iterates through each unit and renders using its own unit type from the CSV.
#
# Usage:
#   .\render-units.ps1 -Version 1.6
#   .\render-units.ps1 -Version 1.6 -Limit 10

param(
    [Parameter(Mandatory)][string]$Version,
    [int]$Limit = 0,
    [string]$FilterUnitType = ""
)

# Optional: FilterUnitType filters to specific unit type (craft, royalarch, mark, ram, etc.)

$rootDir       = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$dataDir       = Join-Path $rootDir "document\data"
$outputDir     = Join-Path $rootDir "output"
$consoleProject = Join-Path $rootDir "src\MasonicCalendar.Console\MasonicCalendar.Console.csproj"

if (-not (Test-Path $consoleProject)) {
    Write-Host "ERROR: Console project not found: $consoleProject" -ForegroundColor Red; exit 1
}

# ============================================================
# Load units from CSV
# ============================================================
Write-Host "Loading units from CSV (version $Version)..." -ForegroundColor Cyan

# Determine which CSV to use based on version
$csvPath = Join-Path $dataDir "units_v$Version.csv"
if (-not (Test-Path $csvPath)) {
    Write-Host "ERROR: Units CSV not found: $csvPath" -ForegroundColor Red; exit 1
}

# Load all units with their unit type
$units = @(Import-Csv $csvPath |
    Select-Object @{N='Type';   E={$_.'Unit Type'.Trim() -replace ' ', ''}},
                  @{N='Number'; E={$_.'Unit No'.Trim()}},
                  @{N='Name';   E={$_.'Unit Name'.Trim()}})

# Filter by unit type if specified
if (-not [string]::IsNullOrWhiteSpace($FilterUnitType)) {
    $units = $units | Where-Object { $_.Type -eq $FilterUnitType }
}

if ($Limit -gt 0) { $units = $units | Select-Object -First $Limit }

$limitNote = if ($Limit -gt 0) { " (limited to $Limit)" } else { "" }
$filterNote = if (-not [string]::IsNullOrWhiteSpace($FilterUnitType)) { " [$FilterUnitType only]" } else { "" }
Write-Host "Rendering $($units.Count) unit(s) as PDF$filterNote$limitNote" -ForegroundColor Cyan

Write-Host ""

# ============================================================
# Render each unit
# ============================================================
$startTime    = Get-Date
$successCount = 0
$failCount    = 0

foreach ($unit in $units) {
    $idx = $successCount + $failCount + 1
    
    # Get unit type from the CSV row
    $unitType = $unit.Type
    
    Write-Host "[$idx/$($units.Count)]  [$unitType] $($unit.Number)  $($unit.Name)..." -NoNewline
    
    # Show the command being executed
    Write-Host ""
    Write-Host "  Command: dotnet run ... -unit $($unit.Number) -unittype $unitType -output pdf" -ForegroundColor DarkGray
    Write-Host "  " -NoNewline

    & dotnet run --project $consoleProject -- `
        -template master_v1 `
        -unit $unit.Number `
        -unittype $unitType `
        -output pdf `
        2>&1 | Out-Null

    if ($LASTEXITCODE -eq 0) {
        Write-Host "OK" -ForegroundColor Green
        $successCount++
    } else {
        Write-Host "FAILED" -ForegroundColor Red
        $failCount++
    }
}

$duration = [Math]::Round(((Get-Date) - $startTime).TotalMinutes, 1)

Write-Host ""
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "  Total:      $($units.Count)"
Write-Host "  Successful: $successCount" -ForegroundColor $(if ($successCount -eq $units.Count) { 'Green' } else { 'Yellow' })
Write-Host "  Failed:     $failCount"    -ForegroundColor $(if ($failCount -gt 0) { 'Red' } else { 'Green' })
Write-Host "  Duration:   $duration min"
Write-Host ""

if ($failCount -eq 0) {
    Write-Host "All PDFs generated successfully!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "Some PDFs failed to generate." -ForegroundColor Red
    exit 1
}

Write-Host ""
Read-Host "Press Enter to exit"
