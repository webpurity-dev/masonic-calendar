# Renders PDFs for all Craft units from the units CSV file.
# Usage:
#   .\render-all-craft-units.ps1 -Version 1.5
#   .\render-all-craft-units.ps1 -Version 1.5 -Limit 3

param(
    [Parameter(Mandatory)][string]$Version,
    [int]$Limit = 0
)

& "$PSScriptRoot\render-units.ps1" -Version $Version -FilterUnitType "Craft" -Limit $Limit
exit $LASTEXITCODE

