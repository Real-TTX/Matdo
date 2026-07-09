<#
.SYNOPSIS
    Baut das Matdo-Docker-Image mit passender Versionsnummer und deployt den Stack.

.DESCRIPTION
    Versionsschema:
      - release : <major>.<minor>.<build>-<builddate>   z.B. 1.0.7-20260710
      - nightly : nightly-<build>-<builddate>            z.B. nightly-7-20260710
      - local   : local-<builddate>                      z.B. local-20260710

    Die Build-Nummer wird bei jedem Aufruf (außer 'local') hochgezählt
    und in build/buildnumber.txt persistiert.

.PARAMETER Channel
    local (Standard) | nightly | release

.PARAMETER NoDeploy
    Nur bauen, nicht deployen.

.EXAMPLE
    ./build.ps1                      # lokaler Build + Deploy (Port 6006)
    ./build.ps1 -Channel nightly
    ./build.ps1 -Channel release
#>
[CmdletBinding()]
param(
    [ValidateSet('local', 'nightly', 'release')]
    [string]$Channel = 'local',
    [switch]$NoDeploy
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$buildDate = Get-Date -Format 'yyyyMMdd'

# --- Version berechnen ---
$version = ''
if ($Channel -eq 'local') {
    $version = "local-$buildDate"
}
else {
    $buildFile = Join-Path $root 'build/buildnumber.txt'
    $build = [int](Get-Content $buildFile -Raw).Trim() + 1
    Set-Content -Path $buildFile -Value $build -Encoding utf8

    if ($Channel -eq 'nightly') {
        $version = "nightly-$build-$buildDate"
    }
    else {
        $mm = (Get-Content (Join-Path $root 'build/version.txt') -Raw).Trim()
        $version = "$mm.$build-$buildDate"
    }
}

Write-Host "==> Matdo Build" -ForegroundColor Cyan
Write-Host "    Channel : $Channel"
Write-Host "    Version : $version"

$env:MATDO_VERSION = $version
if ($Channel -eq 'release') { $env:ASPNETCORE_ENVIRONMENT = 'Production' } else { $env:ASPNETCORE_ENVIRONMENT = 'Development' }

# --- Bauen + Deployen ---
Push-Location $root
try {
    if ($NoDeploy) {
        docker compose build
        if ($LASTEXITCODE -ne 0) { throw "docker compose build fehlgeschlagen." }
        Write-Host "==> Image gebaut: matdo:$version" -ForegroundColor Green
    }
    else {
        if ($Channel -eq 'release') {
            docker compose -f docker-compose.yml -f docker-compose.release.yml up -d --build
        }
        else {
            docker compose up -d --build
        }
        if ($LASTEXITCODE -ne 0) { throw "docker compose up fehlgeschlagen." }
        Write-Host "==> Stack läuft: http://localhost:6006  (Version $version)" -ForegroundColor Green
    }
}
finally {
    Pop-Location
}
