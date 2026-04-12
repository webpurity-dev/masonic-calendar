# Renders a PDF for every Craft unit.
# Usage:
#   .\render-all-craft-units.ps1
#   .\render-all-craft-units.ps1 -Limit 3

param([int]$Limit = 0)

& "$PSScriptRoot\render-units.ps1" -DataSourceYaml "craft_data_source.yaml" -Limit $Limit
exit $LASTEXITCODE

