# Renders PDFs for all Order of the Secret Monitor (OSM) units from the units CSV file.
# Usage:
#   .\render-all-osm-units.ps1
#   .\render-all-osm-units.ps1 -Limit 3

param(
    [int]$Limit = 0
)

& "$PSScriptRoot\render-units.ps1" -FilterUnitType "OSM" -Limit $Limit
exit $LASTEXITCODE
