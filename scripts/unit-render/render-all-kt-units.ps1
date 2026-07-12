# Renders PDFs for all Knights Templar (KT) units from the units CSV file.
# Usage:
#   .\render-all-kt-units.ps1
#   .\render-all-kt-units.ps1 -Limit 3

param(
    [int]$Limit = 0
)

& "$PSScriptRoot\render-units.ps1" -FilterUnitType "KT" -Limit $Limit
exit $LASTEXITCODE
