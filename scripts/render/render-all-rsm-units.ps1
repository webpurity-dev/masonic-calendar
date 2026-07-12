# Renders PDFs for all Royal and Select Masters (RSM) units from the units CSV file.
# Usage:
#   .\render-all-rsm-units.ps1
#   .\render-all-rsm-units.ps1 -Limit 3

param(
    [int]$Limit = 0
)

& "$PSScriptRoot\render-units.ps1" -FilterUnitType "RSM" -Limit $Limit
exit $LASTEXITCODE
