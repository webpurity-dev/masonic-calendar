#Requires -Version 5.1
<#
.SYNOPSIS
Transforms units_v1.5.csv to match units_v1.3.csv structure

.DESCRIPTION
Converts units v1.4 data format to v1.3 by:
- Deriving Short Name from Unit Name
- Generating Super Short Name using abbreviation rules
- Extracting Location from Hall field
- Generating type-specific Email addresses:
  * Craft:  {UnitNo}@dorsetfreemasonry.info
  * RA:     ra{UnitNo}@dorsetfreemasonry.info
  * Mark:   sec{UnitNo}@dorsetmark.org.uk
  * RAM:    scribe{UnitNo}@dorsetmark.org.uk
- Preserving core fields: Unit Type, Unit No, Unit Name, Warrant, Meeting Dates, Last Installation, Hall
- Removing v1.4-specific parsed columns

.PARAMETER Version
Source data version to convert (default: 1.4)
Script will look for units_v{Version}.csv and output units_v{Version}-converted.csv

.PARAMETER InputFile
Override input file path (optional, uses Version parameter by default)

.PARAMETER OutputFile
Override output file path (optional)

.EXAMPLE
.\convert-v1.4-to-v1.3.ps1
.\convert-v1.4-to-v1.3.ps1 -Version 1.4
.\convert-v1.4-to-v1.3.ps1 -Version 1.4 -OutputFile ./custom_output.csv
#>

param(
    [string]$Version = "1.4",
    [string]$InputFile = "",
    [string]$OutputFile = ""
)

# Set defaults based on version if not explicitly provided
$rootDir = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$dataDir = Join-Path $rootDir "document\data"

if ([string]::IsNullOrWhiteSpace($InputFile)) {
    $InputFile = Join-Path $dataDir "units_raw_v$Version.csv"
}
if ([string]::IsNullOrWhiteSpace($OutputFile)) {
    $OutputFile = Join-Path $dataDir "units_v$Version.csv"
}

function Get-SpecialCaseNames {
    <#
    .SYNOPSIS
    Returns hardcoded custom Short/Super Short Names for special cases
    
    .DESCRIPTION
    Some units have custom names that don't follow the standard pattern.
    These are hardcoded based on Unit No.
    
    Special Cases:
    - 8479 (Craft): Custom Super Short "St. Mark"
    - 9508 (Craft): Custom Short "Dorset Provincial Grand Stewards", Super Short "Dorset Stewards"
    - 9942 (Craft): Custom Super Short "Sportsmen's"
    - 1367 (RA): Custom Super Short "Dorset Bikers"
    #>
    param([string]$UnitNo, [string]$UnitType, [string]$UnitName)
    
    $specialCases = @{
        '8479' = @{
            'ShortName' = 'Lodge of Saint Mark'
            'SuperShortName' = 'St. Mark'
        }
        '9508' = @{
            'ShortName' = 'Dorset Provincial Grand Stewards'
            'SuperShortName' = 'Dorset Stewards'
        }
        '9942' = @{
            'ShortName' = "Dorset Sportsmen's Lodge"
            'SuperShortName' = "Sportsmen's "
        }
        '1367' = @{
            'ShortName' = 'Dorset Masonic Bikers Chapter'
            'SuperShortName' = 'Dorset Bikers'
        }
        '1572' = @{
            'ShortName' = 'Dorset Installed Mark Masters'
            'SuperShortName' = 'Dorset Mark Masters'
        }
        '1925' = @{
            'ShortName' = 'Dorset Mark Stewards'
            'SuperShortName' = 'Dorset Stewards'
        }
        '1572_RAM' = @{
            'ShortName' = 'Dorset Installed Commanders'
            'SuperShortName' = 'Dorset Commanders'
        }
    }
    
    # Check for exact match by Unit No (for most cases)
    if ($specialCases.ContainsKey($UnitNo)) {
        return $specialCases[$UnitNo]
    }
    
    # Check for Unit No with Unit Type prefix (for cases with multiple unit types)
    $key = "${UnitNo}_${UnitType.ToUpper()}"
    if ($specialCases.ContainsKey($key)) {
        return $specialCases[$key]
    }
    
    # No special case found
    return $null
}

function Get-SuperShortName {
    <#
    .SYNOPSIS
    Derives Super Short Name from full Unit Name using pattern rules or special cases
    
    .DESCRIPTION
    Rules:
    1. Check for hardcoded special cases first
    2. If starts with "Lodge of " or "Chapter of ": extract everything after and replace "and" with "&"
    3. If ends with " Lodge" or " Chapter": remove that suffix
    4. Otherwise: use the Unit Name as-is
    #>
    param([string]$UnitNo, [string]$UnitType, [string]$UnitName, [string]$CustomShortName)
    
    # Use custom short name if provided
    if (-not [string]::IsNullOrWhiteSpace($CustomShortName)) {
        $name = $CustomShortName
    } else {
        $name = $UnitName.Trim()
    }
    
    # Handle "Lodge of X" or "Chapter of X"
    if ($name -match "^(Lodge|Chapter) of (.+)$") {
        $name = $matches[2]
    }
    # Handle "X Lodge" or "X Chapter" suffix
    elseif ($name -match "^(.+) (Lodge|Chapter)$") {
        $name = $matches[1]
    }
    
    # Replace "and" with "&"
    $name = $name -replace '\s+and\s+', ' & '
    
    return $name.Trim()
}

function Get-EmailAddress {
    <#
    .SYNOPSIS
    Generates email address based on Unit Type and Unit No
    
    .DESCRIPTION
    Email format varies by unit type:
    - Craft: {UnitNo}@dorsetfreemasonry.info
    - RA: ra{UnitNo}@dorsetfreemasonry.info
    - Mark: sec{UnitNo}@dorsetmark.org.uk
    - RAM: scribe{UnitNo}@dorsetmark.org.uk
    #>
    param([string]$UnitType, [string]$UnitNo)
    
    $type = $UnitType.Trim().ToUpper()
    
    switch ($type) {
        'CRAFT' {
            return "$UnitNo@dorsetfreemasonry.info"
        }
        'RA' {
            return "ra$UnitNo@dorsetfreemasonry.info"
        }
        'MARK' {
            return "sec$UnitNo@dorsetmark.org.uk"
        }
        'RAM' {
            return "scribe$UnitNo@dorsetmark.org.uk"
        }
        default {
            # Fallback for unknown types
            return "$UnitNo@dorsetfreemasonry.info"
        }
    }
}

function Get-Location {
    <#
    .SYNOPSIS
    Extracts location (city/town name) from Hall address
    
    .DESCRIPTION
    Reads the first word or phrase before the first comma or Hall name
    Most addresses follow pattern: "City Name, Hall Name, Street, Postcode"
    We extract the city name (first word/phrase before comma)
    #>
    param([string]$HallAddress)
    
    $address = $HallAddress.Trim()
    
    # Extract first part before comma
    if ($address -match "^([^,]+),") {
        $location = $matches[1].Trim()
        return $location
    }
    
    return $address
}

function Clean-MeetingDates {
    <#
    .SYNOPSIS
    Normalizes spacing in Meeting Dates field
    
    .DESCRIPTION
    Rules:
    1. Collapse all multiple spaces (2+) to single space
    2. Ensure DOUBLE space after periods (sentence separation)
    3. Trim leading/trailing whitespace
    Examples:
    - "6.00 pm  on Friday.  Installation" → "6.00 pm on Friday.  Installation"
    - "Jan-Nov. third Wednesday" → "Jan-Nov.  third Wednesday"
    #>
    param([string]$MeetingDates)
    
    if ([string]::IsNullOrWhiteSpace($MeetingDates)) {
        return ""
    }
    
    # Step 1: First, collapse ALL multiple spaces (2 or more) to single space
    $cleaned = [System.Text.RegularExpressions.Regex]::Replace($MeetingDates, '\s{2,}', ' ')
    
    # Step 2: Ensure double space after ALL periods
    # Match period followed by single space and any character (except end of string)
    $cleaned = [System.Text.RegularExpressions.Regex]::Replace($cleaned, '(\. )([^ ])', '.  $2')
    
    # Step 3: Trim and return
    return $cleaned.Trim()
}

# Validate input file exists
if (-not (Test-Path $InputFile)) {
    Write-Error "Input file not found: $InputFile"
    exit 1
}

Write-Host "Converting units_v1.5.csv to v1.3 format..."
Write-Host "Input:  $InputFile"
Write-Host "Output: $OutputFile"
Write-Host ""

# Read entire file as single string to handle multi-line quoted fields
$fileContent = [System.IO.File]::ReadAllText($InputFile)
$data = @()

# Split by newline to get individual lines, but handle quoted fields properly
$lines = @()
$current = ""
$inQuotes = $false

foreach ($char in $fileContent.ToCharArray()) {
    if ($char -eq '"') {
        if ($current.Length -gt 0 -and $current[-1] -eq '"') {
            $current = $current.Substring(0, $current.Length - 1) + '""'
        } else {
            $inQuotes = -not $inQuotes
        }
        $current += $char
    }
    elseif ($char -eq "`n" -and -not $inQuotes) {
        if ($current.Length -gt 0) {
            $lines += $current.TrimEnd("`r")
        }
        $current = ""
    }
    else {
        $current += $char
    }
}

if ($current.Length -gt 0) {
    $lines += $current.TrimEnd("`r")
}

# Process lines starting from line 1 (skip header at line 0)
for ($lineNum = 1; $lineNum -lt $lines.Count; $lineNum++) {
    $line = $lines[$lineNum]
    
    # Parse CSV fields manually
    $fields = @()
    $current = ""
    $inQuotes = $false
    
    for ($i = 0; $i -lt $line.Length; $i++) {
        $char = $line[$i]
        
        if ($char -eq '"') {
            if ($i + 1 -lt $line.Length -and $line[$i + 1] -eq '"') {
                # Escaped quote
                $current += '"'
                $i++
            } else {
                # Toggle quote state
                $inQuotes = -not $inQuotes
            }
        }
        elseif ($char -eq ',' -and -not $inQuotes) {
            # Field separator found
            $fields += $current.Trim()
            $current = ""
        }
        else {
            $current += $char
        }
    }
    
    # Add remaining content as last field
    $fields += $current.Trim()
    
    # Skip lines with insufficient columns or empty unit type
    if ($fields.Count -lt 8 -or [string]::IsNullOrWhiteSpace($fields[0])) {
        continue
    }
    
    # Extract relevant v1.4 fields
    $unitType = $fields[0]
    $unitNo = $fields[1]
    $unitName = $fields[2]
    $warrant = $fields[3]
    $meetingDates = Clean-MeetingDates $fields[4]  # Clean spacing in meeting dates
    $lastInstallation = $fields[5]
    $hall = $fields[6]
    $hallAddress = $fields[7]
    
    # Skip if core fields are empty
    if ([string]::IsNullOrWhiteSpace($unitType) -or [string]::IsNullOrWhiteSpace($unitNo)) {
        continue
    }
    
    # Check for special case names first
    $specialCase = Get-SpecialCaseNames $unitNo $unitType $unitName
    if ($specialCase) {
        $shortName = $specialCase['ShortName']
        $superShortName = $specialCase['SuperShortName']
    } else {
        # Derive Short Name (same as Unit Name in v1.3)
        $shortName = $unitName
        
        # Derive Super Short Name using pattern rules
        $superShortName = Get-SuperShortName $unitNo $unitType $unitName $shortName
    }
    
    # Generate Email based on Unit Type
    $email = Get-EmailAddress $unitType $unitNo
    
    # Create output object
    $row = [PSCustomObject]@{
        'Unit Type' = $unitType
        'Unit No' = $unitNo
        'Unit Name' = $unitName
        'Short Name' = $shortName
        'Super Short Name' = $superShortName
        'Warrant' = $warrant
        'Meeting Dates' = $meetingDates
        'Last Installation' = $lastInstallation
        'Hall' = $hall
        'Location' = $hallAddress  # Use full address directly (no duplication)
        'Email' = $email
    }
    
    $data += $row
}

Write-Host "Converted $($data.Count) units"
Write-Host ""

# Export to CSV with proper formatting
$data | Export-Csv -Path $OutputFile -Encoding UTF8 -NoTypeInformation

Write-Host "[+] Conversion complete: $OutputFile"
Write-Host ""
Write-Host "Next steps:"
Write-Host "1. Review the output file to verify structure"
Write-Host "2. Compare with original v1.3 to ensure all data is present"
Write-Host "3. If satisfied, backup original and rename"
