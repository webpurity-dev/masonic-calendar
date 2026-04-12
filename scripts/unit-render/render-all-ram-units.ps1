# Renders a PDF for every RAM unit.
# Usage:
#   .\render-all-ram-units.ps1
#   .\render-all-ram-units.ps1 -Limit 3
#   .\render-all-ram-units.ps1 -Version 1.4

param(
    [int]$Limit = 0,
    [string]$Version = ""
)

& "$PSScriptRoot\render-units.ps1" -DataSourceYaml "ram_data_source.yaml" -Limit $Limit -Version $Version
exit $LASTEXITCODE
