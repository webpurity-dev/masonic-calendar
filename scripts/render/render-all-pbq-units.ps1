# Renders PDFs for all The Operatives - Purbeck Quarries (PBQ) units from the units CSV file.
# Usage:
#   .\render-all-pbq-units.ps1
#   .\render-all-pbq-units.ps1 -Limit 3

param(
    [int]$Limit = 0
)

& "$PSScriptRoot\render-units.ps1" -FilterUnitType "PBQ" -Limit $Limit
exit $LASTEXITCODE
