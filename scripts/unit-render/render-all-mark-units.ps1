# Renders PDFs for all Mark units from the units CSV file.
# Usage:
#   .\render-all-mark-units.ps1 -Version 1.5
#   .\render-all-mark-units.ps1 -Version 1.5 -Limit 3

param(
    [Parameter(Mandatory)][string]$Version,
    [int]$Limit = 0
)

& "$PSScriptRoot\render-units.ps1" -Version $Version -FilterUnitType "Mark" -Limit $Limit
exit $LASTEXITCODE
