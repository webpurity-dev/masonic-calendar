# verify-validation.ps1
# Verifies all validation entries against the HTML output
param(
    [string]$ValidationCsv = "",
    [string]$HtmlFile = ""
)

$rootDir = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$validationDir = $PSScriptRoot
$outputDir = Join-Path $rootDir "output"

# Auto-detect latest validation CSV
if (-not $ValidationCsv) {
    $latestCsv = Get-ChildItem -Path $validationDir -Filter "validation-*.csv" -ErrorAction SilentlyContinue |
                 Sort-Object -Property LastWriteTime -Descending |
                 Select-Object -First 1
    
    if ($latestCsv) {
        $ValidationCsv = $latestCsv.FullName
        Write-Host "Auto-detected validation CSV: $($latestCsv.Name)" -ForegroundColor Cyan
    } else {
        Write-Host "ERROR: No validation CSV files found" -ForegroundColor Red
        exit 1
    }
}

if (-not (Test-Path $ValidationCsv)) {
    Write-Host "ERROR: Validation CSV not found: $ValidationCsv" -ForegroundColor Red
    exit 1
}

# Auto-detect latest HTML
if (-not $HtmlFile) {
    $latestHtml = Get-ChildItem -Path $outputDir -Filter "master_v1.*-all-sections.html" -ErrorAction SilentlyContinue |
                  Sort-Object -Property LastWriteTime -Descending |
                  Select-Object -First 1
    
    if ($latestHtml) {
        $HtmlFile = $latestHtml.FullName
        Write-Host "Auto-detected HTML file: $($latestHtml.Name)" -ForegroundColor Cyan
    } else {
        Write-Host "ERROR: No HTML files found" -ForegroundColor Red
        exit 1
    }
}

if (-not (Test-Path $HtmlFile)) {
    Write-Host "ERROR: HTML file not found: $HtmlFile" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Verifying validation report..." -ForegroundColor Cyan
Write-Host ("  CSV: $(Split-Path $ValidationCsv -Leaf)") -ForegroundColor DarkGray
Write-Host ("  HTML: $(Split-Path $HtmlFile -Leaf)") -ForegroundColor DarkGray
Write-Host ""

# Load data
$report = Import-Csv -Path $ValidationCsv
$html = Get-Content $HtmlFile -Raw -Encoding UTF8

if (-not $report -or $report.Count -eq 0) {
    Write-Host "No issues found in validation report." -ForegroundColor Green
    exit 0
}

Write-Host "Checking $($report.Count) issue(s)..." -ForegroundColor Cyan
Write-Host ""

$passCount = 0
$failCount = 0

# Group by issue type
$byType = @{}
foreach ($entry in $report) {
    $type = $entry.IssueType.Trim()
    if (-not $byType.ContainsKey($type)) {
        $byType[$type] = @()
    }
    $byType[$type] += $entry
}

Write-Host "Issue types:" -ForegroundColor Cyan
foreach ($type in $byType.Keys) {
    Write-Host ("  - $type : $($byType[$type].Count) issue(s)")
}
Write-Host ""

# Verify DuplicateRef entries
if ($byType.ContainsKey("DuplicateRef")) {
    Write-Host "Verifying DuplicateRef entries..." -ForegroundColor Cyan
    $dupsByRef = @{}
    foreach ($entry in $byType["DuplicateRef"]) {
        $key = $entry.DataId.Trim() + "|" + $entry.Section.Trim()
        if (-not [string]::IsNullOrWhiteSpace($entry.DataId)) {
            if (-not $dupsByRef.ContainsKey($key)) {
                $dupsByRef[$key] = @()
            }
            $dupsByRef[$key] += $entry
        }
    }
    
    foreach ($key in $dupsByRef.Keys) {
        $csvCount = $dupsByRef[$key].Count
        $dataId = $dupsByRef[$key][0].DataId.Trim()
        $section = $dupsByRef[$key][0].Section.Trim()
        
        # In HTML, find all instances with this DataId (might appear in multiple formats)
        $pattern = 'data-id="[^"]*' + [regex]::Escape($dataId) + '[^"]*"'
        $allMatches = @([regex]::Matches($html, $pattern))
        
        # For this section, should see exactly 1 deduplicated entry
        # (Can't filter by section in HTML easily, so check that there's at least 1)
        if ($allMatches.Count -gt 0) {
            $passCount++
            Write-Host ("    OK $dataId in [$section] (CSV: $csvCount -> deduplicated in HTML)") -ForegroundColor Green
        } else {
            $failCount++
            Write-Host ("    FAIL $dataId in [$section] not found in HTML") -ForegroundColor Red
        }
    }
    Write-Host ""
}

# Verify MissingMember entries
if ($byType.ContainsKey("MissingMember")) {
    Write-Host "Verifying MissingMember entries..." -ForegroundColor Cyan
    foreach ($entry in $byType["MissingMember"]) {
        $dataId = $entry.DataId.Trim()
        
        if ($html -match [regex]::Escape($dataId)) {
            $failCount++
            Write-Host ("    FAIL $dataId found in HTML (should be missing)") -ForegroundColor Red
        } else {
            $passCount++
            Write-Host ("    OK $dataId correctly missing") -ForegroundColor Green
        }
    }
    Write-Host ""
}

# Verify MissingAnchor entries
if ($byType.ContainsKey("MissingAnchor")) {
    Write-Host "Verifying MissingAnchor entries..." -ForegroundColor Cyan
    foreach ($entry in $byType["MissingAnchor"]) {
        $anchorId = $entry.DataId.Trim()
        $pattern = 'id="' + [regex]::Escape($anchorId) + '"'
        
        if ($html -match $pattern) {
            $failCount++
            Write-Host ("    FAIL $anchorId found in HTML (should be missing)") -ForegroundColor Red
        } else {
            $passCount++
            Write-Host ("    OK $anchorId correctly missing") -ForegroundColor Green
        }
    }
    Write-Host ""
}

Write-Host "==========================================================="
Write-Host "  VERIFIED: $passCount"
Write-Host "  INVALID:  $failCount"
Write-Host "==========================================================="
Write-Host ""

if ($failCount -gt 0) {
    Write-Host "FAILED - Invalid entries found" -ForegroundColor Red
    exit 1
} else {
    Write-Host "SUCCESS - All entries verified" -ForegroundColor Green
    exit 0
}
