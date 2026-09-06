# Render all meetings-table sections as HTML.

param(
    [string]$TemplateName = "master_v1",
    [string]$TemplateDir = (Join-Path $PSScriptRoot "..\..\document"),
    [string]$ConsoleDir = (Join-Path $PSScriptRoot "..\..\src\MasonicCalendar.Console"),
    [string]$OutputFolder = "meetings-tables"
)

$yamlPath = Join-Path $TemplateDir "$TemplateName.yaml"
if (-not (Test-Path $yamlPath)) {
    Write-Host "[FAIL] Layout file not found: $yamlPath" -ForegroundColor Red
    exit 1
}

$yamlContent = Get-Content $yamlPath -Raw
$sections = @()

# Split the YAML into section blocks and select meetings-table entries.
$sectionBlocks = $yamlContent -split '(?=\s*- section_id:)' | Where-Object { $_ -match 'section_id:' }
foreach ($block in $sectionBlocks) {
    if ($block -match 'section_id:\s*"([^"]+)"') {
        $sectionId = $matches[1]
        if ($block -match 'type:\s*"meetings-table"') {
            $sections += $sectionId
        }
    }
}

if ($sections.Count -eq 0) {
    Write-Host "[FAIL] No meetings-table sections found in $yamlPath" -ForegroundColor Red
    exit 1
}

Write-Host "[OK] Found $($sections.Count) meetings-table section(s):" -ForegroundColor Green
$sections | ForEach-Object { Write-Host "   - $_" }
Write-Host "[DIR] Output folder: output\$OutputFolder" -ForegroundColor Cyan
Write-Host ""

Push-Location $ConsoleDir
$successCount = 0
$failCount = 0

try {
    foreach ($section in $sections) {
        Write-Host "[*] Rendering section: $section" -ForegroundColor Cyan
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

        & dotnet run -- -template $TemplateName -output pdf -showBleed -showMargin -section $section -outputfolder $OutputFolder 2>&1 | Out-Null

        $stopwatch.Stop()
        if ($LASTEXITCODE -eq 0) {
            Write-Host "   [OK] Success ($($stopwatch.Elapsed.TotalSeconds.ToString('F1'))s)" -ForegroundColor Green
            $successCount++
        }
        else {
            Write-Host "   [FAIL] Failed with exit code $LASTEXITCODE" -ForegroundColor Red
            $failCount++
        }
    }
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "================================" -ForegroundColor Cyan
Write-Host "Render Summary:" -ForegroundColor Cyan
Write-Host "   [OK] Successful: $successCount" -ForegroundColor Green
Write-Host "   [FAIL] Failed: $failCount" -ForegroundColor Red
Write-Host "================================" -ForegroundColor Cyan

if ($failCount -gt 0) {
    exit 1
}