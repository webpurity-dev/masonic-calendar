# Split membership.csv by Unit Type into separate CSV files
# Usage: .\split-membership-by-order.ps1
#        .\split-membership-by-order.ps1 -Types Craft,royalarch

param(
    [string]$MembershipFile = "../../document/data/membership.csv",
    [string]$OutputFolder = "../../document/data",
    [string]$DataSourcesFolder = "../../document/data_sources",
    [string[]]$Types = @()
)

function Get-NormalizedUnitTypeName {
    param([string]$Value)

    return ($Value -replace '[^a-zA-Z0-9]', '').ToLower()
}

# Resolve paths to absolute
$scriptDir = Split-Path -Parent (Get-Item $PSCommandPath).FullName
$membershipPath = Join-Path $scriptDir $MembershipFile
$outputPath = Join-Path $scriptDir $OutputFolder
$dataSourcesPath = Join-Path $scriptDir $DataSourcesFolder

# Validate input file exists
if (-not (Test-Path $membershipPath)) {
    Write-Error "Membership file not found: $membershipPath"
    exit 1
}

# Validate output folder exists
if (-not (Test-Path $outputPath)) {
    Write-Error "Output folder not found: $outputPath"
    exit 1
}

Write-Host "Reading membership.csv from: $membershipPath" -ForegroundColor Cyan
Write-Host "Output folder: $outputPath" -ForegroundColor Cyan

try {
    $headerLine = Get-Content -Path $membershipPath -TotalCount 1
    if ([string]::IsNullOrWhiteSpace($headerLine)) {
        throw "Membership CSV appears empty: $membershipPath"
    }

    $sourceHeaders = @(
        ($headerLine -split ",") | ForEach-Object { $_.Trim().Trim('"') }
    )

    # Read all data
    $csv = Import-Csv -Path $membershipPath
    Write-Host "Total rows: $($csv.Count)" -ForegroundColor Yellow
    
    $requestedTypes = @($Types | Where-Object { $_ -and $_.Trim() } | ForEach-Object { Get-NormalizedUnitTypeName $_ })
    $requestedFilterValues = @($requestedTypes)

    if ($requestedTypes.Count -gt 0 -and (Test-Path $dataSourcesPath)) {
        Get-ChildItem -Path $dataSourcesPath -Filter "*_data_source.yaml" | ForEach-Object {
            $dataSourceName = Get-NormalizedUnitTypeName ($_.BaseName -replace '_data_source$', '')
            if ($requestedTypes -contains $dataSourceName) {
                $content = Get-Content -Path $_.FullName -Raw
                $filterMatches = [regex]::Matches($content, 'filter_value:\s*"?([^"\r\n]+)"?')
                foreach ($match in $filterMatches) {
                    $requestedFilterValues += Get-NormalizedUnitTypeName $match.Groups[1].Value.Trim()
                }
            }
        }

        $requestedFilterValues = @($requestedFilterValues | Select-Object -Unique)
    }
    
    # Get unique Unit Types
    $unitTypes = $csv | Select-Object -ExpandProperty "Unit Type" -Unique | Where-Object { $_ -and $_.Trim() } | Sort-Object
    if ($requestedTypes.Count -gt 0) {
        $unitTypes = $unitTypes | Where-Object { $requestedFilterValues -contains (Get-NormalizedUnitTypeName $_) }
        Write-Host "Filtering to requested unit type(s): $($Types -join ', ')" -ForegroundColor Yellow
    }

    Write-Host "Found $($unitTypes.Count) unit type(s): $($unitTypes -join ', ')" -ForegroundColor Yellow
    
    # Split and write each unit type to its own file
    foreach ($unitType in $unitTypes) {
        $filtered = $csv | Where-Object { $_."Unit Type" -eq $unitType }
        $rowCount = @($filtered).Count
        
        # Standardize unit type name for filename (lowercase, no special chars)
        $fileName = Get-NormalizedUnitTypeName $unitType
        $outputFile = Join-Path $outputPath "${fileName}_membership.csv"
        
        # Export to CSV
        $filtered |
            Select-Object -Property $sourceHeaders |
            Export-Csv -Path $outputFile -NoTypeInformation -Encoding UTF8
        Write-Host "[OK] $unitType ($rowCount rows) -> ${fileName}_membership.csv" -ForegroundColor Green
    }
    
    Write-Host "`nSplit complete!" -ForegroundColor Green
    Write-Host "New files created in: $outputPath" -ForegroundColor Cyan
}
catch {
    Write-Error "Error during split: $($_.Exception.Message)"
    exit 1
}
