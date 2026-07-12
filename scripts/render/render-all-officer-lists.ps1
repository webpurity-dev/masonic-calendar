# Render all officer list sections (sections with template "_data-driven/list-officers.html")
# This script parses master_v1.yaml and renders PDF or HTML for each matching section

param(
    [string]$TemplateDir = "e:\Development\repos\masonic-calendar\document",
    [string]$ConsoleDir = "e:\Development\repos\masonic-calendar\src\MasonicCalendar.Console",
    [ValidateSet("pdf", "html")]
    [string]$OutputFormat = "html"
)

# Load the master_v1.yaml file
$yamlPath = Join-Path $TemplateDir "master_v1.yaml"
$yamlContent = Get-Content $yamlPath -Raw

# Extract all sections with template "_data-driven/list-officers.html"
$sections = @()

# Split by "- section_id:" to find each section
$sectionBlocks = $yamlContent -split '(?=\s*- section_id:)' | Where-Object { $_ -match 'section_id:' }

foreach ($block in $sectionBlocks) {
    # Extract section_id
    if ($block -match 'section_id:\s*"([^"]+)"') {
        $sectionId = $matches[1]
        
        # Check if this section has template "_data-driven/list-officers.html"
        if ($block -match 'template:\s*"_data-driven/list-officers\.html"') {
            $sections += $sectionId
        }
    }
}

if ($sections.Count -eq 0) {
    Write-Host "[FAIL] No matching sections found." -ForegroundColor Red
    exit 1
}

Write-Host "[OK] Found $($sections.Count) officer section(s) to render:" -ForegroundColor Green
$sections | ForEach-Object { Write-Host "   - $_" }
Write-Host "[DIR] Output format: $($OutputFormat.ToUpper())" -ForegroundColor Cyan
Write-Host ""

# Change to console directory
Push-Location $ConsoleDir

$successCount = 0
$failCount = 0

# Render each section
foreach ($section in $sections) {
    Write-Host "[*] Rendering section: $section" -ForegroundColor Cyan
    
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    
    & dotnet run -- -template master_v1 -output $OutputFormat -section $section -outputfolder "officers" 2>&1 | Out-Null
    
    $stopwatch.Stop()
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "   [OK] Success ($($stopwatch.Elapsed.TotalSeconds.ToString('F1'))s)" -ForegroundColor Green
        $successCount++
    } else {
        Write-Host "   [FAIL] Failed with exit code $LASTEXITCODE" -ForegroundColor Red
        $failCount++
    }
}

Pop-Location

Write-Host ""
Write-Host "================================" -ForegroundColor Cyan
Write-Host "Render Summary:" -ForegroundColor Cyan
Write-Host "   [OK] Successful: $successCount" -ForegroundColor Green
Write-Host "   [FAIL] Failed: $failCount" -ForegroundColor Red
Write-Host "================================" -ForegroundColor Cyan

if ($failCount -gt 0) {
    exit 1
}
