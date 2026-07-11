# Validates membership.csv structure and required columns
# Usage:
#   .\validate-membership-csv.ps1
#   .\validate-membership-csv.ps1 -CsvPath "path\to\membership.csv" -Verbose

param(
    [string]$CsvPath = "$PSScriptRoot\..\..\document\data\membership.csv",
    [switch]$Verbose
)

$timestamp = Get-Date -Format "yyyy-MM-dd HHmmss"
$reportPath = "$PSScriptRoot\..\..\output\membership-validation-$timestamp.txt"
$issues = @()
$warnings = @()

Write-Host "=====================================================================" -ForegroundColor Cyan
Write-Host "  Membership CSV Validation Report" -ForegroundColor Cyan
Write-Host "=====================================================================" -ForegroundColor Cyan
Write-Host ""

$requiredColumns = @(
    "Unique Ref",
    "Unit Type",
    "Unit",
    "Unit No",
    "Mem Type",
    "Name",
    "Join Date"
)

$recommendedColumns = @(
    "Provincial Rank",
    "Date Accorded",
    "Grand Rank",
    "GR Date Accorded"
)

$optionalRankColumns = @(
    "Rank Oth Prov",
    "OP Date Accorded",
    "Lndn Rank",
    "LR Date Accorded"
)

$optionalGroupingColumns = @(
    "Grouping",
    "Table"
)

Write-Host "[1] File Validation" -ForegroundColor Yellow

if (-not (Test-Path $CsvPath)) {
    Write-Host "  [FAIL] File not found" -ForegroundColor Red
    Write-Host "    Path: $CsvPath" -ForegroundColor Red
    $issues += "File not found: $CsvPath"
    exit 1
}

Write-Host "  [PASS] File exists" -ForegroundColor Green
Write-Host "    Path: $CsvPath" -ForegroundColor Gray
Write-Host "    Size: $((Get-Item $CsvPath).Length / 1KB)KB" -ForegroundColor Gray

Write-Host ""
Write-Host "[2] CSV Structure Validation" -ForegroundColor Yellow

try {
    $csv = Import-Csv -Path $CsvPath -ErrorAction Stop
    $headers = $csv[0].PSObject.Properties.Name
    
    Write-Host "  [PASS] CSV parsed successfully" -ForegroundColor Green
    Write-Host "    Total columns: $($headers.Count)" -ForegroundColor Gray
    Write-Host "    Total rows: $($csv.Count)" -ForegroundColor Gray
    Write-Host ""
    
    if ($Verbose) {
        Write-Host "  Column Headers:" -ForegroundColor Gray
        $headers | ForEach-Object { Write-Host "    - $_" -ForegroundColor Gray }
        Write-Host ""
    }
}
catch {
    Write-Host "  [FAIL] Could not parse CSV file" -ForegroundColor Red
    Write-Host "    Error: $($_.Exception.Message)" -ForegroundColor Red
    $issues += "CSV parsing failed: $($_.Exception.Message)"
    exit 1
}

Write-Host "[3] Required Columns Validation" -ForegroundColor Yellow

$requiredMissing = @()
foreach ($column in $requiredColumns) {
    if ($column -in $headers) {
        Write-Host "  [OK] '$column'" -ForegroundColor Green
    }
    else {
        Write-Host "  [FAIL] Missing required column '$column'" -ForegroundColor Red
        $issues += "Required column missing: $column"
        $requiredMissing += $column
    }
}

if ($requiredMissing.Count -gt 0) {
    Write-Host ""
    Write-Host "  $($requiredMissing.Count) required column(s) missing" -ForegroundColor Red
}
else {
    Write-Host ""
    Write-Host "  All required columns present" -ForegroundColor Green
}

Write-Host ""
Write-Host "[4] Recommended Columns Validation" -ForegroundColor Yellow

$recommendedMissing = @()
foreach ($column in $recommendedColumns) {
    if ($column -in $headers) {
        Write-Host "  [OK] '$column'" -ForegroundColor Green
    }
    else {
        Write-Host "  [WARN] Missing recommended column: '$column'" -ForegroundColor Yellow
        $warnings += "Recommended column missing: $column"
        $recommendedMissing += $column
    }
}

if ($recommendedMissing.Count -gt 0) {
    Write-Host ""
    Write-Host "  $($recommendedMissing.Count) recommended column(s) missing" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "[5] Optional Rank Columns" -ForegroundColor Yellow

$optionalRankPresent = @()
foreach ($column in $optionalRankColumns) {
    if ($column -in $headers) {
        Write-Host "  [OK] '$column'" -ForegroundColor Green
        $optionalRankPresent += $column
    }
}

if ($optionalRankPresent.Count -eq 0) {
    Write-Host "  [INFO] No optional rank columns present (not critical)" -ForegroundColor Cyan
}
else {
    Write-Host "  [INFO] $($optionalRankPresent.Count)/$($optionalRankColumns.Count) optional rank column(s) present" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "[6] Optional Grouping Columns" -ForegroundColor Yellow

$optionalGroupingPresent = @()
foreach ($column in $optionalGroupingColumns) {
    if ($column -in $headers) {
        Write-Host "  [OK] '$column'" -ForegroundColor Green
        $optionalGroupingPresent += $column
    }
}

if ($optionalGroupingPresent.Count -eq 0) {
    Write-Host "  [INFO] No grouping columns present (only needed for multi-degree units)" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "[7] YAML Configuration Validation" -ForegroundColor Yellow

# Helper function to extract csv_column references only from sections that reference membership.csv
function Get-MembershipCsvColumnsFromYaml {
    param([string]$YamlPath)
    
    $content = Get-Content -Path $YamlPath -Raw
    $columns = @{}  # hash of column -> section names that reference it
    
    # Split into logical sections (lines starting without indentation followed by colon)
    $lines = $content -split "`n"
    $currentSection = $null
    $currentSource = $null
    
    foreach ($line in $lines) {
        # Skip comments and empty lines
        if ($line -match '^\s*#' -or [string]::IsNullOrWhiteSpace($line)) {
            continue
        }
        
        # Detect section header (no leading whitespace, ends with colon, no content after colon)
        if ($line -match '^([a-z_]+):\s*$') {
            $currentSection = $Matches[1]
            $currentSource = $null
            continue
        }
        
        # If we're in a section, look for the source field
        if ($currentSection -and $line -match '^\s+source:\s*"([^"]+)"') {
            $currentSource = $Matches[1]
            continue
        }
        
        # If we have a current section with membership.csv source (exact match, not companion_membership.csv), extract csv_column references
        if ($currentSection -and $currentSource -eq "membership.csv" -and $line -match 'csv_column:\s*"([^"]+)"') {
            $col = $Matches[1]
            if ($col -notin $columns) {
                $columns[$col] = @()
            }
            $columns[$col] += $currentSection
        }
    }
    
    return $columns
}

# Parse all YAML data source files and extract csv_column references (only from membership.csv sections)
$yamlDir = "$PSScriptRoot\..\..\document\data_sources"
$yamlFiles = Get-ChildItem -Path $yamlDir -Filter "*_data_source.yaml"

$yamlColumnReferences = @{}
$yamlMembershipFiles = @()

foreach ($yamlFile in $yamlFiles) {
    # Extract columns only from sections that reference membership.csv
    $sectionColumns = Get-MembershipCsvColumnsFromYaml -YamlPath $yamlFile.FullName
    
    if ($sectionColumns.Count -eq 0) {
        Write-Host "  [SKIP] $($yamlFile.BaseName) (no sections reference membership.csv)" -ForegroundColor Gray
        continue
    }
    
    $yamlMembershipFiles += $yamlFile.BaseName
    
    # Merge into overall column references
    foreach ($col in $sectionColumns.Keys) {
        if ($col -notin $yamlColumnReferences.Keys) {
            $yamlColumnReferences[$col] = @()
        }
        foreach ($section in $sectionColumns[$col]) {
            $yamlColumnReferences[$col] += "$($yamlFile.BaseName):$section"
        }
    }
}

Write-Host "  Found $($yamlColumnReferences.Count) unique csv_column references in membership.csv sections" -ForegroundColor Gray
Write-Host "  Checked $($yamlMembershipFiles.Count) YAML file(s): $($yamlMembershipFiles -join ', ')" -ForegroundColor Gray
Write-Host ""

$yamlMissing = 0
foreach ($csvCol in $yamlColumnReferences.Keys | Sort-Object) {
    $references = $yamlColumnReferences[$csvCol]
    
    if ($csvCol -in $headers) {
        Write-Host "  [OK] '$csvCol'" -ForegroundColor Green
    }
    else {
        Write-Host "  [FAIL] CSV column not found: '$csvCol' (referenced in: $($references -join ', '))" -ForegroundColor Red
        $issues += "YAML references non-existent CSV column '$csvCol' in: $($references -join ', ')"
        $yamlMissing++
    }
}

if ($yamlMissing -gt 0) {
    Write-Host ""
    Write-Host "  $yamlMissing column(s) referenced in YAML but missing from CSV" -ForegroundColor Red
}
else {
    Write-Host ""
    Write-Host "  All YAML-referenced columns present in CSV" -ForegroundColor Green
}

Write-Host ""
Write-Host "[8] Comprehensive YAML to CSV Column Validation (All Sources)" -ForegroundColor Yellow

# Helper function to extract all csv_column references from a YAML section
function Get-YamlSectionColumns {
    param(
        [string]$YamlPath,
        [string]$SectionName
    )
    
    $content = Get-Content -Path $YamlPath -Raw
    $columns = @{}  # hash of column -> source
    
    $lines = $content -split "`n"
    $inSection = $false
    $sectionIndent = $null
    $currentSource = $null
    
    foreach ($line in $lines) {
        # Skip comments and empty lines
        if ($line -match '^\s*#' -or [string]::IsNullOrWhiteSpace($line)) {
            continue
        }
        
        # Check if we found the target section
        if ($line -match "^([a-z_]+):\s*$" -and $Matches[1] -eq $SectionName) {
            $inSection = $true
            $sectionIndent = 0
            continue
        }
        
        # If in our section
        if ($inSection) {
            # Check for a new top-level section (no indentation followed by colon)
            if ($line -match '^([a-z_]+):\s*$') {
                # We've hit the next section, so we're done
                break
            }
            
            # Extract source field (must be indented)
            if ($line -match '^\s+source:\s*"([^"]+)"') {
                $currentSource = $Matches[1]
                continue
            }
            
            # Extract csv_column references
            if ($line -match 'csv_column:\s*"([^"]+)"') {
                $col = $Matches[1]
                if ($col -notin $columns) {
                    $columns[$col] = $currentSource
                }
            }
        }
    }
    
    return @{
        columns = $columns
        source = $currentSource
    }
}

# Scan all YAML data source files
$yamlDir = "$PSScriptRoot\..\..\document\data_sources"
$csvBaseDir = "$PSScriptRoot\..\..\document\data"
$yamlFiles = Get-ChildItem -Path $yamlDir -Filter "*_data_source.yaml" | Sort-Object Name

$yamlCsvValidationIssues = 0
$yamlCsvValidationWarnings = 0
$yamlSectionsChecked = @()

foreach ($yamlFile in $yamlFiles) {
    $content = Get-Content -Path $yamlFile.FullName -Raw
    
    # Extract all section names (lines starting with no whitespace, ending with colon)
    $sectionMatches = [regex]::Matches($content, '^([a-z_]+):\s*$', 'Multiline')
    
    foreach ($match in $sectionMatches) {
        $sectionName = $match.Groups[1].Value
        
        # Get columns and source for this section
        $result = Get-YamlSectionColumns -YamlPath $yamlFile.FullName -SectionName $sectionName
        $sectionColumns = $result.columns
        $sourceFile = $result.source
        
        # Skip sections with no csv_column references or no source
        if ($sectionColumns.Count -eq 0 -or [string]::IsNullOrWhiteSpace($sourceFile)) {
            continue
        }
        
        $yamlSectionsChecked += @{
            file = $yamlFile.BaseName
            section = $sectionName
            source = $sourceFile
            columnCount = $sectionColumns.Count
        }
        
        # Try to load the CSV to get its headers
        $csvPath = Join-Path $csvBaseDir $sourceFile
        
        # Special handling for no_officers.csv (placeholder file, not yet populated)
        if ($sourceFile -eq "no_officers.csv") {
            Write-Host "  [WARN] $($yamlFile.BaseName) section '$sectionName' uses no_officers.csv (not yet configured)" -ForegroundColor Yellow
            $warnings += "Section $($yamlFile.BaseName):$sectionName references no_officers.csv which is not yet configured"
            $yamlCsvValidationWarnings++
            continue
        }
        
        if (-not (Test-Path $csvPath)) {
            Write-Host "  [FAIL] CSV not found: $sourceFile (referenced in $($yamlFile.BaseName):$sectionName)" -ForegroundColor Red
            $issues += "YAML references non-existent CSV file: $sourceFile (in $($yamlFile.BaseName):$sectionName)"
            $yamlCsvValidationIssues++
            continue
        }
        
        try {
            $csvData = Import-Csv -Path $csvPath -ErrorAction Stop
            
            # Handle empty CSV files (only headers, no data)
            if ($csvData.Count -eq 0) {
                Write-Host "  [WARN] $($yamlFile.BaseName) section '$sectionName' references empty CSV: $sourceFile (contains headers only, no data)" -ForegroundColor Yellow
                $warnings += "Section $($yamlFile.BaseName):$sectionName references empty CSV file $sourceFile"
                $yamlCsvValidationWarnings++
                continue
            }
            
            $csvHeaders = $csvData[0].PSObject.Properties.Name
        }
        catch {
            Write-Host "  [FAIL] Could not parse CSV: $sourceFile (referenced in $($yamlFile.BaseName):$sectionName)" -ForegroundColor Red
            $issues += "Failed to parse CSV $sourceFile referenced in $($yamlFile.BaseName):$sectionName"
            $yamlCsvValidationIssues++
            continue
        }
        
        # Check if all referenced columns exist in the CSV
        $missingCols = @()
        foreach ($col in $sectionColumns.Keys) {
            if ($col -notin $csvHeaders) {
                $missingCols += $col
            }
        }
        
        if ($missingCols.Count -gt 0) {
            Write-Host "  [FAIL] $($yamlFile.BaseName) section '$sectionName' references missing columns:" -ForegroundColor Red
            foreach ($col in $missingCols) {
                Write-Host "    - '$col' (not found in $sourceFile)" -ForegroundColor Red
            }
            $issues += "YAML section $($yamlFile.BaseName):$sectionName references non-existent CSV columns: $($missingCols -join ', ') in $sourceFile"
            $yamlCsvValidationIssues++
        }
    }
}

Write-Host "  Validated $($yamlSectionsChecked.Count) YAML section(s) with CSV references" -ForegroundColor Gray
if ($yamlCsvValidationWarnings -gt 0) {
    Write-Host "  [WARN] $yamlCsvValidationWarnings section(s) use placeholder/unconfigured files (warnings)" -ForegroundColor Yellow
}
if ($yamlCsvValidationIssues -eq 0) {
    Write-Host "  [PASS] All YAML csv_column references are valid" -ForegroundColor Green
}
else {
    Write-Host "  [$yamlCsvValidationIssues issue(s) found]" -ForegroundColor Red
}

Write-Host ""
Write-Host "[9] Data Quality Checks" -ForegroundColor Yellow

$emptyRefCount = ($csv | Where-Object { [string]::IsNullOrWhiteSpace($_."Unique Ref") }).Count
$emptyNameCount = ($csv | Where-Object { [string]::IsNullOrWhiteSpace($_."Name") }).Count
$emptyUnitNoCount = ($csv | Where-Object { [string]::IsNullOrWhiteSpace($_."Unit No") }).Count
$emptyMemTypeCount = ($csv | Where-Object { [string]::IsNullOrWhiteSpace($_."Mem Type") }).Count

Write-Host "  Unique Ref: $($csv.Count - $emptyRefCount)/$($csv.Count) populated" -ForegroundColor Gray
if ($emptyRefCount -gt 0) {
    Write-Host "    [WARN] $emptyRefCount empty values" -ForegroundColor Yellow
    $warnings += "$emptyRefCount rows have empty Unique Ref"
}

Write-Host "  Name: $($csv.Count - $emptyNameCount)/$($csv.Count) populated" -ForegroundColor Gray
if ($emptyNameCount -gt 0) {
    Write-Host "    [WARN] $emptyNameCount empty values" -ForegroundColor Yellow
    $warnings += "$emptyNameCount rows have empty Name"
}

Write-Host "  Unit No: $($csv.Count - $emptyUnitNoCount)/$($csv.Count) populated" -ForegroundColor Gray
if ($emptyUnitNoCount -gt 0) {
    Write-Host "    [WARN] $emptyUnitNoCount empty values" -ForegroundColor Yellow
    $warnings += "$emptyUnitNoCount rows have empty Unit No"
}

Write-Host "  Mem Type: $($csv.Count - $emptyMemTypeCount)/$($csv.Count) populated" -ForegroundColor Gray
if ($emptyMemTypeCount -gt 0) {
    Write-Host "    [WARN] $emptyMemTypeCount empty values" -ForegroundColor Yellow
    $warnings += "$emptyMemTypeCount rows have empty Mem Type"
}

Write-Host ""
Write-Host "[10] Data Summary" -ForegroundColor Yellow

$unitTypes = ($csv."Unit Type" | Sort-Object -Unique) -join ", "
Write-Host "  Unit Types: $unitTypes" -ForegroundColor Gray

$memTypes = ($csv."Mem Type" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique) -join ", "
Write-Host "  Membership Types: $memTypes" -ForegroundColor Gray

Write-Host "  Unique Units: $(($csv."Unit No" | Sort-Object -Unique).Count)" -ForegroundColor Gray

Write-Host ""
Write-Host "=====================================================================" -ForegroundColor Cyan

if ($issues.Count -eq 0 -and $warnings.Count -eq 0) {
    Write-Host "  [PASS] VALIDATION PASSED - No issues found!" -ForegroundColor Green
    $exitCode = 0
}
elseif ($issues.Count -eq 0) {
    Write-Host "  [WARN] VALIDATION PASSED WITH WARNINGS" -ForegroundColor Yellow
    Write-Host "    $($warnings.Count) warning(s) found - review recommended" -ForegroundColor Yellow
    $exitCode = 0
}
else {
    Write-Host "  [FAIL] VALIDATION FAILED - $($issues.Count) critical issue(s) found" -ForegroundColor Red
    $exitCode = 1
}

Write-Host ""
Write-Host "Report saved to: $reportPath" -ForegroundColor Gray
Write-Host "=====================================================================" -ForegroundColor Cyan

exit $exitCode

