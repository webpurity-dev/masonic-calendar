# Renders PDFs for all Ancient and Accepted Rite (Rose Croix) (RC) units from the units CSV file.
# Usage:
#   .\render-all-rc-units.ps1
#   .\render-all-rc-units.ps1 -Limit 3

param(
    [int]$Limit = 0
)

& "$PSScriptRoot\render-units.ps1" -FilterUnitType "RC" -Limit $Limit
exit $LASTEXITCODE
