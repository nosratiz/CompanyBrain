<#
.SYNOPSIS
    Native AOT publish for DeepRoot.Photino on Windows hosts.
.DESCRIPTION
    Produces self-contained, trimmed, single-binary builds.
    Native AOT can only target the host OS family, so this script
    publishes win-x64 by default. Pass a RID to override.
.EXAMPLE
    ./publish.ps1
    ./publish.ps1 -Rid win-x64
#>
[CmdletBinding()]
param(
    [string]$Rid        = 'win-x64',
    [string]$Project    = 'src/DeepRoot.Photino/DeepRoot.Photino.csproj',
    [string]$OutputRoot = 'artifacts/deeproot',
    [string]$Config     = 'Release'
)

$ErrorActionPreference = 'Stop'

$out = Join-Path $OutputRoot $Rid
Write-Host "▶  Publishing $Rid → $out" -ForegroundColor Cyan

dotnet publish $Project `
    --configuration $Config `
    --runtime $Rid `
    --self-contained true `
    --output $out `
    -p:PublishAot=true `
    -p:OptimizationPreference=Size `
    -p:StripSymbols=true `
    -p:InvariantGlobalization=true `
    -p:DebugType=none `
    -p:DebugSymbols=false

if ($LASTEXITCODE -ne 0) { throw "Publish failed for $Rid" }
Write-Host "✔  $Rid published" -ForegroundColor Green
