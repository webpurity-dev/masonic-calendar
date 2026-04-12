# Renders a PDF for every RAM unit.
# Usage:
#   .\render-all-ram-units.ps1
#   .\render-all-ram-units.ps1 -Limit 3

param([int]$Limit = 0)

& "$PSScriptRoot\render-units.ps1" -DataSourceYaml "ram_data_source.yaml" -Limit $Limit
exit $LASTEXITCODE
