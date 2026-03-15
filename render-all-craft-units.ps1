# PowerShell script to generate a PDF for each Craft Masonry Unit
# Usage: powershell -ExecutionPolicy Bypass -File .\render-all-craft-units.ps1
# Usage (first 2 units): powershell -ExecutionPolicy Bypass -File .\render-all-craft-units.ps1 -limit 2

param(
    [int]$limit = 0  # 0 means no limit, render all
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$dataFile = Join-Path $scriptDir "document\data\craft-units.csv"
$consoleProject = Join-Path $scriptDir "src\MasonicCalendar.Console\MasonicCalendar.Console.csproj"
$outputDir = Join-Path $scriptDir "output"

# Verify files exist
if (-not (Test-Path $dataFile)) {
    Write-Host "Error: craft-units.csv not found"
    exit 1
}

if (-not (Test-Path $consoleProject)) {
    Write-Host "Error: Console project not found"
    exit 1
}

# Read the CSV file
Write-Host "Reading craft units from CSV..."
$csv = Import-Csv $dataFile
$units = @()
foreach ($row in $csv) {
    $units += @{ Number = $row.Number; Name = $row.Name }
}

# Apply limit if specified
if ($limit -gt 0) {
    $units = $units | Select-Object -First $limit
    Write-Host "Found $($units.Count) craft units (limited to $limit)"
}
else {
    Write-Host "Found $($units.Count) craft units"
}
Write-Host ""

# Generate PDF for each unit
$startTime = Get-Date
$successCount = 0
$failCount = 0

foreach ($unit in $units) {
    $number = $unit.Number
    $name = $unit.Name
    
    Write-Host "Rendering unit $number - $name..."
    
    try {
        $output = & dotnet run --project $consoleProject -- `
            -template master_v1 `
            -section craft_units `
            -unit $number `
            -output pdf `
            2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  SUCCESS: PDF generated"
            $successCount++
        }
        else {
            Write-Host "  FAILED"
            $failCount++
        }
    }
    catch {
        Write-Host "  ERROR: $_"
        $failCount++
    }
}

Write-Host ""
Write-Host "=============================================="
$endTime = Get-Date
$duration = [Math]::Round(($endTime - $startTime).TotalMinutes, 1)

Write-Host "Summary:"
Write-Host "  Total:       $($units.Count)"
Write-Host "  Successful:  $successCount"
Write-Host "  Failed:      $failCount"
Write-Host "  Duration:    $duration minutes"
Write-Host ""

if ($failCount -eq 0) {
    Write-Host "All PDFs generated successfully!"
    exit 0
}
else {
    Write-Host "Some PDFs failed to generate"
    exit 1
}
