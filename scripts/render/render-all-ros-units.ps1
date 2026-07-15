# Renders PDFs for all Royal Order of Scotland (ROS) units from the units CSV file.
# Usage:
#   .\render-all-ros-units.ps1
#   .\render-all-ros-units.ps1 -Limit 3

param(
    [int]$Limit = 0
)

& "$PSScriptRoot\render-units.ps1" -FilterUnitType "ROS" -Limit $Limit
exit $LASTEXITCODE
