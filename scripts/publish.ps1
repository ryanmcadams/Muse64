<#
.SYNOPSIS
    Local one-shot: build, test, and publish MuseApp.exe to ./artifacts/publish.

.EXAMPLE
    ./scripts/publish.ps1
    ./scripts/publish.ps1 -SkipTests
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
$sln = Join-Path $root 'Muse64.sln'
$proj = Join-Path $root 'src/MuseApp/MuseApp.csproj'
$out = Join-Path $root 'artifacts/publish'

function Invoke-Dotnet {
    & dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($args -join ' ') failed with exit code $LASTEXITCODE" }
}

Write-Host "==> Restore" -ForegroundColor Cyan
Invoke-Dotnet restore $sln

Write-Host "==> Build ($Configuration)" -ForegroundColor Cyan
Invoke-Dotnet build $sln -c $Configuration --no-restore

if (-not $SkipTests) {
    Write-Host "==> Test" -ForegroundColor Cyan
    Invoke-Dotnet test $sln -c $Configuration --no-build
}

Write-Host "==> Publish -> $out" -ForegroundColor Cyan
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
Invoke-Dotnet publish $proj -c $Configuration -o $out

$exe = Join-Path $out 'MuseApp.exe'
if (-not (Test-Path $exe)) { throw "Publish succeeded but $exe was not found." }

$item = Get-Item $exe
$hash = (Get-FileHash $exe -Algorithm SHA256).Hash.ToLowerInvariant()

Write-Host ""
Write-Host "Published: $($item.FullName)" -ForegroundColor Green
Write-Host ("Size:      {0:N1} MB ({1:N0} bytes)" -f ($item.Length / 1MB), $item.Length)
Write-Host "SHA256:    $hash"
