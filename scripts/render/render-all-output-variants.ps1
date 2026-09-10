# Renders all standard HTML and PDF output variants.
#
# Usage:
#   .\render-all-output-variants.ps1

$rootDir = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$consoleDir = Join-Path $rootDir "src\MasonicCalendar.Console"

if (-not (Test-Path $consoleDir)) {
    Write-Host "ERROR: Console project directory not found: $consoleDir" -ForegroundColor Red
    exit 1
}

$renderCommands = @(
    @("-template", "master_v1", "-output", "html"),
    @("-template", "master_v1", "-output", "pdf"),
    @("-template", "master_v1", "-output", "pdf", "-digital"),
    @("-template", "master_v1", "-output", "pdf", "-showprint"),
    @("-template", "master_v1", "-output", "pdf", "-showbleed", "-showmargins")
)

Push-Location $consoleDir
try {
    foreach ($renderCommand in $renderCommands) {
        Write-Host "dotnet run -- $($renderCommand -join ' ')" -ForegroundColor Cyan
        & dotnet run -- @renderCommand

        if ($LASTEXITCODE -ne 0) {
            Write-Host "ERROR: Render command failed with exit code $LASTEXITCODE" -ForegroundColor Red
            exit $LASTEXITCODE
        }
    }
}
finally {
    Pop-Location
}