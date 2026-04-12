# Renders a PDF for every Mark unit.
# Usage:
#   .\render-all-mark-units.ps1
#   .\render-all-mark-units.ps1 -Limit 3

param([int]$Limit = 0)

& "$PSScriptRoot\render-units.ps1" -DataSourceYaml "mark_data_source.yaml" -Limit $Limit
exit $LASTEXITCODE
