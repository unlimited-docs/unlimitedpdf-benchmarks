<#
.SYNOPSIS
    Regenerates report.html, summary.md, results.csv and results.json from the raw data of one or more run folders.
.EXAMPLE
    ./scripts/generate-reports.ps1                       # every folder under results/
    ./scripts/generate-reports.ps1 results/20261124-220000-full
#>
param([string[]] $RunFolder)
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
Push-Location $root
try {
    if (-not $RunFolder) {
        $RunFolder = Get-ChildItem results -Directory -ErrorAction SilentlyContinue | Where-Object { Test-Path (Join-Path $_.FullName 'manifest.json') } | ForEach-Object FullName
    }
    if (-not $RunFolder) { throw 'No run folders found under results/.' }
    dotnet build src/UnlimitedPDF.Benchmarks.Reporting -c Release
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    foreach ($folder in $RunFolder) {
        dotnet run --project src/UnlimitedPDF.Benchmarks.Reporting -c Release --no-build -- $folder
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
}
finally {
    Pop-Location
}
