# Renders PDFs for all Royal Ark Mariner units from the units CSV file.
# Usage:
#   .\render-all-ram-units.ps1 -Version 1.5
#   .\render-all-ram-units.ps1 -Version 1.5 -Limit 3

param(
    [int]$Limit = 0
)

& "$PSScriptRoot\render-units.ps1" -FilterUnitType "RAM" -Limit $Limit
exit $LASTEXITCODE
