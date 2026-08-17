[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string[]]$Path,
    [Parameter(Mandatory = $true)]
    [string]$OutputPath,
    [string]$JsonOutputPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$records = foreach ($filePath in $Path) {
    if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) {
        throw "Release asset was not found at $filePath."
    }
    $file = Get-Item -LiteralPath $filePath
    $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    [pscustomobject][ordered]@{
        file = $file.Name
        path = $file.FullName
        bytes = $file.Length
        sha256 = $hash
    }
}

New-Item -ItemType Directory -Path (Split-Path -Parent $OutputPath) -Force | Out-Null
$lines = $records | ForEach-Object { "$($_.sha256)  $($_.file)" }
$lines | Set-Content -LiteralPath $OutputPath -Encoding utf8
if ([string]::IsNullOrWhiteSpace($JsonOutputPath)) {
    $JsonOutputPath = [IO.Path]::ChangeExtension($OutputPath, '.json')
}
New-Item -ItemType Directory -Path (Split-Path -Parent $JsonOutputPath) -Force | Out-Null
$records | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $JsonOutputPath -Encoding utf8
Write-Output "Wrote checksum output to $OutputPath"
