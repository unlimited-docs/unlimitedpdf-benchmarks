<#
.SYNOPSIS
    Builds the suite, validates every case, measures the passing cases, profiles process resources and writes the reports.

.EXAMPLE
    ./scripts/run-benchmarks.ps1 -Profile quick
    ./scripts/run-benchmarks.ps1 -Profile standard -Category Manipulation -Library UnlimitedPdf,PdfPig
    ./scripts/run-benchmarks.ps1 -Profile all          # full, then stress, each in its own run folder
    ./scripts/run-benchmarks.ps1 -Profile smoke -- --filter '*TableBenchmarks*'
#>
[CmdletBinding()]
param(
    [ValidateSet('smoke', 'quick', 'standard', 'full', 'stress', 'all')]
    [string] $Profile = 'standard',
    [string] $Category,
    [string[]] $Library,
    [string] $Output,
    [switch] $SkipBuild,
    [switch] $SkipBenchmarks,
    [switch] $SkipProfiler,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $BenchmarkDotNetArgs
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
Push-Location $root
try {
    function Invoke-Checked([string] $File, [string[]] $Arguments) {
        & $File @Arguments
        if ($LASTEXITCODE -ne 0) { throw "$File $($Arguments -join ' ') failed with exit code $LASTEXITCODE" }
    }

    if (-not $SkipBuild) {
        Invoke-Checked dotnet @('restore', 'UnlimitedPDF.Benchmarks.slnx', '--locked-mode')
        Invoke-Checked dotnet @('build', 'UnlimitedPDF.Benchmarks.slnx', '-c', 'Release', '--no-restore')
    }

    $profiles = if ($Profile -eq 'all') { @('full', 'stress') } else { @($Profile) }
    foreach ($p in $profiles) {
        $runId = '{0:yyyyMMdd-HHmmss}-{1}' -f [DateTime]::UtcNow, $p
        $folder = if ($Output -and $profiles.Count -eq 1) { $Output } elseif ($Output) { Join-Path $Output $runId } else { Join-Path 'results' $runId }
        New-Item -ItemType Directory -Force -Path $folder | Out-Null
        Write-Host "== Profile $p -> $folder"

        $common = @('--profile', $p, '--output', $folder)
        if ($Category) { $common += @('--category', $Category) }
        if ($Library) { $common += @('--library', ($Library -join ',')) }

        if (-not $SkipBenchmarks) {
            # Validation and measurement run in separate processes so a timed-out validation cannot disturb measurement.
            Invoke-Checked dotnet (@('run', '--project', 'src/UnlimitedPDF.Benchmarks.Runner', '-c', 'Release', '--no-build', '--', 'validate') + $common)
            Invoke-Checked dotnet (@('run', '--project', 'src/UnlimitedPDF.Benchmarks.Runner', '-c', 'Release', '--no-build', '--', 'bench') + $common + $BenchmarkDotNetArgs)
        }
        if (-not $SkipProfiler) {
            $profilerArgs = @('--profile', $p, '--output', $folder)
            if ($Library) { $profilerArgs += @('--library', ($Library -join ',')) }
            Invoke-Checked dotnet (@('run', '--project', 'src/UnlimitedPDF.Benchmarks.ResourceProfiler', '-c', 'Release', '--no-build', '--') + $profilerArgs)
        }
        Invoke-Checked dotnet @('run', '--project', 'src/UnlimitedPDF.Benchmarks.Reporting', '-c', 'Release', '--no-build', '--', $folder)
        Write-Host "Report: $(Join-Path $folder 'report.html')"
    }
}
finally {
    Pop-Location
}
