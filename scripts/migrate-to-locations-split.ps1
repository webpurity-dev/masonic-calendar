#Requires -Version 5.1
<#
.SYNOPSIS
Migrate units_v1.6.csv to remove Location and What3Words columns

.DESCRIPTION
Removes Location and What3Words columns from units_v1.6.csv since they are now
stored in unit_locations.csv. This completes the data split implementation.

Backs up the original file before making changes.
#>

param(
    [string]$DataPath = $PSScriptRoot,
    [switch]$Force
)

$unitsFile = Join-Path $DataPath "units_v1.6.csv"
$backupFile = Join-Path $DataPath "units_v1.6-backup.csv"

Write-Host "Location CSV Split Migration"
Write-Host "============================"
Write-Host ""

# Check if backup exists
if (Test-Path $backupFile) {
    Write-Host "Backup file already exists: units_v1.6-backup.csv"
} else {
    Write-Host "Creating backup: units_v1.6-backup.csv"
    Copy-Item $unitsFile $backupFile
}

Write-Host ""
Write-Host "Reading units_v1.6.csv..."
$csv = Import-Csv $unitsFile

# Get column count before
$columnsBefore = ($csv[0] | Get-Member -MemberType NoteProperty).Count
Write-Host "Columns before: $columnsBefore"

# Remove Location and What3Words columns
$columnsToKeep = $csv[0] | Get-Member -MemberType NoteProperty | Where-Object {
    $_.Name -ne "Location" -and $_.Name -ne "What3Words"
}

Write-Host "Removing columns: Location, What3Words"

# Create new CSV with only the kept columns
$newCsv = @()
foreach ($row in $csv) {
    $newRow = [PSCustomObject]@{}
    foreach ($prop in $columnsToKeep) {
        $newRow | Add-Member -MemberType NoteProperty -Name $prop.Name -Value $row.($prop.Name)
    }
    $newCsv += $newRow
}

# Validate row count
$rowsBefore = $csv.Count
$rowsAfter = $newCsv.Count
Write-Host ""
Write-Host "Row count validation:"
Write-Host "  Before: $rowsBefore rows"
Write-Host "  After:  $rowsAfter rows"

if ($rowsBefore -ne $rowsAfter) {
    Write-Host "ERROR: Row count mismatch! Migration failed."
    exit 1
}

$columnsAfter = ($newCsv[0] | Get-Member -MemberType NoteProperty).Count
Write-Host ""
Write-Host "Column count validation:"
Write-Host "  Before: $columnsBefore columns"
Write-Host "  After:  $columnsAfter columns"
Write-Host "  Removed: $($columnsBefore - $columnsAfter) columns"

if ($columnsAfter -ne ($columnsBefore - 2)) {
    Write-Host "ERROR: Column count mismatch! Expected $($columnsBefore - 2), got $columnsAfter"
    exit 1
}

Write-Host ""
if (-not $Force) {
    Write-Host "Preview of updated CSV (first 3 rows):"
    $newCsv | Select-Object -First 3 | Format-Table
    Write-Host ""
    $confirm = Read-Host "Proceed with writing updated units_v1.6.csv? (yes/no)"
    if ($confirm -ne "yes") {
        Write-Host "Migration cancelled."
        exit 0
    }
}

# Write the new CSV
Write-Host "Writing updated units_v1.6.csv..."
$newCsv | Export-Csv -Path $unitsFile -NoTypeInformation -Encoding UTF8

Write-Host ""
Write-Host "Migration complete!"
Write-Host "  Original saved to: units_v1.6-backup.csv"
Write-Host "  Updated file: units_v1.6.csv"
Write-Host "  New locations file: unit_locations.csv"
