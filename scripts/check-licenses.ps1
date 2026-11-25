<#
.SYNOPSIS
    License-policy gate: every package resolved in any packages.lock.json must carry an allowed SPDX licence expression
    (read from the restored .nuspec), or be an exact name+version that was reviewed by hand. Run after a locked restore.
    A dependency update that changes licence (for example PDFsharp 6.5.0, AGPL-3.0-only) fails this check.
#>
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$allowed = @('MIT', 'Apache-2.0', 'BSD-2-Clause', 'BSD-3-Clause')
# Packages whose nuspec gives a licence file or URL instead of an expression; each was read and confirmed by hand.
$reviewed = @{
    'commandlineparser/2.9.1'                    = 'MIT (License.md)'
    'microsoft.dotnet.platformabstractions/3.1.6' = 'MIT (LICENSE.TXT)'
    'xunit.abstractions/2.0.3'                   = 'Apache-2.0 (licence URL)'
}
# Version ceilings for packages that changed licence in later releases.
$ceilings = @{ 'pdfsharp' = [version]'6.5.0'; 'pdfsharp-migradoc' = [version]'6.5.0' }

$packagesRoot = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget/packages' }
$resolved = @{}
Get-ChildItem $root -Recurse -Filter packages.lock.json | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } | ForEach-Object {
    $lock = Get-Content $_.FullName -Raw | ConvertFrom-Json
    foreach ($framework in $lock.dependencies.PSObject.Properties) {
        foreach ($package in $framework.Value.PSObject.Properties) {
            if ($package.Value.type -eq 'Project') { continue }
            $resolved["$($package.Name.ToLowerInvariant())/$($package.Value.resolved)"] = $package.Name
        }
    }
}

$failures = @()
foreach ($key in ($resolved.Keys | Sort-Object)) {
    $id, $version = $key -split '/'
    if ($ceilings.ContainsKey($id) -and [version]($version -replace '-.*$', '') -ge $ceilings[$id]) {
        $failures += "$($resolved[$key]) $version is at or above the licence ceiling $($ceilings[$id])."
        continue
    }
    if ($reviewed.ContainsKey($key)) { Write-Host "reviewed  $($resolved[$key]) $version : $($reviewed[$key])"; continue }
    $nuspec = Get-ChildItem (Join-Path $packagesRoot "$id/$version") -Filter *.nuspec -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $nuspec) { $failures += "$($resolved[$key]) $version is not restored; run a locked restore first."; continue }
    [xml] $xml = Get-Content $nuspec.FullName -Raw
    $license = $xml.package.metadata.license
    $expression = if ($license -and $license.type -eq 'expression') { $license.'#text' } else { $null }
    if (-not $expression) { $failures += "$($resolved[$key]) $version has no SPDX licence expression and is not in the reviewed list."; continue }
    $parts = $expression -split '\s+(?:OR|AND)\s+' | ForEach-Object { $_.Trim('(', ')', ' ') }
    if ($expression -match '\sAND\s' -and ($parts | Where-Object { $_ -notin $allowed })) {
        $failures += "$($resolved[$key]) $version is licensed '$expression'."
    }
    elseif (-not ($parts | Where-Object { $_ -in $allowed })) {
        $failures += "$($resolved[$key]) $version is licensed '$expression'."
    }
    else { Write-Host "allowed   $($resolved[$key]) $version : $expression" }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Host "FAIL      $_" -ForegroundColor Red }
    throw "$($failures.Count) package(s) violate the licence policy. Review docs/library-licenses.md before changing it."
}
Write-Host "Licence policy: $($resolved.Count) packages checked, all allowed."
