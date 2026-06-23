# Renders PDFs for all Royal Arch units from the units CSV file.
# Usage:
#   .\render-all-ra-units.ps1 -Version 1.5
#   .\render-all-ra-units.ps1 -Version 1.5 -Limit 3

param(
    [int]$Limit = 0
)

& "$PSScriptRoot\render-units.ps1" -FilterUnitType "RA" -Limit $Limit
exit $LASTEXITCODE
