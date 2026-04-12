# Core render script: reads unit list from a data source YAML and renders each unit as PDF.
# Called by the type-specific wrapper scripts (render-all-craft-units.ps1, etc.)
#
# Usage (direct):
#   .\render-units.ps1 -DataSourceYaml craft_data_source.yaml
#   .\render-units.ps1 -DataSourceYaml royalarch_data_source.yaml -Limit 3

param(
    [Parameter(Mandatory)][string]$DataSourceYaml,
    [int]$Limit = 0
)

$rootDir       = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$dataSourceDir = Join-Path $rootDir "document\data_sources"
$dataDir       = Join-Path $rootDir "document\data"
$consoleProject = Join-Path $rootDir "src\MasonicCalendar.Console\MasonicCalendar.Console.csproj"

$yamlPath = Join-Path $dataSourceDir $DataSourceYaml
if (-not (Test-Path $yamlPath)) {
    Write-Host "ERROR: YAML not found: $yamlPath" -ForegroundColor Red; exit 1
}
if (-not (Test-Path $consoleProject)) {
    Write-Host "ERROR: Console project not found: $consoleProject" -ForegroundColor Red; exit 1
}

# ============================================================
# Parse the "units:" section of the data source YAML
# ============================================================
$lines         = Get-Content $yamlPath
$inUnits       = $false
$lastFieldName = $null
$source        = $null
$filterField   = $null
$filterValue   = $null
$unitNoCol     = "Unit No"
$unitNameCol   = "Unit Name"

foreach ($line in $lines) {
    if ($line -match '^\s*#') { continue }
    if ($line -match '^units:\s*$') { $inUnits = $true; continue }
    # Stop when we hit the next top-level key
    if ($inUnits -and $line -match '^[a-zA-Z][a-zA-Z_]*:\s*$') { break }
    if (-not $inUnits) { continue }

    if    ($line -match '^\s+source:\s*"([^"]+)"')        { $source      = $Matches[1] }
    elseif ($line -match '^\s+filter_field:\s*"([^"]+)"') { $filterField = $Matches[1] }
    elseif ($line -match '^\s+filter_value:\s*"([^"]+)"') { $filterValue = $Matches[1] }
    elseif ($line -match '^\s+-\s*name:\s*"([^"]+)"')     { $lastFieldName = $Matches[1] }
    elseif ($line -match '^\s+csv_column:\s*"([^"]+)"' -and $lastFieldName) {
        if    ($lastFieldName -eq 'Number') { $unitNoCol   = $Matches[1] }
        elseif ($lastFieldName -eq 'Name')  { $unitNameCol = $Matches[1] }
        $lastFieldName = $null
    }
}

if (-not $source -or -not $filterField -or -not $filterValue) {
    Write-Host "ERROR: Could not parse units section from $DataSourceYaml" -ForegroundColor Red; exit 1
}

# Derive section ID: "craft_data_source.yaml" -> "craft_units"
$sectionId = ([System.IO.Path]::GetFileNameWithoutExtension($DataSourceYaml) -replace '_data_source$','') + '_units'

# ============================================================
# Load and filter units from CSV
# ============================================================
$csvPath = Join-Path $dataDir $source
if (-not (Test-Path $csvPath)) {
    Write-Host "ERROR: Units CSV not found: $csvPath" -ForegroundColor Red; exit 1
}

$units = @(Import-Csv $csvPath |
    Where-Object { $_.$filterField.Trim() -eq $filterValue } |
    Select-Object @{N='Number'; E={$_.$unitNoCol.Trim()}},
                  @{N='Name';   E={$_.$unitNameCol.Trim()}})

if ($Limit -gt 0) { $units = $units | Select-Object -First $Limit }

$limitNote = if ($Limit -gt 0) { " (limited to $Limit)" } else { "" }
Write-Host "Rendering $($units.Count) $filterValue unit(s) as PDF$limitNote  [section: $sectionId]" -ForegroundColor Cyan
Write-Host ""

# ============================================================
# Render each unit
# ============================================================
$startTime    = Get-Date
$successCount = 0
$failCount    = 0

foreach ($unit in $units) {
    $idx = $successCount + $failCount + 1
    Write-Host "[$idx/$($units.Count)]  $($unit.Number)  $($unit.Name)..." -NoNewline

    & dotnet run --project $consoleProject -- `
        -template master_v1 `
        -section $sectionId `
        -unit $unit.Number `
        -output pdf `
        2>&1 | Out-Null

    if ($LASTEXITCODE -eq 0) {
        Write-Host "  OK" -ForegroundColor Green
        $successCount++
    } else {
        Write-Host "  FAILED" -ForegroundColor Red
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
