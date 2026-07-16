# Split membership.csv by Unit Type into separate CSV files
# Usage: .\split-membership-by-order.ps1

param(
    [string]$MembershipFile = "../../document/data/membership.csv",
    [string]$OutputFolder = "../../document/data"
)

# Resolve paths to absolute
$scriptDir = Split-Path -Parent (Get-Item $PSCommandPath).FullName
$membershipPath = Join-Path $scriptDir $MembershipFile
$outputPath = Join-Path $scriptDir $OutputFolder

# Validate input file exists
if (-not (Test-Path $membershipPath)) {
    Write-Error "Membership file not found: $membershipPath"
    exit 1
}

# Validate output folder exists
if (-not (Test-Path $outputPath)) {
    Write-Error "Output folder not found: $outputPath"
    exit 1
}

Write-Host "Reading membership.csv from: $membershipPath" -ForegroundColor Cyan
Write-Host "Output folder: $outputPath" -ForegroundColor Cyan

try {
    # Read all data
    $csv = Import-Csv -Path $membershipPath
    Write-Host "Total rows: $($csv.Count)" -ForegroundColor Yellow
    
    # Get unique Unit Types
    $unitTypes = $csv | Select-Object -ExpandProperty "Unit Type" -Unique | Where-Object { $_ -and $_.Trim() } | Sort-Object
    Write-Host "Found $($unitTypes.Count) unit type(s): $($unitTypes -join ', ')" -ForegroundColor Yellow
    
    # Split and write each unit type to its own file
    foreach ($unitType in $unitTypes) {
        $filtered = $csv | Where-Object { $_."Unit Type" -eq $unitType }
        $rowCount = @($filtered).Count
        
        # Standardize unit type name for filename (lowercase, no special chars)
        $fileName = $unitType -replace '[^a-zA-Z0-9]', '' | ForEach-Object { $_.ToLower() }
        $outputFile = Join-Path $outputPath "${fileName}_membership.csv"
        
        # Export to CSV
        $filtered | Export-Csv -Path $outputFile -NoTypeInformation -Encoding UTF8
        Write-Host "[OK] $unitType ($rowCount rows) -> ${fileName}_membership.csv" -ForegroundColor Green
    }
    
    Write-Host "`nSplit complete!" -ForegroundColor Green
    Write-Host "New files created in: $outputPath" -ForegroundColor Cyan
}
catch {
    Write-Error "Error during split: $($_.Exception.Message)"
    exit 1
}
