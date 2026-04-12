# validate-output.ps1
# Validates that every unit and named member from the CSV data appears in the rendered HTML.
# Data sources are discovered from document/data_sources/*.yaml (excluding meetings).
# Results are written to a timestamped CSV alongside this script.
#
# Usage (run from any directory):
#   .\validation\validate-output.ps1
#   .\validation\validate-output.ps1 -Render
#   .\validation\validate-output.ps1 -UnitType Craft
#   .\validation\validate-output.ps1 -HtmlFile output\my.html

param(
    [string]$HtmlFile = "",
    [switch]$Render,
    [string]$UnitType = "All"
)

$rootDir       = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$dataDir       = Join-Path $rootDir "document\data"
$dataSourceDir = Join-Path $rootDir "document\data_sources"
$consoleDir    = Join-Path $rootDir "src\MasonicCalendar.Console"

# ============================================================
# Read-DataSourceConfig: line-by-line YAML parser (PS 5.1 safe)
# Returns a PSCustomObject with units + membership section config.
# ============================================================
function Read-DataSourceConfig([string]$yamlPath) {
    $lines = Get-Content $yamlPath

    # Result structure
    $cfg = [PSCustomObject]@{
        YamlFile        = (Split-Path $yamlPath -Leaf)
        UnitType        = $null
        UnitsSource     = $null
        UnitFilterField = "Unit Type"
        UnitNoColumn    = "Unit No"
        UnitNameColumn  = "Unit Name"
        MemSections     = [System.Collections.Generic.List[hashtable]]::new()
    }

    $topSection     = $null      # current top-level key (units/officers/etc.)
    $inFiltersBlock = $false     # inside a "filters:" list
    $lastFieldName  = $null      # last "- name: X" seen (for fields)
    $filterFldTemp  = $null      # filter_field seen; waiting for matching filter_value
    $memSrc         = $null      # current membership section source
    $memFilters     = $null      # current membership section filters hashtable
    $memFields      = $null      # current membership section fields hashtable

    function Save-MemSection {
        if ($topSection -and $topSection -ne "units" -and $memSrc) {
            $refCol  = if ($memFields -and $memFields.ContainsKey("Reference")) { $memFields["Reference"] } else { "UniqueRef" }
            $nameCol = if ($memFields -and $memFields.ContainsKey("Name"))      { $memFields["Name"]      } else { "Name"      }
            $uidFld  = "Unit"
            [void]$cfg.MemSections.Add(@{
                Name        = $topSection
                Source      = $memSrc
                Filters     = $memFilters
                RefColumn   = $refCol
                NameColumn  = $nameCol
                UnitIdField = $uidFld
            })
        }
    }

    foreach ($rawLine in $lines) {
        # Skip comment lines
        if ($rawLine -match '^\s*#') { continue }

        # Detect top-level section: no leading whitespace, ends with ":"
        if ($rawLine -match '^([a-zA-Z][a-zA-Z_]*):\s*$') {
            # Save previous membership section before moving on
            Save-MemSection

            $topSection     = $Matches[1]
            $inFiltersBlock = $false
            $lastFieldName  = $null
            $filterFldTemp  = $null

            if ($topSection -ne "units") {
                # Start new membership section context
                $memSrc     = $null
                $memFilters = @{}
                $memFields  = @{}
            }
            continue
        }

        if (-not $topSection) { continue }

        # ---- Units section ------------------------------------------------
        if ($topSection -eq "units") {
            if    ($rawLine -match '^\s+source:\s*"([^"]+)"')       { $cfg.UnitsSource     = $Matches[1] }
            elseif ($rawLine -match '^\s+filter_field:\s*"([^"]+)"') { $cfg.UnitFilterField = $Matches[1] }
            elseif ($rawLine -match '^\s+filter_value:\s*"([^"]+)"') { $cfg.UnitType        = $Matches[1] }
            elseif ($rawLine -match '^\s+-\s*name:\s*"([^"]+)"')     { $lastFieldName       = $Matches[1] }
            elseif ($rawLine -match '^\s+csv_column:\s*"([^"]+)"' -and $lastFieldName) {
                if    ($lastFieldName -eq "Number") { $cfg.UnitNoColumn   = $Matches[1] }
                elseif ($lastFieldName -eq "Name")  { $cfg.UnitNameColumn = $Matches[1] }
                $lastFieldName = $null
            }
            continue
        }

        # ---- Membership sections ------------------------------------------
        if ($rawLine -match '^\s+source:\s*"([^"]+)"')        { $memSrc = $Matches[1]; continue }
        if ($rawLine -match '^\s+filters:\s*$')               { $inFiltersBlock = $true; continue }
        if ($rawLine -match '^\s+fields:\s*$')                { $inFiltersBlock = $false; $lastFieldName = $null; continue }
        if ($rawLine -match '^\s+unit_id_field:\s*"([^"]+)"') {
            # override unit id field if specified
            if ($cfg.MemSections.Count -gt 0) {
                $cfg.MemSections[$cfg.MemSections.Count - 1]['UnitIdField'] = $Matches[1]
            }
            continue
        }

        if ($inFiltersBlock) {
            if    ($rawLine -match '^\s+-?\s*filter_field:\s*"([^"]+)"') { $filterFldTemp = $Matches[1] }
            elseif ($rawLine -match '^\s+filter_value:\s*"([^"]+)"' -and $filterFldTemp) {
                $memFilters[$filterFldTemp] = $Matches[1]
                $filterFldTemp = $null
            }
        } else {
            if    ($rawLine -match '^\s+-\s*name:\s*"([^"]+)"')        { $lastFieldName = $Matches[1] }
            elseif ($rawLine -match '^\s+csv_column:\s*"([^"]+)"' -and $lastFieldName) {
                $memFields[$lastFieldName] = $Matches[1]
                $lastFieldName = $null
            }
        }
    }

    # Save the last membership section
    Save-MemSection

    return $cfg
}

# ============================================================
# Load data source configs from YAML
# ============================================================
$dataSourceFiles = @(Get-ChildItem (Join-Path $dataSourceDir "*.yaml") |
    Where-Object { $_.Name -notmatch 'meetings' })

$allConfigs = [System.Collections.Generic.List[PSCustomObject]]::new()
foreach ($f in $dataSourceFiles) {
    $c = Read-DataSourceConfig $f.FullName
    if ($c.UnitType) { [void]$allConfigs.Add($c) }
}

if ($allConfigs.Count -eq 0) {
    Write-Host "ERROR: No data source YAML files found in $dataSourceDir" -ForegroundColor Red
    exit 1
}

$targetConfigs = if ($UnitType -ne "All") {
    @($allConfigs | Where-Object { $_.UnitType -eq $UnitType })
} else {
    @($allConfigs)
}

if ($targetConfigs.Count -eq 0) {
    Write-Host "ERROR: No data source found for UnitType '$UnitType'" -ForegroundColor Red
    exit 1
}

# ============================================================
# Optionally re-render
# ============================================================
if ($Render) {
    Write-Host "Rendering full document HTML..." -ForegroundColor Cyan
    Push-Location $consoleDir
    dotnet run -- -template master_v1 -output html 2>&1 | Select-Object -Last 4 |
        ForEach-Object { Write-Host "  $_" }
    Pop-Location
}

# ============================================================
# Locate HTML file
# ============================================================
if (-not $HtmlFile) {
    # Auto-detect latest master_v1.X-all-sections.html file
    $outputDir = Join-Path $rootDir "output"
    $latestFile = Get-ChildItem -Path $outputDir -Filter "master_v1.*-all-sections.html" -ErrorAction SilentlyContinue |
                  Sort-Object -Property LastWriteTime -Descending |
                  Select-Object -First 1
    
    if ($latestFile) {
        $HtmlFile = $latestFile.FullName
        Write-Host "Auto-detected latest HTML: $($latestFile.Name)" -ForegroundColor Cyan
    } else {
        $HtmlFile = Join-Path $outputDir "master_v1-all-sections.html"
    }
}
if (-not (Test-Path $HtmlFile)) {
    Write-Host "ERROR: HTML file not found: $HtmlFile" -ForegroundColor Red
    Write-Host "       Run with -Render to generate it first." -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "Validating : $HtmlFile" -ForegroundColor Cyan
Write-Host "Data sources: $($targetConfigs.Count) YAML file(s) ($( ($targetConfigs | ForEach-Object { $_.YamlFile }) -join ', '))" -ForegroundColor Cyan
Write-Host ""

$html = Get-Content $HtmlFile -Raw -Encoding UTF8

# ============================================================
# Build lookup sets from HTML
# ============================================================
$htmlDataIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
[regex]::Matches($html, 'data-id="([^"]*)"') | ForEach-Object {
    [void]$htmlDataIds.Add($_.Groups[1].Value)
}

$htmlAnchors = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
[regex]::Matches($html, '\bid="([^"]*)"') | ForEach-Object {
    [void]$htmlAnchors.Add($_.Groups[1].Value)
}

# ============================================================
# Anchor-ID logic (mirrors GenerateAnchorId in C#)
# ============================================================
function Build-AnchorId([string]$unitType, [int]$unitNumber, [string]$unitName) {
    $ct = $unitType -replace '[^a-zA-Z0-9]', '_'
    $cn = $unitName -replace '[^a-zA-Z0-9]', '_'
    return "unit_${ct}_${unitNumber}_${cn}".ToLower()
}

# ============================================================
# CSV cache
# ============================================================
$csvCache = @{}
function Get-CsvData([string]$source) {
    if (-not $csvCache.ContainsKey($source)) {
        $p = Join-Path $dataDir $source
        $csvCache[$source] = if (Test-Path $p) { @(Import-Csv $p) } else {
            Write-Host "WARNING: CSV not found: $p" -ForegroundColor Yellow
            @()
        }
    }
    return $csvCache[$source]
}

# ============================================================
# Validate
# ============================================================
$timestamp  = Get-Date -Format "yyyy-MM-dd-HHmmss"
$csvOutPath = Join-Path $PSScriptRoot "validation-${timestamp}.csv"
$issues     = [System.Collections.Generic.List[PSCustomObject]]::new()
$grandTotal = 0
$grandFail  = 0
$grandRowsChecked = 0

foreach ($cfg in $targetConfigs) {

    $allUnits  = Get-CsvData $cfg.UnitsSource
    $typeUnits = @($allUnits | Where-Object { $_.($cfg.UnitFilterField) -eq $cfg.UnitType })

    # Pre-filter membership rows per section
    $secData = @{}
    foreach ($sec in $cfg.MemSections) {
        $rows = Get-CsvData $sec.Source
        $secData[$sec.Name] = @($rows | Where-Object {
            $row  = $_
            $pass = $true
            foreach ($fk in $sec.Filters.Keys) {
                if ($row.$fk -ne $sec.Filters[$fk]) { $pass = $false; break }
            }
            $pass
        })
    }

    $typeUnits  = @($typeUnits)
    $unitCount  = $typeUnits.Count
    $typeFail        = 0
    $typeRowsChecked = 0

    # Build unit name lookup for error messages (unit no -> unit name)
    $unitNameMap = @{}
    foreach ($u in $typeUnits) { $unitNameMap[$u.($cfg.UnitNoColumn).Trim()] = $u.($cfg.UnitNameColumn).Trim() }

    Write-Host "=== $($cfg.UnitType) ($unitCount units | $($cfg.YamlFile)) ===" -ForegroundColor Cyan

    # Show section row counts (proves YAML filters are loading the right rows)
    foreach ($sec in $cfg.MemSections) {
        Write-Host "    $($sec.Name): $($secData[$sec.Name].Count) rows" -ForegroundColor DarkGray
    }

    # Check for duplicate UniqueRef values within each section
    foreach ($sec in $cfg.MemSections) {
        # Skip duplicate check for Officers section - officers can legitimately appear multiple times
        # with different Office values (same person in multiple positions)
        if ($sec.Name -eq "officers") {
            continue
        }

        $refGroups = @{}
        foreach ($row in $secData[$sec.Name]) {
            $ref = $row.($sec.RefColumn).Trim()
            if (-not [string]::IsNullOrWhiteSpace($ref)) {
                if (-not $refGroups.ContainsKey($ref)) {
                    $refGroups[$ref] = @()
                }
                $refGroups[$ref] += $row
            }
        }

        # Report any duplicates
        foreach ($ref in $refGroups.Keys) {
            if ($refGroups[$ref].Count -gt 1) {
                $typeFail++
                Write-Host "  DUPLICATE $ref in [$($sec.Name)]" -ForegroundColor Yellow
                foreach ($row in $refGroups[$ref]) {
                    $name   = $row.($sec.NameColumn).Trim()
                    $unitNo = $row.($sec.UnitIdField).Trim()
                    $unitName = if ($unitNameMap.ContainsKey($unitNo)) { $unitNameMap[$unitNo] } else { '(not in units CSV)' }
                    $label = if ($name) { $name } else { '(vacant)' }
                    Write-Host "          $unitNo $unitName - $label" -ForegroundColor Red
                    [void]$issues.Add([PSCustomObject]@{
                        Timestamp  = $timestamp
                        HtmlFile   = (Split-Path $HtmlFile -Leaf)
                        UnitType   = $cfg.UnitType
                        UnitNo     = $unitNo
                        UnitName   = $unitName
                        IssueType  = "DuplicateRef"
                        Section    = $sec.Name
                        MemType    = if ($row.PSObject.Properties['MemType']) { $row.MemType.Trim() } else { $sec.Name }
                        MemberName = $name
                        DataId     = $ref
                    })
                }
                Write-Host ""
            }
        }
    }

    # a) Unit anchor check (from units CSV)
    foreach ($unit in $typeUnits) {
        $unitNo   = $unit.($cfg.UnitNoColumn).Trim()
        $unitName = $unit.($cfg.UnitNameColumn).Trim()
        $anchor   = Build-AnchorId $cfg.UnitType ([int]$unitNo) $unitName
        if (-not $htmlAnchors.Contains($anchor)) {
            $typeFail++
            Write-Host "  FAIL $unitNo  $unitName" -ForegroundColor Yellow
            Write-Host "       Missing anchor (expected id=""$anchor"")" -ForegroundColor Red
            [void]$issues.Add([PSCustomObject]@{
                Timestamp  = $timestamp
                HtmlFile   = (Split-Path $HtmlFile -Leaf)
                UnitType   = $cfg.UnitType
                UnitNo     = $unitNo
                UnitName   = $unitName
                IssueType  = "MissingAnchor"
                Section    = ""
                MemType    = ""
                MemberName = ""
                DataId     = $anchor
            })
        }
    }

    # b) Check EVERY row in EVERY CSV section directly.
    #    CSV (filtered by YAML section rules) is the single source of truth.
    #    No per-unit filtering, no skipping - every row must have a matching data-id in HTML.
    foreach ($sec in $cfg.MemSections) {
        foreach ($row in $secData[$sec.Name]) {
            $ref     = $row.($sec.RefColumn).Trim()
            $memType = if ($row.PSObject.Properties['MemType']) { $row.MemType.Trim() } else { $sec.Name }
            $office  = if ($row.PSObject.Properties['Office'])  { $row.Office.Trim()  } else { '' }
            $dataId  = $ref + '-' + $memType
            if ($office) { $dataId += '-' + $office }

            $typeRowsChecked++

            if (-not $htmlDataIds.Contains($dataId)) {
                $typeFail++
                $name     = $row.($sec.NameColumn).Trim()
                $unitNo   = $row.($sec.UnitIdField).Trim()
                $unitName = if ($unitNameMap.ContainsKey($unitNo)) { $unitNameMap[$unitNo] } else { '(not in units CSV)' }
                $label    = if ($name) { $name } else { '(vacant)' }
                Write-Host "  FAIL $unitNo $unitName" -ForegroundColor Yellow
                Write-Host "       MISSING [$memType] $label (dataId=$dataId)" -ForegroundColor Red
                [void]$issues.Add([PSCustomObject]@{
                    Timestamp  = $timestamp
                    HtmlFile   = (Split-Path $HtmlFile -Leaf)
                    UnitType   = $cfg.UnitType
                    UnitNo     = $unitNo
                    UnitName   = $unitName
                    IssueType  = "MissingMember"
                    Section    = $sec.Name
                    MemType    = $memType
                    MemberName = $name
                    DataId     = $dataId
                })
            }
        }
    }

    $grandTotal       += $unitCount
    $grandFail        += $typeFail
    $grandRowsChecked += $typeRowsChecked

    if ($typeFail -eq 0) {
        Write-Host "  OK   All $unitCount units and $typeRowsChecked membership rows accounted for." -ForegroundColor Green
    } else {
        Write-Host "  FAIL $typeFail issue(s) in $($cfg.UnitType). ($typeRowsChecked rows checked)" -ForegroundColor Red
    }
    Write-Host ""
}

# ============================================================
# Summary + CSV output
# ============================================================
$totalAnchors = ($htmlAnchors | Where-Object { $_ -match '^unit_' }).Count

Write-Host "-------------------------------------------------"
Write-Host "  HTML : $totalAnchors unit anchors, $($htmlDataIds.Count) unique data-id values"
Write-Host "  CSV  : $grandTotal units, $grandRowsChecked membership rows checked"
Write-Host "-------------------------------------------------"

if ($issues.Count -gt 0) {
    $issues | Export-Csv -Path $csvOutPath -NoTypeInformation -Encoding UTF8
    Write-Host "  Report: $csvOutPath ($($issues.Count) issue(s))" -ForegroundColor Yellow
} else {
    Write-Host "  Report: no issues - CSV not written." -ForegroundColor Green
}

if ($grandFail -eq 0) {
    Write-Host "  PASSED - no missing units or members." -ForegroundColor Green
} else {
    Write-Host "  FAILED - $grandFail issue(s) found." -ForegroundColor Red
}

Write-Host ""
Read-Host "Press Enter to exit"

if ($grandFail -gt 0) { exit 1 }
