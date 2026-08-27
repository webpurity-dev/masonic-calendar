# Preprocess membership.csv with repeatable, idempotent fixes before splitting.
# Usage:
#   .\prepare-membership-csv.ps1
#   .\prepare-membership-csv.ps1 -Preview
#   .\prepare-membership-csv.ps1 -MembershipFile "../../document/data/membership.csv"
#   .\prepare-membership-csv.ps1 -OutputFile "../../document/data/membership.prepared.csv"

param(
    [string]$MembershipFile = "../../document/data/membership.csv",
    [string]$OutputFile = "",
    [switch]$Preview,
    [switch]$NoBackup
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-FromScriptDir {
    param([string]$Path)

    $scriptDir = Split-Path -Parent (Get-Item $PSCommandPath).FullName
    return [System.IO.Path]::GetFullPath((Join-Path $scriptDir $Path))
}

function Get-UpdatedHeaders {
    param(
        [string[]]$Headers,
        [hashtable]$RenameMap
    )

    $updated = New-Object System.Collections.Generic.List[string]
    foreach ($header in $Headers) {
        if ($RenameMap.ContainsKey($header)) {
            $updated.Add([string]$RenameMap[$header])
        }
        else {
            $updated.Add($header)
        }
    }

    return ,$updated.ToArray()
}

function Rename-RowProperty {
    param(
        [pscustomobject]$Row,
        [string]$OldName,
        [string]$NewName
    )

    $oldProp = $Row.PSObject.Properties[$OldName]
    if ($null -eq $oldProp) {
        return $false
    }

    $newProp = $Row.PSObject.Properties[$NewName]
    if ($null -eq $newProp) {
        $Row | Add-Member -NotePropertyName $NewName -NotePropertyValue $oldProp.Value
        $Row.PSObject.Properties.Remove($OldName)
        return $true
    }

    if ([string]::IsNullOrWhiteSpace([string]$newProp.Value) -and -not [string]::IsNullOrWhiteSpace([string]$oldProp.Value)) {
        $newProp.Value = $oldProp.Value
    }

    $Row.PSObject.Properties.Remove($OldName)
    return $true
}

# Easy-to-extend rule blocks
$headerRenameRules = [ordered]@{
    "Rank Oth Prov" = "Prov Rank Oth Prov"
}
$headerRenameRules[[string][char]0x2020] = "Suffix"

$textReplacementRules = @(
    @{ Old = "Hants. & I. of W."; New = "Hants & IoW" },
    @{ Old = "Northants. & Hunts."; New = "Northants & Hunts" },
    @{ Old = "s.)"; New = "s)" },
    @{ Old = "x.)"; New = "x)" },
    @{ Old = "d.)"; New = "d)" },
    @{ Old = "n.)"; New = "n)" },
    @{ Old = "s. &"; New = "s &" },
    @{ Old = "2000 & 2013"; New = "2000,2013" },
    @{ Old = "2025, 2025"; New = "2025" },
    @{ Old = "1988, 1991, 2007, 2014, 2022"; New = "1988,91,2007,14,22" },
    @{ Old = "2009, 2016, 2017, 2018"; New = "2009,16,17,18" },
    @{ Old = "2007, 2014, 2022, 2023"; New = "2007,14,22,23" },
    @{ Old = "1991, 2015, 2019, 2020"; New = "1991,2015,19,20" },
    @{ Old = "HAWKINS, V D"; New = "Hawkins, V D" }
    @{ Old = "Vacant,"; New = "Vacant" },
    @{ Old = "Comms Officer"; New = "Comms" },
    @{ Old = "CORPUZ QUIJANO, E C"; New = "Corpuz Quijano, E C" },
    @{ Old = "ORDONEZ, D G"; New = "Ordonez, D G" },
    @{ Old = "HAWKINS, V D"; New = "Hawkins, V D" },

)

$membershipPath = Resolve-FromScriptDir -Path $MembershipFile
if (-not (Test-Path $membershipPath)) {
    Write-Error "Membership file not found: $membershipPath"
    exit 1
}

$targetPath = if ([string]::IsNullOrWhiteSpace($OutputFile)) {
    $membershipPath
}
else {
    Resolve-FromScriptDir -Path $OutputFile
}

Write-Host "Input file:  $membershipPath" -ForegroundColor Cyan
Write-Host "Target file: $targetPath" -ForegroundColor Cyan
if ($Preview) {
    Write-Host "Mode: Preview (no files will be changed)" -ForegroundColor Yellow
}

$headerLine = Get-Content -Path $membershipPath -TotalCount 1
if ([string]::IsNullOrWhiteSpace($headerLine)) {
    Write-Error "CSV appears empty: $membershipPath"
    exit 1
}

$originalHeaders = @(
    ($headerLine -split ",") | ForEach-Object { $_.Trim().Trim('"') }
)
$updatedHeaders = Get-UpdatedHeaders -Headers $originalHeaders -RenameMap $headerRenameRules

$rows = @(Import-Csv -Path $membershipPath)
if ($rows.Count -eq 0) {
    Write-Warning "CSV has headers but no data rows. Header normalization will still be applied."
}

$headerRenameCount = 0
foreach ($row in $rows) {
    foreach ($pair in $headerRenameRules.GetEnumerator()) {
        if (Rename-RowProperty -Row $row -OldName $pair.Key -NewName $pair.Value) {
            $headerRenameCount++
        }
    }
}

$replaceCounts = @{}
foreach ($rule in $textReplacementRules) {
    $replaceCounts[$rule.Old] = 0
}

foreach ($row in $rows) {
    foreach ($prop in $row.PSObject.Properties) {
        if ($prop.Value -isnot [string]) {
            continue
        }

        $value = [string]$prop.Value
        foreach ($rule in $textReplacementRules) {
            if ($value.Contains($rule.Old)) {
                $replaceCounts[$rule.Old] += ([regex]::Matches($value, [regex]::Escape($rule.Old))).Count
                $value = $value.Replace($rule.Old, $rule.New)
            }
        }
        $prop.Value = $value
    }
}

$targetedFixCount = 0
$ortegaMatchCount = 0
$ortegaGrandRankFixCount = 0
$ortegaGrDateFixCount = 0
foreach ($row in $rows) {
    
    if (
        $row."Unique Ref" -eq "Craft 1146 Hon 001" -and
        ([string]$row.Name) -like "*Ortega*"
    ) {
        $ortegaMatchCount++

        if ($row."Grand Rank" -ne "Past Grand Master (Spain)") {
            $row."Grand Rank" = "Past Grand Master (Spain)"
            $targetedFixCount++
            $ortegaGrandRankFixCount++
        }

        if ($row."GR Date Accorded" -ne "2000") {
            $row."GR Date Accorded" = "2000"
            $targetedFixCount++
            $ortegaGrDateFixCount++
        }
    }
}

Write-Host "" 
Write-Host "Summary" -ForegroundColor Yellow
Write-Host "  Rows loaded: $($rows.Count)" -ForegroundColor Gray
Write-Host "  Header rename operations applied: $headerRenameCount" -ForegroundColor Gray
foreach ($rule in $textReplacementRules) {
    $count = $replaceCounts[$rule.Old]
    Write-Host ("  Replace '{0}' -> '{1}': {2}" -f $rule.Old, $rule.New, $count) -ForegroundColor Gray
}
Write-Host "  Targeted row fixes applied: $targetedFixCount" -ForegroundColor Gray
Write-Host "  Ortega rule match count: $ortegaMatchCount" -ForegroundColor Gray
Write-Host "  Ortega Grand Rank fixes: $ortegaGrandRankFixCount" -ForegroundColor Gray
Write-Host "  Ortega GR Date Accorded fixes: $ortegaGrDateFixCount" -ForegroundColor Gray

if ($Preview -and $ortegaMatchCount -eq 0) {
    Write-Host "  Ortega confirmation: no matching row found for Unique Ref 'Craft 1146 Hon 001' with Name containing 'Ortega'." -ForegroundColor Yellow
}

if ($Preview) {
    Write-Host "" 
    Write-Host "Preview complete. No file changes were written." -ForegroundColor Green
    exit 0
}

if ($targetPath -eq $membershipPath -and -not $NoBackup) {
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $backupPath = "${membershipPath}.bak-$timestamp"
    Copy-Item -Path $membershipPath -Destination $backupPath -Force
    Write-Host "Backup created: $backupPath" -ForegroundColor Cyan
}

$outputRows = if ($rows.Count -gt 0) {
    $rows | Select-Object -Property $updatedHeaders
}
else {
    @()
}

$outputRows | Export-Csv -Path $targetPath -NoTypeInformation -Encoding UTF8
Write-Host "" 
Write-Host "Done. Updated CSV written to: $targetPath" -ForegroundColor Green
