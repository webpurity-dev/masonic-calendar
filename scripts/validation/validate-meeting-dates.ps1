#Requires -Version 5.1
<#
.SYNOPSIS
    Validates the unit-meetings.csv file for data integrity and coverage issues.

.DESCRIPTION
    This script performs the following validations:
    1. Ensures each unit (UnitType + Number combination) has at least one row
    2. Reports units with multiple meeting definitions
    3. Validates InstallationMonth coverage:
       - InstallationMonth must appear in the Months column, OR
       - InstallationMonth must fall within the StartMonth-EndMonth range
#>

param(
    [string]$Version = "1.5",
    [string]$CsvPath = (Join-Path $PSScriptRoot "..\..\document\data\unit-meetings.csv")
)

Write-Host "== Unit Meetings Validation Script ==" -ForegroundColor Cyan
Write-Host ""

# Load units CSV for cross-validation
$unitsCsvPath = Join-Path $PSScriptRoot "..\..\document\data\units_v$Version.csv"
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

Write-Host "[OK] Loading meetings CSV: $CsvPath" -ForegroundColor Green

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

# Load CSV
$rows = @(Import-Csv -Path $CsvPath)
Write-Host "[OK] Loaded $($rows.Count) rows from CSV" -ForegroundColor Green
Write-Host ""

# Group by UnitType and Number
$grouped = $rows | Group-Object -Property @{Expression={$_.UnitType}}, @{Expression={$_.Number}}

Write-Host "== Unit Summary ==" -ForegroundColor Cyan
Write-Host "Total units in reference file (units_v$Version.csv): $($unitsRef.Count)"
Write-Host "Total unique units in meetings CSV: $($grouped.Count)"
$unitsWithMultiple = @($grouped | Where-Object { $_.Group.Count -gt 1 })
Write-Host "Units with multiple rows: $($unitsWithMultiple.Count)"
Write-Host ""

# Cross-validate units exist in reference file
Write-Host "== Unit Cross-Validation ==" -ForegroundColor Cyan
$refUnits = @($unitsRef | ForEach-Object { "$($_.`"Unit Type`"),$($_.`"Unit No`")" })
$meetingUnits = @($grouped | ForEach-Object { "$($_.Group[0].UnitType),$($_.Group[0].Number)" })
$missingUnits = @($meetingUnits | Where-Object { $_ -notin $refUnits })

Write-Host "Reference file units: $($refUnits.Count)"
Write-Host "Meetings CSV units: $($meetingUnits.Count)"

if ($missingUnits.Count -gt 0) {
    Write-Host "[WARN] Found $($missingUnits.Count) unit(s) in meetings CSV not in reference units_v$Version.csv:" -ForegroundColor Yellow
    $missingUnits | ForEach-Object { Write-Host "  - $_" -ForegroundColor Yellow }
} else {
    Write-Host "[OK] All units in meetings CSV exist in reference file" -ForegroundColor Green
}
Write-Host ""

# Validation
$issues = @()

# Check for day clashes (same unit with multiple meetings on same DayOfWeek+WeekNumber AND overlapping months)
Write-Host "== Clash Detection ==" -ForegroundColor Cyan
$dayClashes = 0
foreach ($unitGroup in $grouped) {
    $unitType = $unitGroup.Group[0].UnitType
    $unitNumber = $unitGroup.Group[0].Number
    
    # Group this unit's meetings by DayOfWeek and WeekNumber
    $dayGroups = $unitGroup.Group | 
        Where-Object { ![string]::IsNullOrWhiteSpace($_.DayOfWeek) -and ![string]::IsNullOrWhiteSpace($_.WeekNumber) } |
        Group-Object -Property @{Expression={$_.DayOfWeek}}, @{Expression={$_.WeekNumber}}
    
    foreach ($dayGroup in $dayGroups) {
        if ($dayGroup.Group.Count -gt 1) {
            # Check if month ranges actually overlap for any pair of meetings
            $meetings = $dayGroup.Group
            for ($i = 0; $i -lt $meetings.Count - 1; $i++) {
                for ($j = $i + 1; $j -lt $meetings.Count; $j++) {
                    if (MonthRangesOverlap $meetings[$i].Months $meetings[$i].StartMonth $meetings[$i].EndMonth `
                                          $meetings[$j].Months $meetings[$j].StartMonth $meetings[$j].EndMonth) {
                        $dayClashes++
                        # Flag both meetings as clashing
                        foreach ($row in @($meetings[$i], $meetings[$j])) {
                            $rowIndex = 0
                            for ($k = 0; $k -lt $rows.Count; $k++) {
                                if ($rows[$k].UnitType -eq $row.UnitType -and `
                                    $rows[$k].Number -eq $row.Number -and `
                                    $rows[$k].Title -eq $row.Title) {
                                    $rowIndex = $k + 2
                                    break
                                }
                            }
                            
                            $issues += [PSCustomObject]@{
                                Type       = "ERROR"
                                UnitType   = $row.UnitType
                                UnitNumber = $row.Number
                                Row        = $rowIndex
                                Title      = $row.Title
                                Issue      = "Day Clash"
                                Details    = "Meets on $($row.DayOfWeek) $($row.WeekNumber) with overlapping month range"
                            }
                        }
                    }
                }
            }
        }
    }
}
if ($dayClashes -eq 0) {
    Write-Host "[OK] No day clashes found" -ForegroundColor Green
} else {
    Write-Host "[WARN] Found $dayClashes meeting(s) with day clashes" -ForegroundColor Yellow
}
Write-Host ""

# Track units with multiple definitions separately (not errors)
$multipleDefinitions = @()

foreach ($unitGroup in $grouped) {
    $unitType = $unitGroup.Group[0].UnitType
    $unitNumber = $unitGroup.Group[0].Number
    $rowCount = $unitGroup.Group.Count
    
    if ($rowCount -gt 1) {
        $definitions = @($unitGroup.Group | ForEach-Object { $_.Title }) -join ", "
        $multipleDefinitions += [PSCustomObject]@{
            UnitType   = $unitType
            UnitNumber = $unitNumber
            DefinitionCount = $rowCount
            Details    = $definitions
        }
    }
    
    foreach ($row in $unitGroup.Group) {
        $rowIndex = 0
        for ($i = 0; $i -lt $rows.Count; $i++) {
            if ($rows[$i].UnitType -eq $row.UnitType -and `
                $rows[$i].Number -eq $row.Number -and `
                $rows[$i].Title -eq $row.Title) {
                $rowIndex = $i + 2
                break
            }
        }
        
        # Validate DayOfWeek
        if (![string]::IsNullOrWhiteSpace($row.DayOfWeek)) {
            if (!(IsValidDayOfWeek $row.DayOfWeek)) {
                $issues += [PSCustomObject]@{
                    Type       = "ERROR"
                    UnitType   = $row.UnitType
                    UnitNumber = $row.Number
                    Row        = $rowIndex
                    Title      = $row.Title
                    Issue      = "Invalid DayOfWeek"
                    Details    = "DayOfWeek '$($row.DayOfWeek)' is not valid. Must be Monday-Sunday."
                }
            }
        }
        
        # Validate month values (StartMonth, EndMonth, Months)
        if (![string]::IsNullOrWhiteSpace($row.StartMonth)) {
            $monthError = ValidateMonthValue $row.StartMonth "StartMonth"
            if ($null -ne $monthError) {
                $issues += [PSCustomObject]@{
                    Type       = "ERROR"
                    UnitType   = $row.UnitType
                    UnitNumber = $row.Number
                    Row        = $rowIndex
                    Title      = $row.Title
                    Issue      = "Invalid Month Value"
                    Details    = $monthError
                }
            }
        }
        
        if (![string]::IsNullOrWhiteSpace($row.EndMonth)) {
            $monthError = ValidateMonthValue $row.EndMonth "EndMonth"
            if ($null -ne $monthError) {
                $issues += [PSCustomObject]@{
                    Type       = "ERROR"
                    UnitType   = $row.UnitType
                    UnitNumber = $row.Number
                    Row        = $rowIndex
                    Title      = $row.Title
                    Issue      = "Invalid Month Value"
                    Details    = $monthError
                }
            }
        }
        
        if (![string]::IsNullOrWhiteSpace($row.Months)) {
            $monthError = ValidateMonthValue $row.Months "Months"
            if ($null -ne $monthError) {
                $issues += [PSCustomObject]@{
                    Type       = "ERROR"
                    UnitType   = $row.UnitType
                    UnitNumber = $row.Number
                    Row        = $rowIndex
                    Title      = $row.Title
                    Issue      = "Invalid Month Value"
                    Details    = $monthError
                }
            }
        }
        
        if (![string]::IsNullOrWhiteSpace($row.InstallationMonth)) {
            $monthError = ValidateMonthValue $row.InstallationMonth "InstallationMonth"
            if ($null -ne $monthError) {
                $issues += [PSCustomObject]@{
                    Type       = "ERROR"
                    UnitType   = $row.UnitType
                    UnitNumber = $row.Number
                    Row        = $rowIndex
                    Title      = $row.Title
                    Issue      = "Invalid Month Value"
                    Details    = $monthError
                }
            }
        }
        
        # Validate InstallationMonth coverage
        $validation = ValidateInstallationMonth `
            -installationMonth $row.InstallationMonth `
            -startMonth $row.StartMonth `
            -endMonth $row.EndMonth `
            -months $row.Months
        
        if ($null -ne $validation) {
            $issues += [PSCustomObject]@{
                Type       = "ERROR"
                UnitType   = $row.UnitType
                UnitNumber = $row.Number
                Row        = $rowIndex
                Title      = $row.Title
                Issue      = "InstallationMonth Not Covered"
                Details    = $validation
            }
        }
    }
}

# Results
Write-Host "== Validation Results ==" -ForegroundColor Cyan

$errors = @($issues | Where-Object { $_.Type -eq "ERROR" })
$clashErrors = @($issues | Where-Object { $_.Issue -eq "Day Clash" })

if ($errors.Count -eq 0 -and $multipleDefinitions.Count -eq 0 -and $missingUnits.Count -eq 0) {
    Write-Host "[OK] All validations passed! No issues found." -ForegroundColor Green
} else {
    if ($errors.Count -gt 0) {
        Write-Host ""
        Write-Host "[ERROR] Found $($errors.Count) error(s):" -ForegroundColor Red
        Write-Host ("-" * 80)
        $errors | Group-Object Issue | ForEach-Object {
            Write-Host "  $($_.Name): $($_.Group.Count) error(s)"
            foreach ($err in $_.Group) {
                Write-Host "    $($err.UnitType) $($err.UnitNumber) - $($err.Title)"
                Write-Host "      -> $($err.Details)"
            }
        }
    }
    
    if ($multipleDefinitions.Count -gt 0) {
        Write-Host ""
        Write-Host "[INFO] Found $($multipleDefinitions.Count) unit(s) with multiple definitions:" -ForegroundColor Blue
        Write-Host ("-" * 80)
        foreach ($multi in $multipleDefinitions) {
            Write-Host "  $($multi.UnitType) $($multi.UnitNumber) [$($multi.DefinitionCount) definitions]: $($multi.Details)" -ForegroundColor Blue
        }
    }
    
    if ($missingUnits.Count -gt 0) {
        Write-Host ""
        Write-Host "[WARN] $($missingUnits.Count) unit(s) not in reference file (units_v$Version.csv)" -ForegroundColor Yellow
    }
}

# Summary
Write-Host ""
if ($errors.Count -gt 0) {
    Write-Host "[ERROR] Found $($errors.Count) error(s):" -ForegroundColor Red
    Write-Host "  - Installation month errors: $(@($errors | Where-Object { $_.Issue -eq 'InstallationMonth Not Covered' }).Count)"
    Write-Host "  - Day clash errors: $($clashErrors.Count)"
}

if ($multipleDefinitions.Count -gt 0) {
    Write-Host "[INFO] Found $($multipleDefinitions.Count) unit(s) with multiple definitions" -ForegroundColor Blue
}

Write-Host ""
Write-Host "[DONE] Validation complete!" -ForegroundColor Green
