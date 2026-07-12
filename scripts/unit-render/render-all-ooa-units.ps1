# Renders PDFs for all Order of Athelstan (OOA) units from the units CSV file.
# Usage:
#   .\render-all-ooa-units.ps1
#   .\render-all-ooa-units.ps1 -Limit 3

param(
    [int]$Limit = 0
)

& "$PSScriptRoot\render-units.ps1" -FilterUnitType "OOA" -Limit $Limit
exit $LASTEXITCODE
