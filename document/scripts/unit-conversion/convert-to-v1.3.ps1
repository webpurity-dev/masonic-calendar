#Requires -Version 5.1
<#
.SYNOPSIS
Transforms units_v1.4.csv to match units_v1.3.csv structure

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
if ([string]::IsNullOrWhiteSpace($InputFile)) {
    $InputFile = "e:\Development\repos\masonic-calendar\document\data\units_v$Version.csv"
}
if ([string]::IsNullOrWhiteSpace($OutputFile)) {
    $OutputFile = "e:\Development\repos\masonic-calendar\document\data\units_v$Version-converted.csv"
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

# Validate input file exists
if (-not (Test-Path $InputFile)) {
    Write-Error "Input file not found: $InputFile"
    exit 1
}

Write-Host "Converting units_v1.4.csv to v1.3 format..."
Write-Host "Input:  $InputFile"
Write-Host "Output: $OutputFile"
Write-Host ""

# Read v1.4 data, handling the malformed header row
$data = @()
$first = $true
$lineNumber = 0

Get-Content $InputFile | ForEach-Object {
    $lineNumber++
    
    if ($first) {
        # Skip header and potential junk lines
        $first = $false
        return
    }
    
    # Parse CSV manually to handle quoted fields
    $fields = @()
    $current = ""
    $inQuotes = $false
    $chars = $_.ToCharArray()
    
    for ($i = 0; $i -lt $chars.Count; $i++) {
        $char = $chars[$i]
        
        if ($char -eq '"') {
            # Check if it's an escaped quote
            if ($i + 1 -lt $chars.Count -and $chars[$i + 1] -eq '"') {
                $current += '"'
                $i++
            } else {
                $inQuotes = -not $inQuotes
            }
        }
        elseif ($char -eq ',' -and -not $inQuotes) {
            $fields += $current.Trim()
            $current = ""
        }
        else {
            $current += $char
        }
    }
    $fields += $current.Trim()
    
    # Skip lines with insufficient columns
    if ($fields.Count -lt 8) {
        return
    }
    
    # Extract relevant v1.4 fields (indices 0-7)
    $unitType = $fields[0]
    $unitNo = $fields[1]
    $unitName = $fields[2]
    $warrant = $fields[3]
    $meetingDates = $fields[4]
    $lastInstallation = $fields[5]
    $hall = $fields[6]
    $hallAddress = $fields[7]  # "Add No Postcode" field
    
    # Skip if core fields are empty
    if ([string]::IsNullOrWhiteSpace($unitType) -or [string]::IsNullOrWhiteSpace($unitNo)) {
        return
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
    
    # Extract Location from Hall Address (usually first word/phrase)
    $location = Get-Location $hallAddress
    
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
        'Location' = "$location,$hallAddress"  # Construct full location
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
