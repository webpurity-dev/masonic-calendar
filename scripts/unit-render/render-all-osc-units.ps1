# Renders PDFs for all Order of the Scarlet Cord (OSC) units from the units CSV file.
# Usage:
#   .\render-all-osc-units.ps1
#   .\render-all-osc-units.ps1 -Limit 3

param(
    [int]$Limit = 0
)

& "$PSScriptRoot\render-units.ps1" -FilterUnitType "OSC" -Limit $Limit
exit $LASTEXITCODE
