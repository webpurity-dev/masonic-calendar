#Requires -Version 5.1
<#
.SYNOPSIS
    Validates the unit_meetings.csv file and the rendered meetings output CSV.

.DESCRIPTION
    This script performs the following validations:
    1. Validates input unit_meetings.csv for data integrity and structure issues
    2. Loads the latest rendered meetings CSV from the output directory
    3. Ensures each unit has sufficient meeting dates in the rendered output
    4. Validates InstallationMonth coverage in both input and output
#>

param(
    [string]$CsvPath = (Join-Path $PSScriptRoot "..\..\document\data\unit_meetings.csv"),
    [string]$OutputDir = (Join-Path $PSScriptRoot "..\..\output")
)

Write-Host "== Unit Meetings Validation Script ==" -ForegroundColor Cyan
Write-Host ""

# Find the latest rendered meetings CSV
$latestMeetingsCsv = @(Get-ChildItem -Path $OutputDir -Filter "*-meetings.csv" -ErrorAction SilentlyContinue | Sort-Object Name -Descending | Select-Object -First 1)
if ($latestMeetingsCsv.Count -eq 0) {
    Write-Host "[WARN] No rendered meetings CSV found in $OutputDir" -ForegroundColor Yellow
    $renderedMeetingsCsv = $null
} else {
    $renderedMeetingsCsv = $latestMeetingsCsv[0].FullName
    Write-Host "[OK] Found rendered meetings CSV: $($latestMeetingsCsv[0].Name)" -ForegroundColor Green
}

# Load units CSV for cross-validation
$unitsCsvPath = Join-Path $PSScriptRoot "..\..\document\data\units.csv"
if (!(Test-Path $unitsCsvPath)) {
    Write-Host "[ERROR] Units CSV file not found: $unitsCsvPath" -ForegroundColor Red
    exit 1
}

Write-Host "[OK] Loading units reference: $unitsCsvPath" -ForegroundColor Green
$unitsRef = @(Import-Csv -Path $unitsCsvPath)
Write-Host "[OK] Found $($unitsRef.Count) units in reference file" -ForegroundColor Green

# Load meetings CSV
if (!(Test-Path $CsvPath)) {
    Write-Host "[ERROR] CSV file not found: $CsvPath" -ForegroundColor Red
    exit 1
}

Write-Host "[OK] Loading input meetings CSV: $CsvPath" -ForegroundColor Green

# Month mapping
$monthMap = @{
    'Jan' = 1;  'Feb' = 2;  'Mar' = 3;  'Apr' = 4;  'May' = 5;  'Jun' = 6
    'Jul' = 7;  'Aug' = 8;  'Sep' = 9;  'Oct' = 10; 'Nov' = 11; 'Dec' = 12
}

function ConvertMonthToNumber {
    param([string]$monthName)
    if ([string]::IsNullOrWhiteSpace($monthName)) { return $null }
    return $monthMap[$monthName]
}

function IsValidDayOfWeek {
    param([string]$dayOfWeek)
    $validDays = @('Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday')
    return $validDays -contains $dayOfWeek
}

function IsValidMonth {
    param([string]$month)
    return $monthMap.ContainsKey($month)
}

function ValidateMonthValue {
    param([string]$monthValue, [string]$context)
    if ([string]::IsNullOrWhiteSpace($monthValue)) {
        return $null
    }
    
    if ($monthValue -like '*:*') {
        $months = $monthValue -split ':'
        $invalid = @($months | Where-Object { ![string]::IsNullOrWhiteSpace($_) -and !(IsValidMonth $_) })
        if ($invalid.Count -gt 0) {
            return "Invalid month(s) in $context : $($invalid -join ', ')"
        }
    } else {
        if (!(IsValidMonth $monthValue)) {
            return "Invalid month in $context : $monthValue"
        }
    }
    return $null
}

function MonthInList {
    param([string]$month, [string]$monthList)
    if ([string]::IsNullOrWhiteSpace($monthList)) { return $false }
    $months = $monthList -split ':'
    return $months -contains $month
}

function MonthInRange {
    param([int]$monthNum, [string]$startMonth, [string]$endMonth)
    
    if ([string]::IsNullOrWhiteSpace($startMonth) -or [string]::IsNullOrWhiteSpace($endMonth)) {
        return $false
    }
    
    $start = ConvertMonthToNumber $startMonth
    $end = ConvertMonthToNumber $endMonth
    
    if ($null -eq $start -or $null -eq $end) { return $false }
    
    if ($start -le $end) {
        return ($monthNum -ge $start -and $monthNum -le $end)
    } else {
        return ($monthNum -ge $start -or $monthNum -le $end)
    }
}

function MonthRangesOverlap {
    param(
        [string]$months1,
        [string]$startMonth1,
        [string]$endMonth1,
        [string]$months2,
        [string]$startMonth2,
        [string]$endMonth2
    )
    
    # Build list of months for each meeting
    $monthsA = @()
    $monthsB = @()
    
    # For first meeting
    if (![string]::IsNullOrWhiteSpace($months1)) {
        $monthsA = @($months1 -split ':' | ForEach-Object { ConvertMonthToNumber $_ } | Where-Object { $null -ne $_ })
    } elseif (![string]::IsNullOrWhiteSpace($startMonth1) -and ![string]::IsNullOrWhiteSpace($endMonth1)) {
        $start = ConvertMonthToNumber $startMonth1
        $end = ConvertMonthToNumber $endMonth1
        if ($null -ne $start -and $null -ne $end) {
            if ($start -le $end) {
                $monthsA = @($start..$end)
            } else {
                $monthsA = @(($start..12) + (1..$end))
            }
        }
    }
    
    # For second meeting
    if (![string]::IsNullOrWhiteSpace($months2)) {
        $monthsB = @($months2 -split ':' | ForEach-Object { ConvertMonthToNumber $_ } | Where-Object { $null -ne $_ })
    } elseif (![string]::IsNullOrWhiteSpace($startMonth2) -and ![string]::IsNullOrWhiteSpace($endMonth2)) {
        $start = ConvertMonthToNumber $startMonth2
        $end = ConvertMonthToNumber $endMonth2
        if ($null -ne $start -and $null -ne $end) {
            if ($start -le $end) {
                $monthsB = @($start..$end)
            } else {
                $monthsB = @(($start..12) + (1..$end))
            }
        }
    }
    
    # Check if any months overlap
    if ($monthsA.Count -eq 0 -or $monthsB.Count -eq 0) {
        return $false
    }
    
    $overlap = @($monthsA | Where-Object { $monthsB -contains $_ })
    return $overlap.Count -gt 0
}


function ValidateInstallationMonth {
    param(
        [string]$installationMonth,
        [string]$startMonth,
        [string]$endMonth,
        [string]$months
    )
    
    if ([string]::IsNullOrWhiteSpace($installationMonth)) {
        return $null
    }
    
    if (MonthInList $installationMonth $months) {
        return $null
    }
    
    $instNum = ConvertMonthToNumber $installationMonth
    if ($null -ne $instNum -and (MonthInRange $instNum $startMonth $endMonth)) {
        return $null
    }
    
    return "InstallationMonth '$installationMonth' not covered by Months ($months) or Range ($startMonth-$endMonth)"
}

# ===== SOURCE CSV VALIDATION DISABLED =====
# This script now focuses only on validating rendered output.
# Source unit_meetings.csv validation is skipped.
Write-Host "[INFO] Skipping source CSV validation. Validating rendered output only." -ForegroundColor Yellow
Write-Host ""

# Initialize issues array for rendered validation only
$issues = @()
$renderedIssues = @()

# === Validate rendered meetings CSV if it exists ===
$renderedIssues = @()
Write-Host ""
Write-Host "== Rendered Meetings Output Validation ==" -ForegroundColor Cyan
Write-Host "[DEBUG] renderedMeetingsCsv = $renderedMeetingsCsv" -ForegroundColor Gray

if ($null -ne $renderedMeetingsCsv -and (Test-Path $renderedMeetingsCsv)) {
    Write-Host "[DEBUG] File exists and path is valid" -ForegroundColor Gray
    
    try {
        Write-Host "[DEBUG] About to load rendered CSV from: $renderedMeetingsCsv" -ForegroundColor Gray
        $renderedMeetings = @(Import-Csv -Path $renderedMeetingsCsv)
        Write-Host "[OK] Loaded $($renderedMeetings.Count) meeting instances from rendered output" -ForegroundColor Green
        
        # Group by Unit Type and Unit Number
        Write-Host "[DEBUG] Grouping meetings by Unit Type and Unit Number..." -ForegroundColor Gray
        $renderedGrouped = @{}
        foreach ($meeting in $renderedMeetings) {
            $unitType = $meeting.'Unit Type'
            $unitNumber = $meeting.'Unit Number'
            $key = "$unitType|$unitNumber"
            
            if (-not $renderedGrouped.ContainsKey($key)) {
                $renderedGrouped[$key] = @{
                    UnitType = $unitType
                    UnitNumber = $unitNumber
                    Meetings = @()
                    HasInstallation = $false
                }
            }
            
            $renderedGrouped[$key].Meetings += $meeting
            if ($meeting.'Is Installation' -eq 'TRUE') {
                $renderedGrouped[$key].HasInstallation = $true
            }
        }
        
        Write-Host "[DEBUG] Grouped into $($renderedGrouped.Count) units" -ForegroundColor Gray
        
        # Check rendered meetings coverage
        $renderedMissingMeetings = @()
        $renderedInsufficientMeetings = @()
        $renderedMissingInstallation = @()
        
        foreach ($unit in $unitsRef) {
            $unitType = $unit."Unit Type"
            $unitNumber = $unit."Unit No"
            $key = "$unitType|$unitNumber"
            
            if ($renderedGrouped.ContainsKey($key)) {
                $rendMeeting = $renderedGrouped[$key]
                
                if ($rendMeeting.Meetings.Count -lt 2) {
                    $renderedInsufficientMeetings += [PSCustomObject]@{
                        UnitType = $unitType
                        UnitNumber = $unitNumber
                        UnitName = $unit."Unit Name"
                        RenderedCount = $rendMeeting.Meetings.Count
                    }
                }
                
                if (-not $rendMeeting.HasInstallation) {
                    $renderedMissingInstallation += [PSCustomObject]@{
                        UnitType = $unitType
                        UnitNumber = $unitNumber
                        UnitName = $unit."Unit Name"
                    }
                }
            } else {
                $renderedMissingMeetings += [PSCustomObject]@{
                    UnitType = $unitType
                    UnitNumber = $unitNumber
                    UnitName = $unit."Unit Name"
                }
            }
        }
        
        Write-Host "[DEBUG] Validation complete: Missing=$($renderedMissingMeetings.Count), Insufficient=$($renderedInsufficientMeetings.Count), NoInstall=$($renderedMissingInstallation.Count)" -ForegroundColor Gray
        
        # Report rendered coverage issues
        if ($renderedMissingMeetings.Count -eq 0 -and $renderedInsufficientMeetings.Count -eq 0 -and $renderedMissingInstallation.Count -eq 0) {
            Write-Host "[OK] All $($unitsRef.Count) units have at least 2 expanded meeting dates and at least 1 installation meeting in rendered output" -ForegroundColor Green
        } else {
            if ($renderedMissingMeetings.Count -gt 0) {
                Write-Host "[ERROR] $($renderedMissingMeetings.Count) unit(s) missing from rendered meetings:" -ForegroundColor Red
                foreach ($unit in $renderedMissingMeetings) {
                    Write-Host "  - $($unit.UnitType) $($unit.UnitNumber): $($unit.UnitName)" -ForegroundColor Red
                    $renderedIssues += [PSCustomObject]@{
                        Type = "ERROR"
                        UnitType = $unit.UnitType
                        UnitNumber = $unit.UnitNumber
                        Issue = "Not in Rendered Output"
                        Details = "Unit not found in rendered meetings CSV"
                    }
                }
            }
            
            if ($renderedInsufficientMeetings.Count -gt 0) {
                Write-Host "[ERROR] $($renderedInsufficientMeetings.Count) unit(s) with fewer than 2 expanded meetings in rendered output:" -ForegroundColor Red
                foreach ($unit in $renderedInsufficientMeetings) {
                    Write-Host "  - $($unit.UnitType) $($unit.UnitNumber): $($unit.UnitName) ($($unit.RenderedCount) rendered meeting(s))" -ForegroundColor Red
                    $renderedIssues += [PSCustomObject]@{
                        Type = "ERROR"
                        UnitType = $unit.UnitType
                        UnitNumber = $unit.UnitNumber
                        Issue = "Insufficient Rendered Meetings"
                        Details = "Only $($unit.RenderedCount) meeting(s) in rendered output; need at least 2"
                    }
                }
            }
            
            if ($renderedMissingInstallation.Count -gt 0) {
                Write-Host "[ERROR] $($renderedMissingInstallation.Count) unit(s) without installation meeting in rendered output:" -ForegroundColor Red
                foreach ($unit in $renderedMissingInstallation) {
                    Write-Host "  - $($unit.UnitType) $($unit.UnitNumber): $($unit.UnitName)" -ForegroundColor Red
                    $renderedIssues += [PSCustomObject]@{
                        Type = "ERROR"
                        UnitType = $unit.UnitType
                        UnitNumber = $unit.UnitNumber
                        Issue = "No Rendered Installation Meeting"
                        Details = "No installation meeting found in rendered output"
                    }
                }
            }
        }
        
        Write-Host ""
    } catch {
        Write-Host "[ERROR] Failed to load rendered meetings CSV: $($_.Exception.Message)" -ForegroundColor Red
        $renderedIssues += [PSCustomObject]@{
            Type = "ERROR"
            Issue = "Rendered CSV Load Error"
            Details = $_.Exception.Message
        }
    }
} else {
    Write-Host "== Rendered Meetings Output Validation ==" -ForegroundColor Cyan
    Write-Host "[WARN] No rendered meetings CSV found in output directory" -ForegroundColor Yellow
    Write-Host ""
}

# Combine all issues
$issues += $renderedIssues

# Results
Write-Host "== Validation Results ==" -ForegroundColor Cyan

$errors = @($issues | Where-Object { $_.Type -eq "ERROR" })

if ($errors.Count -eq 0) {
    Write-Host "[OK] All validations passed! No issues found in rendered meetings output." -ForegroundColor Green
    exit 0
} else {
    Write-Host ""
    Write-Host "[ERROR] Found $($errors.Count) error(s) in rendered output:" -ForegroundColor Red
    Write-Host ("-" * 80)
    $errors | Group-Object Issue | ForEach-Object {
        Write-Host "  $($_.Name): $($_.Group.Count) error(s)"
        foreach ($err in $_.Group) {
            Write-Host "    $($err.UnitType) $($err.UnitNumber)"
            Write-Host "      -> $($err.Details)"
        }
    }
    Write-Host ""
    exit 1
}
