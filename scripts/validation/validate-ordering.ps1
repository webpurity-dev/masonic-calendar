# validate-ordering.ps1
# Validates that officers, past masters, joining past masters, and members are in correct sort order.
# Uses the master CSV export (master_v1.*.2-members.csv) to check ordering within each unit and category.
#
# Ordering Rules:
# - Officers: sorted by PosNo (ascending)
# - Past Masters: sorted by Year (ascending), then PosNo if no Year
# - Joining Past Masters: sorted by Year (ascending), then PosNo if no Year
# - Members: sorted by Year (ascending) IF any members have Year data, else by PosNo
# - Honorary Members: no specific order enforced
#
# Usage (run from scripts/validation/ or project root):
#   .\validate-ordering.ps1
#   .\validate-ordering.ps1 -CsvPath "path\to\members.csv"
#   .\validate-ordering.ps1 -Verbose

param(
    [string]$CsvPath = "",
    [switch]$Verbose
)

$rootDir = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$masterYamlPath = Join-Path $rootDir "document\master_v1.yaml"

# ============================================================
# Get version from master_v1.yaml
# ============================================================
function Get-DocumentVersion([string]$yamlPath) {
    if (Test-Path $yamlPath) {
        $lines = Get-Content $yamlPath
        foreach ($line in $lines) {
            if ($line -match '^\s*version\s*:\s*(.+)$') {
                return $Matches[1].Trim().Trim('"\"')
            }
        }
    }
    return "unknown"
}

$documentVersion = Get-DocumentVersion $masterYamlPath

# Helper function to extract first year from date string (same logic as C# ExtractYearAsInt)
# Examples: "2021" → 2021, "2025, 2026" → 2025, "2021/2022" → 2021, "2020-21" → 2020
function Get-SortableYear([string]$dateString) {
    if ([string]::IsNullOrWhiteSpace($dateString)) {
        return [int]::MaxValue
    }
    
    # Extract first number that looks like a year (4 digits)
    if ($dateString -match '(\d{4})') {
        try {
            return [int]$Matches[1]
        } catch {
            return [int]::MaxValue
        }
    }
    
    return [int]::MaxValue
}

# Auto-discover CSV if not provided
if ([string]::IsNullOrWhiteSpace($CsvPath)) {
    $outputDir = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) "output"
    $csvFiles = Get-ChildItem $outputDir -Filter "*-members.csv" | Sort-Object -Property LastWriteTime -Descending
    if ($csvFiles) {
        $CsvPath = $csvFiles[0].FullName
    } else {
        Write-Host "ERROR: No members CSV found in $outputDir" -ForegroundColor Red
        exit 1
    }
}

if (-not (Test-Path $CsvPath)) {
    Write-Host "ERROR: CSV file not found: $CsvPath" -ForegroundColor Red
    exit 1
}

Write-Host "===================================================================" -ForegroundColor Cyan
Write-Host "  Ordering Validation Report" -ForegroundColor Cyan
Write-Host "===================================================================" -ForegroundColor Cyan
Write-Host "CSV: $(Split-Path $CsvPath -Leaf)" -ForegroundColor Yellow
Write-Host ""

# Import CSV
$allRecords = Import-Csv $CsvPath

# Group by unit type AND unit number (to avoid mixing different degree types with same number)
$units = $allRecords | Group-Object -Property @("Unit Type", "Unit Number")
$totalIssues = 0
$totalUnits = $units.Count
$allIssues = [System.Collections.Generic.List[PSCustomObject]]::new()
Write-Host "[1] Checking ordering within each unit and category..." -ForegroundColor Yellow
Write-Host ""

foreach ($unitGroup in $units) {
    $unitType = $unitGroup.Group[0]."Unit Type"
    $unitNo = $unitGroup.Group[0]."Unit Number"
    $records = $unitGroup.Group
    $unitIssues = 0
    
    # Get unique categories in order they appear
    $categories = $records | Select-Object -ExpandProperty Category -Unique
    
    foreach ($category in $categories) {
        $categoryRecords = $records | Where-Object { $_.Category -eq $category }
        
        if ($categoryRecords.Count -le 1) {
            continue  # Skip if 0 or 1 record (nothing to validate)
        }
        
        $orderingIssues = @()
        
        if ($category -eq "Officer") {
            # Officers should be sorted by PosNo (ascending)
            # Note: CSV doesn't export PosNo, so we check by appearance order matches numeric pattern
            Write-Host "  Unit $unitNo ($unitType) - Officers: $($categoryRecords.Count) records" -ForegroundColor Gray
            # PosNo not in CSV, so we validate based on office position hierarchy instead
            $offices = @("W.M.", "S.W.", "J.W.", "Treasurer", "Secretary", "D.C.", "I.G.", "Tyler")
            $officeIndices = @()
            foreach ($record in $categoryRecords) {
                $office = $record.Office
                $idx = $offices.IndexOf($office)
                $officeIndices += $idx
            }
            
            # Check if officers are in a sensible order (first few offices in order)
            for ($i = 0; $i -lt $officeIndices.Count - 1; $i++) {
                if ($officeIndices[$i] -gt $officeIndices[$i + 1] -and $officeIndices[$i] -gt 0 -and $officeIndices[$i + 1] -gt 0) {
                    $orderingIssues += "Officers out of order: {0} (idx {1}) before {2} (idx {3})" -f `
                        $categoryRecords[$i].Name, $officeIndices[$i], `
                        $categoryRecords[$i + 1].Name, $officeIndices[$i + 1]
                }
            }
        }
        elseif ($category -eq "PastMaster") {
            # Past Masters should be sorted by Year (ascending)
            Write-Host "  Unit $unitNo ($unitType) - Past Masters: $($categoryRecords.Count) records" -ForegroundColor Gray
            
            $prevYear = 0
            for ($i = 0; $i -lt $categoryRecords.Count; $i++) {
                $record = $categoryRecords[$i]
                $year = Get-SortableYear $record.Year
                
                if ($year -lt $prevYear) {
                    $orderingIssues += "Past Master out of order at row $($i + 1): {0} (Year: {1}) comes after {2}" -f `
                        $record.Name, $record.Year, $categoryRecords[$i - 1].Name
                }
                $prevYear = $year
            }
        }
        elseif ($category -eq "JoiningPastMaster") {
            # Joining Past Masters should be sorted by Year (ascending)
            Write-Host "  Unit $unitNo ($unitType) - Joining Past Masters: $($categoryRecords.Count) records" -ForegroundColor Gray
            
            $prevYear = 0
            for ($i = 0; $i -lt $categoryRecords.Count; $i++) {
                $record = $categoryRecords[$i]
                $year = Get-SortableYear $record.Year
                
                if ($year -lt $prevYear) {
                    $orderingIssues += "Joining Past Master out of order at row $($i + 1): {0} (Year: {1}) comes after {2}" -f `
                        $record.Name, $record.Year, $categoryRecords[$i - 1].Name
                }
                $prevYear = $year
            }
        }
        elseif ($category -eq "Member") {
            # Members should be sorted by Year (ascending) IF any have year data
            # If all members lack year data, they should be sorted by PosNo
            Write-Host "  Unit $unitNo ($unitType) - Members: $($categoryRecords.Count) records" -ForegroundColor Gray
            
            $hasAnyYear = $categoryRecords | Where-Object { -not [string]::IsNullOrWhiteSpace($_.Year) }
            
            if ($hasAnyYear.Count -gt 0) {
                # Sort by Year
                $prevYear = 0
                for ($i = 0; $i -lt $categoryRecords.Count; $i++) {
                    $record = $categoryRecords[$i]
                    $year = Get-SortableYear $record.Year
                    
                    if ($year -lt $prevYear) {
                        $orderingIssues += "Member out of order at row $($i + 1): {0} (Year: {1}) comes after {2}" -f `
                            $record.Name, $record.Year, $categoryRecords[$i - 1].Name
                    }
                    $prevYear = $year
                }
            } else {
                # All members lack year - no specific ordering to enforce in CSV (would need PosNo which isn't exported)
                Write-Host "    (All members lack Year data - ordering cannot be validated without PosNo export)" -ForegroundColor Gray
            }
        }
        elseif ($category -eq "HonoraryMember") {
            Write-Host "  Unit $unitNo ($unitType) - Honorary Members: $($categoryRecords.Count) records (no order enforced)" -ForegroundColor Gray
        }
        
        if ($orderingIssues.Count -gt 0) {
            $unitIssues += $orderingIssues.Count
            $totalIssues += $orderingIssues.Count
            Write-Host "    ⚠️  ISSUES FOUND ($($orderingIssues.Count)):" -ForegroundColor Red
            foreach ($issue in $orderingIssues) {
                Write-Host "      - $issue" -ForegroundColor Red                # Add to issues list
                [void]$allIssues.Add([PSCustomObject]@{
                    Timestamp = (Get-Date -Format 'yyyy-MM-dd-HHmmss')
                    CsvFile = (Split-Path $CsvPath -Leaf)
                    UnitType = $unitType
                    UnitNo = $unitNo
                    Category = $category
                    Issue = $issue
                })            }
        }
    }
}

Write-Host ""
Write-Host "===================================================================" -ForegroundColor Cyan
Write-Host "  Summary" -ForegroundColor Cyan
Write-Host "===================================================================" -ForegroundColor Cyan
Write-Host "Total units checked: $totalUnits" -ForegroundColor Yellow
Write-Host "Total ordering issues found: $totalIssues" -ForegroundColor $(if ($totalIssues -eq 0) { "Green" } else { "Red" })
Write-Host ""
# Export to CSV
if ($allIssues.Count -gt 0) {
    $timestamp = Get-Date -Format 'yyyy-MM-dd-HHmmss'
    $csvOutPath = Join-Path $PSScriptRoot "validation-ordering-${documentVersion}-${timestamp}.csv"
    $allIssues | Export-Csv -Path $csvOutPath -NoTypeInformation -Encoding UTF8
    Write-Host "Report: $csvOutPath ($($allIssues.Count) issue(s))" -ForegroundColor Yellow
} else {
    Write-Host "Report: No issues - CSV not written." -ForegroundColor Green
}

Write-Host \"\"
if ($totalIssues -eq 0) {
    Write-Host "✅ All orderings are correct!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "❌ Found $totalIssues ordering issues that should be reviewed." -ForegroundColor Red
    exit 1
}
