# Renders PDFs for all Knights Beneficent of the Holy City (KBHC) units from the units CSV file.
# Usage:
#   .\render-all-kbhc-units.ps1
#   .\render-all-kbhc-units.ps1 -Limit 3

param(
    [int]$Limit = 0
)

& "$PSScriptRoot\render-units.ps1" -FilterUnitType "KBHC" -Limit $Limit
exit $LASTEXITCODE
