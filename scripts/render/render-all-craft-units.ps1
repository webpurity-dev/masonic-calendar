# Renders PDFs for all Craft units from the units CSV file.
# Usage:
#   .\render-all-craft-units.ps1 -Version 1.5
#   .\render-all-craft-units.ps1 -Version 1.5 -Limit 3

param(
    [int]$Limit = 0
)

& "$PSScriptRoot\render-units.ps1" -FilterUnitType "Craft" -Limit $Limit
exit $LASTEXITCODE

