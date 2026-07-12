# Renders PDFs for all St Thomas of Acon (STOA) units from the units CSV file.
# Usage:
#   .\render-all-stoa-units.ps1
#   .\render-all-stoa-units.ps1 -Limit 3

param(
    [int]$Limit = 0
)

& "$PSScriptRoot\render-units.ps1" -FilterUnitType "STOA" -Limit $Limit
exit $LASTEXITCODE
