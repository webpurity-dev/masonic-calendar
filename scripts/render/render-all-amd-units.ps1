# Renders PDFs for all Allied Masonic Degrees (AMD) units from the units CSV file.
# Usage:
#   .\render-all-amd-units.ps1
#   .\render-all-amd-units.ps1 -Limit 3

param(
    [int]$Limit = 0
)

& "$PSScriptRoot\render-units.ps1" -FilterUnitType "AMD" -Limit $Limit
exit $LASTEXITCODE
