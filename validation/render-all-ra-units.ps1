# Renders a PDF for every Royal Arch unit.
# Usage:
#   .\render-all-ra-units.ps1
#   .\render-all-ra-units.ps1 -Limit 3

param([int]$Limit = 0)

& "$PSScriptRoot\render-units.ps1" -DataSourceYaml "royalarch_data_source.yaml" -Limit $Limit
exit $LASTEXITCODE
