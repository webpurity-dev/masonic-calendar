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
        # Only save if: not units section AND has source AND has filters AND has Reference field
        if ($topSection -and $topSection -ne "units" -and $memSrc -and $memFilters.Count -gt 0 -and $memFields.ContainsKey("Reference")) {
            $refCol      = $memFields["Reference"]
            $nameCol     = if ($memFields.ContainsKey("Name")) { $memFields["Name"] } else { "Name" }
            $positionCol = if ($memFields.ContainsKey("Position")) { $memFields["Position"] } else { $null }
            $uidFld      = if ($memFields.ContainsKey("UnitId")) { $memFields["UnitId"] } else { "Unit" }
            [void]$cfg.MemSections.Add(@{
                Name        = $topSection
                Source      = $memSrc
                Filters     = $memFilters
                RefColumn   = $refCol
                NameColumn  = $nameCol
                PositionColumn = $positionCol
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
        # Strip "data/" prefix if present to avoid duplication (e.g. data/data/file.csv)
        $cleanSource = $source -replace '^data[/\\]', ''
        $p = Join-Path $dataDir $cleanSource
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
$csvOutPath = Join-Path $PSScriptRoot "validation-output-${documentVersion}-${timestamp}.csv"
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
        # with different Office values (same person in multiple positions). Also skip vacant officers.
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

        # Report any duplicates as warnings (not failures)
        foreach ($ref in $refGroups.Keys) {
            if ($refGroups[$ref].Count -gt 1) {
                Write-Host "  WARN  $ref in [$($sec.Name)] - duplicate rows in CSV" -ForegroundColor Yellow
                # Only log the first instance as a warning; don't fail the validation
                $row = $refGroups[$ref][0]
                $name   = $row.($sec.NameColumn).Trim()
                $unitNo = $row.($sec.UnitIdField).Trim()
                $unitName = if ($unitNameMap.ContainsKey($unitNo)) { $unitNameMap[$unitNo] } else { '(not in units CSV)' }
                $label = if ($name) { $name } else { '(vacant)' }
                [void]$issues.Add([PSCustomObject]@{
                    Timestamp  = $timestamp
                    HtmlFile   = (Split-Path $HtmlFile -Leaf)
                    UnitType   = $cfg.UnitType
                    UnitNo     = $unitNo
                    UnitName   = $unitName
                    IssueType  = "WARNING"
                    Issue      = "DuplicateRef-MembershipCsv"
                    Section    = $sec.Name
                    MemType    = if ($row.PSObject.Properties['MemType']) { $row.MemType.Trim() } else { $sec.Name }
                    MemberName = $name
                    DataId     = $ref
                })
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
                IssueType  = "ERROR"
                Issue      = "MissingAnchor"
                Section    = ""
                MemType    = ""
                MemberName = ""
                DataId     = $anchor
            })
        }
    }

    # Check for units with no membership data
    $unitsWithMembers = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $unitsWithOfficers = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $unitsWithValidOfficers = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $unitsWithMinimalOfficers = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $unitsWithPastMasters = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $unitsWithHonoraryMembers = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    
    # Count officers with valid names per unit (for majority check)
    $officerNameCounts = @{}  # unitNo -> { total, withNames }
    
    # Track past master data quality (years, ranks)
    $pastMasterYearsCount = @{}  # unitNo -> { total, withYears }
    $pastMasterRanksCount = @{}  # unitNo -> { total, withRanks }
    
    # Track joining past master data quality
    $joiningPastMasterYearsCount = @{}  # unitNo -> { total, withYears }
    $joiningPastMasterRanksCount = @{}  # unitNo -> { total, withRanks }
    
    # Track honorary member ranks
    $honoraryMemberRanksCount = @{}  # unitNo -> { total, withRanks }

    foreach ($sec in $cfg.MemSections) {
        foreach ($row in $secData[$sec.Name]) {
            $unitNo = $row.($sec.UnitIdField).Trim()
            if (-not [string]::IsNullOrWhiteSpace($unitNo)) {
                # Track by section type
                if ($sec.Name -match "officers") {
                    $name = $row.($sec.NameColumn).Trim()
                    $office = if ($sec.PositionColumn) { $row.($sec.PositionColumn).Trim() } else { "" }
                    
                    # Skip vacant officers (empty name, contains "vacant", or empty office) 
                    if ([string]::IsNullOrWhiteSpace($name) -or $name -ilike "*vacant*" -or [string]::IsNullOrWhiteSpace($office)) {
                        continue
                    }
                    
                    [void]$unitsWithOfficers.Add($unitNo)
                    # Count officers with names
                    if (-not $officerNameCounts.ContainsKey($unitNo)) {
                        $officerNameCounts[$unitNo] = @{ total = 0; withNames = 0 }
                    }
                    $officerNameCounts[$unitNo]['total']++
                    if (-not [string]::IsNullOrWhiteSpace($name) -and $name -ne "Vacant") {
                        $officerNameCounts[$unitNo]['withNames']++
                    }
                } elseif ($sec.Name -match "past_heads|past_masters") {
                    [void]$unitsWithPastMasters.Add($unitNo)
                    # Track past master years and ranks
                    if (-not $pastMasterYearsCount.ContainsKey($unitNo)) {
                        $pastMasterYearsCount[$unitNo] = @{ total = 0; withYears = 0 }
                        $pastMasterRanksCount[$unitNo] = @{ total = 0; withRanks = 0 }
                    }
                    $pastMasterYearsCount[$unitNo]['total']++
                    $pastMasterRanksCount[$unitNo]['total']++
                    
                    # Check for YearInstalled
                    $yearField = $row.PSObject.Properties | Where-Object { $_.Name -match "Installed|YearInstalled" } | Select-Object -First 1
                    if ($yearField -and -not [string]::IsNullOrWhiteSpace($yearField.Value)) {
                        $pastMasterYearsCount[$unitNo]['withYears']++
                    }
                    
                    # Check for any rank (Provincial Rank, Grand Rank, Prov Rank Oth Prov, Lndn Rank)
                    $hasRank = $false
                    foreach ($prop in $row.PSObject.Properties) {
                        if ($prop.Name -match "Rank" -and -not [string]::IsNullOrWhiteSpace($prop.Value)) {
                            $hasRank = $true
                            break
                        }
                    }
                    if ($hasRank) {
                        $pastMasterRanksCount[$unitNo]['withRanks']++
                    }
                } elseif ($sec.Name -match "joining_past") {
                    # Track joining past master years and ranks
                    if (-not $joiningPastMasterYearsCount.ContainsKey($unitNo)) {
                        $joiningPastMasterYearsCount[$unitNo] = @{ total = 0; withYears = 0 }
                        $joiningPastMasterRanksCount[$unitNo] = @{ total = 0; withRanks = 0 }
                    }
                    $joiningPastMasterYearsCount[$unitNo]['total']++
                    $joiningPastMasterRanksCount[$unitNo]['total']++
                    
                    # Check for JoinedDate
                    $joinedField = $row.PSObject.Properties | Where-Object { $_.Name -match "JoinedDate|Join" } | Select-Object -First 1
                    if ($joinedField -and -not [string]::IsNullOrWhiteSpace($joinedField.Value)) {
                        $joiningPastMasterYearsCount[$unitNo]['withYears']++
                    }
                    
                    # Check for any rank
                    $hasRank = $false
                    foreach ($prop in $row.PSObject.Properties) {
                        if ($prop.Name -match "Rank" -and -not [string]::IsNullOrWhiteSpace($prop.Value)) {
                            $hasRank = $true
                            break
                        }
                    }
                    if ($hasRank) {
                        $joiningPastMasterRanksCount[$unitNo]['withRanks']++
                    }
                } elseif ($sec.Name -match "honorary") {
                    [void]$unitsWithHonoraryMembers.Add($unitNo)
                    # Track honorary member ranks
                    if (-not $honoraryMemberRanksCount.ContainsKey($unitNo)) {
                        $honoraryMemberRanksCount[$unitNo] = @{ total = 0; withRanks = 0 }
                    }
                    $honoraryMemberRanksCount[$unitNo]['total']++
                    
                    # Check for any rank (multiple rank types)
                    $hasRank = $false
                    foreach ($prop in $row.PSObject.Properties) {
                        if ($prop.Name -match "Rank" -and -not [string]::IsNullOrWhiteSpace($prop.Value)) {
                            $hasRank = $true
                            break
                        }
                    }
                    if ($hasRank) {
                        $honoraryMemberRanksCount[$unitNo]['withRanks']++
                    }
                } elseif ($sec.Name -match "members" -and $sec.Name -notmatch "past|honorary") {
                    [void]$unitsWithMembers.Add($unitNo)
                }
            }
        }
    }
    
    # Check officers: distinguish between 0 officers (ERROR) vs. mostly vacant (WARNING)
    foreach ($unitNo in $officerNameCounts.Keys) {
        $counts = $officerNameCounts[$unitNo]
        if ($counts['total'] -eq 0) {
            # No officers at all = ERROR
            # Don't add to any valid set
        } elseif ($counts['withNames'] -lt [math]::Ceiling($counts['total'] / 2.0)) {
            # Majority have no names = WARNING (MinimalOfficerData)
            [void]$unitsWithMinimalOfficers.Add($unitNo)
        } else {
            # Majority have names = valid
            [void]$unitsWithValidOfficers.Add($unitNo)
        }
    }

    foreach ($unit in $typeUnits) {
        $unitNo   = $unit.($cfg.UnitNoColumn).Trim()
        $unitName = $unit.($cfg.UnitNameColumn).Trim()
        
        # Check for missing or invalid officers
        if (-not $unitsWithValidOfficers.Contains($unitNo)) {
            # Distinguish between no officers (ERROR) vs. mostly vacant (WARNING)
            # ERROR: Unit not in $officerNameCounts (zero officer rows) OR has 0 total officers
            if (-not $officerNameCounts.ContainsKey($unitNo) -or $officerNameCounts[$unitNo]['total'] -eq 0) {
                # 0 officers = ERROR
                Write-Host "  FAIL $unitNo  $unitName" -ForegroundColor Yellow
                Write-Host "       No officer data found" -ForegroundColor Red
                [void]$issues.Add([PSCustomObject]@{
                    Timestamp  = $timestamp
                    HtmlFile   = (Split-Path $HtmlFile -Leaf)
                    UnitType   = $cfg.UnitType
                    UnitNo     = $unitNo
                    UnitName   = $unitName
                    IssueType  = "ERROR"
                    Issue      = "NoOfficerData"
                    Section    = ""
                    MemType    = ""
                    MemberName = ""
                    DataId     = ""
                })
            } elseif ($unitsWithMinimalOfficers.Contains($unitNo)) {
                # Majority vacant = WARNING
                Write-Host "  WARN $unitNo  $unitName" -ForegroundColor Yellow
                Write-Host "       Minimal officer data found (majority vacant)" -ForegroundColor Yellow
                [void]$issues.Add([PSCustomObject]@{
                    Timestamp  = $timestamp
                    HtmlFile   = (Split-Path $HtmlFile -Leaf)
                    UnitType   = $cfg.UnitType
                    UnitNo     = $unitNo
                    UnitName   = $unitName
                    IssueType  = "WARNING"
                    Issue      = "MinimalOfficerData"
                    Section    = ""
                    MemType    = ""
                    MemberName = ""
                    DataId     = ""
                })
            }
        }

        # Check for missing past masters (WARNING)
        if (-not $unitsWithPastMasters.Contains($unitNo)) {
            Write-Host "  WARN $unitNo  $unitName" -ForegroundColor Yellow
            Write-Host "       No past master data found" -ForegroundColor Yellow
            [void]$issues.Add([PSCustomObject]@{
                Timestamp  = $timestamp
                HtmlFile   = (Split-Path $HtmlFile -Leaf)
                UnitType   = $cfg.UnitType
                UnitNo     = $unitNo
                UnitName   = $unitName
                IssueType  = "WARNING"
                Issue      = "NoPastMasterData"
                Section    = ""
                MemType    = ""
                MemberName = ""
                DataId     = ""
            })
        }

        # Check for missing members (WARNING)
        if (-not $unitsWithMembers.Contains($unitNo)) {
            Write-Host "  WARN $unitNo  $unitName" -ForegroundColor Yellow
            Write-Host "       No member data found" -ForegroundColor Yellow
            [void]$issues.Add([PSCustomObject]@{
                Timestamp  = $timestamp
                HtmlFile   = (Split-Path $HtmlFile -Leaf)
                UnitType   = $cfg.UnitType
                UnitNo     = $unitNo
                UnitName   = $unitName
                IssueType  = "WARNING"
                Issue      = "NoMemberData"
                Section    = ""
                MemType    = ""
                MemberName = ""
                DataId     = ""
            })
        }

        # Check for past masters without years (WARNING)
        if ($pastMasterYearsCount.ContainsKey($unitNo)) {
            $counts = $pastMasterYearsCount[$unitNo]
            if ($counts['total'] -gt 0 -and $counts['withYears'] -eq 0) {
                Write-Host "  WARN $unitNo  $unitName" -ForegroundColor Yellow
                Write-Host "       No past master installation years found" -ForegroundColor Yellow
                [void]$issues.Add([PSCustomObject]@{
                    Timestamp  = $timestamp
                    HtmlFile   = (Split-Path $HtmlFile -Leaf)
                    UnitType   = $cfg.UnitType
                    UnitNo     = $unitNo
                    UnitName   = $unitName
                    IssueType  = "WARNING"
                    Issue      = "NoPastMasterYears"
                    Section    = ""
                    MemType    = ""
                    MemberName = ""
                    DataId     = ""
                })
            }
        }

        # Check for past masters without ranks (WARNING)
        if ($pastMasterRanksCount.ContainsKey($unitNo)) {
            $counts = $pastMasterRanksCount[$unitNo]
            if ($counts['total'] -gt 0 -and $counts['withRanks'] -eq 0) {
                Write-Host "  WARN $unitNo  $unitName" -ForegroundColor Yellow
                Write-Host "       No past master ranks found" -ForegroundColor Yellow
                [void]$issues.Add([PSCustomObject]@{
                    Timestamp  = $timestamp
                    HtmlFile   = (Split-Path $HtmlFile -Leaf)
                    UnitType   = $cfg.UnitType
                    UnitNo     = $unitNo
                    UnitName   = $unitName
                    IssueType  = "WARNING"
                    Issue      = "NoPastMasterRanks"
                    Section    = ""
                    MemType    = ""
                    MemberName = ""
                    DataId     = ""
                })
            }
        }

        # Check for joining past masters without join years (WARNING)
        if ($joiningPastMasterYearsCount.ContainsKey($unitNo)) {
            $counts = $joiningPastMasterYearsCount[$unitNo]
            if ($counts['total'] -gt 0 -and $counts['withYears'] -eq 0) {
                Write-Host "  WARN $unitNo  $unitName" -ForegroundColor Yellow
                Write-Host "       No joining past master join dates found" -ForegroundColor Yellow
                [void]$issues.Add([PSCustomObject]@{
                    Timestamp  = $timestamp
                    HtmlFile   = (Split-Path $HtmlFile -Leaf)
                    UnitType   = $cfg.UnitType
                    UnitNo     = $unitNo
                    UnitName   = $unitName
                    IssueType  = "WARNING"
                    Issue      = "NoJoiningPastMasterYears"
                    Section    = ""
                    MemType    = ""
                    MemberName = ""
                    DataId     = ""
                })
            }
        }

        # Check for joining past masters without ranks (WARNING)
        if ($joiningPastMasterRanksCount.ContainsKey($unitNo)) {
            $counts = $joiningPastMasterRanksCount[$unitNo]
            if ($counts['total'] -gt 0 -and $counts['withRanks'] -eq 0) {
                Write-Host "  WARN $unitNo  $unitName" -ForegroundColor Yellow
                Write-Host "       No joining past master ranks found" -ForegroundColor Yellow
                [void]$issues.Add([PSCustomObject]@{
                    Timestamp  = $timestamp
                    HtmlFile   = (Split-Path $HtmlFile -Leaf)
                    UnitType   = $cfg.UnitType
                    UnitNo     = $unitNo
                    UnitName   = $unitName
                    IssueType  = "WARNING"
                    Issue      = "NoJoiningPastMasterRanks"
                    Section    = ""
                    MemType    = ""
                    MemberName = ""
                    DataId     = ""
                })
            }
        }

        # Check for honorary members without ranks (WARNING)
        if ($honoraryMemberRanksCount.ContainsKey($unitNo)) {
            $counts = $honoraryMemberRanksCount[$unitNo]
            if ($counts['total'] -gt 0 -and $counts['withRanks'] -eq 0) {
                Write-Host "  WARN $unitNo  $unitName" -ForegroundColor Yellow
                Write-Host "       Honorary members have no ranks found" -ForegroundColor Yellow
                [void]$issues.Add([PSCustomObject]@{
                    Timestamp  = $timestamp
                    HtmlFile   = (Split-Path $HtmlFile -Leaf)
                    UnitType   = $cfg.UnitType
                    UnitNo     = $unitNo
                    UnitName   = $unitName
                    IssueType  = "WARNING"
                    Issue      = "NoHonoraryRanks"
                    Section    = ""
                    MemType    = ""
                    MemberName = ""
                    DataId     = ""
                })
            }
        }

        # Check for units with no honorary members (WARNING)
        if (-not $unitsWithHonoraryMembers.Contains($unitNo)) {
            Write-Host "  WARN $unitNo  $unitName" -ForegroundColor Yellow
            Write-Host "       No honorary members found" -ForegroundColor Yellow
            [void]$issues.Add([PSCustomObject]@{
                Timestamp  = $timestamp
                HtmlFile   = (Split-Path $HtmlFile -Leaf)
                UnitType   = $cfg.UnitType
                UnitNo     = $unitNo
                UnitName   = $unitName
                IssueType  = "WARNING"
                Issue      = "NoHonoraryMembers"
                Section    = ""
                MemType    = ""
                MemberName = ""
                DataId     = ""
            })
        }
    }

    # b) Check EVERY row in EVERY CSV section directly.
    #    CSV (filtered by YAML section rules) is the single source of truth.
    #    No per-unit filtering, no skipping - every row must have a matching data-id in HTML.
    #    Track units not in units CSV to report only one warning per unit.
    $unitsNotInCsv = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    
    foreach ($sec in $cfg.MemSections) {
        foreach ($row in $secData[$sec.Name]) {
            $name = $row.($sec.NameColumn).Trim()
            
            # Skip officers where name contains "vacant" (case-insensitive), is empty, or office is empty
            if ($sec.Name -match "officers") {
                $office = if ($sec.PositionColumn) { $row.($sec.PositionColumn).Trim() } else { "" }
                
                # Skip if name is empty, contains "vacant" (any case), or office is empty
                if ([string]::IsNullOrWhiteSpace($name) -or $name -ilike "*vacant*" -or [string]::IsNullOrWhiteSpace($office)) {
                    continue
                }
            }
            
            $dataId = $row.($sec.RefColumn).Trim()

            $typeRowsChecked++

            # Check if any HTML data-id contains this reference (as a substring)
            $found = $htmlDataIds | Where-Object { $_.StartsWith($dataId) } | Select-Object -First 1
            if (-not $found) {
                $name     = $row.($sec.NameColumn).Trim()
                $unitNo   = $row.($sec.UnitIdField).Trim()
                $unitName = if ($unitNameMap.ContainsKey($unitNo)) { $unitNameMap[$unitNo] } else { '(not in units CSV)' }
                $label    = if ($name) { $name } else { '(vacant)' }
                
                # Check if unit is in units CSV
                if ($unitName -eq '(not in units CSV)') {
                    # Only report once per unit
                    if (-not $unitsNotInCsv.Contains($unitNo)) {
                        [void]$unitsNotInCsv.Add($unitNo)
                        Write-Host "  WARN $unitNo $unitName" -ForegroundColor Yellow
                        Write-Host "       Found in membership CSV but not in units CSV" -ForegroundColor Yellow
                        [void]$issues.Add([PSCustomObject]@{
                            Timestamp  = $timestamp
                            HtmlFile   = (Split-Path $HtmlFile -Leaf)
                            UnitType   = $cfg.UnitType
                            UnitNo     = $unitNo
                            UnitName   = $unitName
                            IssueType  = "WARNING"
                            Issue      = "NotInUnitsCsv"
                            Section    = $sec.Name
                            MemType    = ""
                            MemberName = ""
                            DataId     = ""
                        })
                    }
                } else {
                    # Unit is in units CSV but member/officer is missing from HTML
                    $typeFail++
                    Write-Host "  FAIL $unitNo $unitName" -ForegroundColor Yellow
                    Write-Host "       MISSING $label (dataId=$dataId)" -ForegroundColor Red
                    [void]$issues.Add([PSCustomObject]@{
                        Timestamp  = $timestamp
                        HtmlFile   = (Split-Path $HtmlFile -Leaf)
                        UnitType   = $cfg.UnitType
                        UnitNo     = $unitNo
                        UnitName   = $unitName
                        IssueType  = "ERROR"
                        Issue      = "MissingMember"
                        Section    = $sec.Name
                        MemType    = ""
                        MemberName = $name
                        DataId     = $dataId
                    })
                }
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
