# Renders PDFs for all Societas Rosicruciana in Anglia (SRIA) units from the units CSV file.
# Usage:
#   .\render-all-sria-units.ps1
#   .\render-all-sria-units.ps1 -Limit 3

param(
    [int]$Limit = 0
)

& "$PSScriptRoot\render-units.ps1" -FilterUnitType "SRIA" -Limit $Limit
exit $LASTEXITCODE
