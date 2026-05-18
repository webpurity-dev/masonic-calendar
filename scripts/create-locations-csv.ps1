#Requires -Version 5.1
<#
.SYNOPSIS
Extract unique locations from units_v1.6.csv and create unit_locations.csv

.DESCRIPTION
Reads the units CSV file, extracts unique halls with their location data,
and creates a new unit_locations.csv file for the split data model.
#>

param(
    [string]$DataPath = $PSScriptRoot
)

Write-Host "Reading units_v1.6.csv from: $DataPath"

# Read the CSV
$csv = Import-Csv (Join-Path $DataPath "units_v1.6.csv")

# Extract unique halls with their location data
$locations = @()
$processedHalls = @{}

foreach ($row in $csv) {
    $hall = $row.Hall
    
    if (-not $processedHalls.ContainsKey($hall)) {
        $locations += [PSCustomObject]@{
            Hall = $hall
            Location = $row.Location
            What3Words = $row.'What3Words'
            ImageFile = ''
        }
        $processedHalls[$hall] = $true
    }
}

# Sort by hall name
$locations = $locations | Sort-Object Hall

# Export to CSV
$outputPath = Join-Path $DataPath "unit_locations.csv"
$locations | Export-Csv -Path $outputPath -NoTypeInformation -Encoding UTF8

Write-Host "Created unit_locations.csv with $($locations.Count) locations"
Write-Host ""
Write-Host "First 5 locations:"
Get-Content $outputPath | Select-Object -First 6
