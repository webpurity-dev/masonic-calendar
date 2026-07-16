# Validates split membership CSV files discovered from data_source YAML configurations
# Usage:
#   .\validate-membership-csv.ps1
#   .\validate-membership-csv.ps1 -DataSourceDir "path\to\data_sources" -DataDir "path\to\data" -Verbose

param(
    [string]$DataSourceDir = "$PSScriptRoot\..\..\document\data_sources",
    [string]$DataDir = "$PSScriptRoot\..\..\document\data",
    [switch]$Verbose
)

# Extract version from master_v1.yaml
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

$rootDir = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$masterYamlPath = Join-Path $rootDir "document\master_v1.yaml"
$documentVersion = Get-DocumentVersion $masterYamlPath

$timestamp = Get-Date -Format "yyyy-MM-dd HHmmss"
$reportPath = "$PSScriptRoot\..\..\output\membership-validation-$timestamp.txt"
$globalIssues = @()
$globalWarnings = @()

Write-Host "=====================================================================" -ForegroundColor Cyan
Write-Host "  Membership CSV Files Validation Report (from data_source YAMLs)" -ForegroundColor Cyan
Write-Host "=====================================================================" -ForegroundColor Cyan
Write-Host ""

# Helper function to extract source CSV and csv_column references from YAML (per-section)
function Get-CsvSourceAndColumns {
    param([string]$YamlPath)
    
    $content = Get-Content -Path $YamlPath -Raw
    $sectionInfo = @{}  # Maps section name -> @{ Source: "csv", Columns: @(col1, col2, ...) }
    
    $lines = $content -split "`n"
    $currentSection = $null
    $currentSource = $null
    $currentColumns = @()
    
    foreach ($line in $lines) {
        if ($line -match '^\s*#' -or [string]::IsNullOrWhiteSpace($line)) {
            continue
        }
        
        # Detect section header (no leading whitespace, ends with colon)
        if ($line -match '^([a-z_]+):\s*$') {
            # Save previous section if it had a source
            if ($currentSection -and $currentSource) {
                $sectionInfo[$currentSection] = @{
                    Source = $currentSource
                    Columns = $currentColumns
                }
            }
            
            $currentSection = $Matches[1]
            $currentSource = $null
            $currentColumns = @()
            continue
        }
        
        # Look for source field to get the CSV file
        if ($currentSection -and $line -match '^\s+source:\s*"([^"]+)"') {
            $currentSource = $Matches[1]
            continue
        }
        
        # Extract csv_column references for this source
        if ($currentSection -and $currentSource -and $line -match 'csv_column:\s*"([^"]+)"') {
            $col = $Matches[1]
            if ($col -notin $currentColumns) {
                $currentColumns += $col
            }
        }
    }
    
    # Don't forget the last section
    if ($currentSection -and $currentSource) {
        $sectionInfo[$currentSection] = @{
            Source = $currentSource
            Columns = $currentColumns
        }
    }
    
    return $sectionInfo
}

Write-Host "[1] Discovering data sources from YAML files" -ForegroundColor Yellow

if (-not (Test-Path $DataSourceDir)) {
    Write-Host "  [FAIL] DataSourceDir not found: $DataSourceDir" -ForegroundColor Red
    $globalIssues += "DataSourceDir not found: $DataSourceDir"
    exit 1
}

if (-not (Test-Path $DataDir)) {
    Write-Host "  [FAIL] DataDir not found: $DataDir" -ForegroundColor Red
    $globalIssues += "DataDir not found: $DataDir"
    exit 1
}

$yamlFiles = Get-ChildItem -Path $DataSourceDir -Filter "*_data_source.yaml" | Sort-Object Name

if ($yamlFiles.Count -eq 0) {
    Write-Host "  [FAIL] No data_source YAML files found" -ForegroundColor Red
    $globalIssues += "No data_source YAML files found in $DataSourceDir"
    exit 1
}

Write-Host "  Found $($yamlFiles.Count) YAML file(s)" -ForegroundColor Green
Write-Host ""

# Build map of CSV file -> YAML section info
# Structure: $csvToSections[csvFile] = @( { YamlFile, Section, Columns }, ... )
$csvToSections = @{}
$timestamp = Get-Date -Format "yyyy-MM-dd-HHmmss"

foreach ($yamlFile in $yamlFiles) {
    $sectionInfo = Get-CsvSourceAndColumns -YamlPath $yamlFile.FullName
    
    foreach ($section in $sectionInfo.Keys) {
        $csvFile = $sectionInfo[$section].Source
        $columns = $sectionInfo[$section].Columns
        
        if ($csvFile -notin $csvToSections) {
            $csvToSections[$csvFile] = @()
        }
        $csvToSections[$csvFile] += @{
            YamlFile = $yamlFile.BaseName
            Section = $section
            Columns = $columns
        }
    }
}

Write-Host "[2] Validating membership CSV files" -ForegroundColor Yellow
Write-Host ""

$totalFiles = $csvToSections.Keys.Count
$passedFiles = 0
$failedFiles = 0
$issues = [System.Collections.Generic.List[PSCustomObject]]::new()

foreach ($csvFile in $csvToSections.Keys | Sort-Object) {
    $csvPath = Join-Path $DataDir $csvFile
    $sectionRefs = $csvToSections[$csvFile]
    
    Write-Host "CSV: $csvFile" -ForegroundColor Cyan
    Write-Host "  Path: $csvPath" -ForegroundColor Gray
    
    if (-not (Test-Path $csvPath)) {
        Write-Host "  [FAIL] File not found" -ForegroundColor Red
        $globalIssues += "CSV file not found: $csvFile"
        
        # Add issue record for each section
        foreach ($ref in $sectionRefs) {
            [void]$issues.Add([PSCustomObject]@{
                Timestamp = $timestamp
                CsvFile = $csvFile
                YamlFile = $ref.YamlFile
                Section = $ref.Section
                IssueType = "FileNotFound"
                Columns = ""
                Status = "FAIL"
                Details = "CSV file does not exist"
            })
        }
        
        $failedFiles++
        Write-Host ""
        continue
    }
    
    $fileSize = (Get-Item $csvPath).Length / 1KB
    Write-Host "  Size: $($fileSize)KB" -ForegroundColor Gray
    
    # Show which YAMLs/sections reference this file
    Write-Host "  Referenced by:" -ForegroundColor Gray
    $uniqueYamls = @($sectionRefs | ForEach-Object { $_['YamlFile'] } | Select-Object -Unique)
    foreach ($yaml in $uniqueYamls) {
        $sectionsForYaml = @($sectionRefs | Where-Object { $_['YamlFile'] -eq $yaml } | ForEach-Object { $_['Section'] })
        Write-Host "    - $yaml → $($sectionsForYaml -join ', ')" -ForegroundColor Gray
    }
    
    try {
        $csv = Import-Csv -Path $csvPath -ErrorAction Stop
        
        # Handle empty CSV files (only headers, no data)
        if ($null -eq $csv -or $csv.Count -eq 0) {
            Write-Host "  Structure: [WARN] Empty file (headers only, no data rows)" -ForegroundColor Yellow
            Write-Host "  Columns: [SKIP] No data rows to validate columns" -ForegroundColor Gray
            $passedFiles++
            Write-Host ""
            continue
        }
        
        $headers = $csv[0].PSObject.Properties.Name
        $rowCount = @($csv).Count
        
        Write-Host "  Structure: [PASS]" -ForegroundColor Green
        Write-Host "    Rows: $rowCount, Columns: $($headers.Count)" -ForegroundColor Gray
        
        # Validate columns per-section
        $csvPassed = $true
        foreach ($ref in $sectionRefs) {
            $section = $ref.Section
            $expectedCols = $ref.Columns
            $missingCols = @($expectedCols | Where-Object { $_ -notin $headers })
            
            if ($missingCols.Count -eq 0) {
                Write-Host "  Columns [$section]: [PASS] All $($expectedCols.Count) column(s) present" -ForegroundColor Green
                [void]$issues.Add([PSCustomObject]@{
                    Timestamp = $timestamp
                    CsvFile = $csvFile
                    YamlFile = $ref.YamlFile
                    Section = $section
                    IssueType = "ColumnsValidated"
                    Columns = $expectedCols -join ", "
                    Status = "PASS"
                    Details = "All required columns found"
                })
            } else {
                Write-Host "  Columns [$section]: [FAIL] Missing $($missingCols.Count) column(s)" -ForegroundColor Red
                Write-Host "    Missing: $($missingCols -join ', ')" -ForegroundColor Red
                $globalIssues += "$csvFile [$section]: Missing columns: $($missingCols -join ', ')"
                $csvPassed = $false
                
                [void]$issues.Add([PSCustomObject]@{
                    Timestamp = $timestamp
                    CsvFile = $csvFile
                    YamlFile = $ref.YamlFile
                    Section = $section
                    IssueType = "MissingColumns"
                    Columns = $missingCols -join ", "
                    Status = "FAIL"
                    Details = "Required columns not found in CSV"
                })
            }
        }
        
        if ($csvPassed) {
            $passedFiles++
        } else {
            $failedFiles++
        }
        
        if ($Verbose) {
            Write-Host "    All columns in CSV:" -ForegroundColor Gray
            $headers | ForEach-Object { Write-Host "      - $_" -ForegroundColor Gray }
        }
    }
    catch {
        Write-Host "  [FAIL] Could not parse CSV: $($_.Exception.Message)" -ForegroundColor Red
        $globalIssues += "$($csvFile): Parse error: $($_.Exception.Message)"
        
        foreach ($ref in $sectionRefs) {
            [void]$issues.Add([PSCustomObject]@{
                Timestamp = $timestamp
                CsvFile = $csvFile
                YamlFile = $ref.YamlFile
                Section = $ref.Section
                IssueType = "ParseError"
                Columns = ""
                Status = "FAIL"
                Details = $_.Exception.Message
            })
        }
        
        $failedFiles++
    }
    
    Write-Host ""
}

Write-Host "[3] Validation Summary" -ForegroundColor Yellow
Write-Host "  Total files validated: $totalFiles" -ForegroundColor Gray
Write-Host "  Passed: $passedFiles" -ForegroundColor Green
Write-Host "  Failed: $failedFiles" -ForegroundColor $(if ($failedFiles -gt 0) { "Red" } else { "Green" })

if ($globalIssues.Count -gt 0) {
    Write-Host ""
    Write-Host "Issues found ($($globalIssues.Count)):" -ForegroundColor Red
    $globalIssues | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
}

Write-Host ""
Write-Host "=====================================================================" -ForegroundColor Cyan
if ($failedFiles -eq 0) {
    Write-Host "✓ All membership CSV files are valid" -ForegroundColor Green
}
else {
    Write-Host "✗ $failedFiles file(s) failed validation" -ForegroundColor Red
}
Write-Host "=====================================================================" -ForegroundColor Cyan

# Write results to CSV
$csvOutPath = Join-Path $PSScriptRoot "validation-membership-${documentVersion}-${timestamp}.csv"
$issues | Export-Csv -Path $csvOutPath -NoTypeInformation -Encoding UTF8
Write-Host ""
Write-Host "Results written to: $csvOutPath" -ForegroundColor Cyan



