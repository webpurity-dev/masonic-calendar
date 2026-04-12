# Renders a PDF for every Mark unit.
# Usage:
#   .\render-all-mark-units.ps1
#   .\render-all-mark-units.ps1 -Limit 3
#   .\render-all-mark-units.ps1 -Version 1.4

param(
    [int]$Limit = 0,
    [string]$Version = ""
)

& "$PSScriptRoot\render-units.ps1" -DataSourceYaml "mark_data_source.yaml" -Limit $Limit -Version $Version
exit $LASTEXITCODE
