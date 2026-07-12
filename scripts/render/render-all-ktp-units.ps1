# Renders PDFs for all Knight Templar Priests (KTP) units from the units CSV file.
# Usage:
#   .\render-all-ktp-units.ps1
#   .\render-all-ktp-units.ps1 -Limit 3

param(
    [int]$Limit = 0
)

& "$PSScriptRoot\render-units.ps1" -FilterUnitType "KTP" -Limit $Limit
exit $LASTEXITCODE
