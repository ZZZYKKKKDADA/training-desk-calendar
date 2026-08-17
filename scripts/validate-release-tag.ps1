[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Tag,
    [string]$VersionsPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ([string]::IsNullOrWhiteSpace($VersionsPath)) {
    $VersionsPath = Join-Path (Split-Path -Parent $PSScriptRoot) 'eng\Versions.props'
}
if ($Tag -notmatch '^v\d+\.\d+\.\d+$') {
    throw 'Release tag must match vMAJOR.MINOR.PATCH.'
}
if (-not (Test-Path -LiteralPath $VersionsPath -PathType Leaf)) {
    throw "Versions.props was not found at $VersionsPath."
}

[xml]$versions = Get-Content -LiteralPath $VersionsPath -Raw
$expected = [string]$versions.Project.PropertyGroup.VersionPrefix
$tagVersion = $Tag.Substring(1)
if ($tagVersion -ne $expected) {
    throw "Release tag $Tag does not match VersionPrefix $expected."
}

Write-Output "Release tag $Tag matches VersionPrefix $expected."
