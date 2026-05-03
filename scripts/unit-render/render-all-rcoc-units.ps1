# Renders PDFs for all Royal Ark Mariner units from the units CSV file.
# Usage:
#   .\render-all-ram-units.ps1 -Version 1.5
#   .\render-all-ram-units.ps1 -Version 1.5 -Limit 3

param(
    [Parameter(Mandatory)][string]$Version,
    [int]$Limit = 0
)

& "$PSScriptRoot\render-units.ps1" -Version $Version -FilterUnitType "RCOC" -Limit $Limit
exit $LASTEXITCODE
