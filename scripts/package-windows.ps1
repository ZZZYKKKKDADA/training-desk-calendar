[CmdletBinding()]
param(
    [string]$OutputRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))
$versionsPath = Join-Path $repoRoot 'eng\Versions.props'
[xml]$versions = Get-Content -LiteralPath $versionsPath -Raw
$properties = $versions.Project.PropertyGroup
$appVersion = [string]$properties.VersionPrefix
$runtimeIdentifier = [string]$properties.WindowsRuntimeIdentifier

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $payloadPath = Join-Path $repoRoot 'artifacts\windows-x64\payload'
    $outputPath = Split-Path -Parent $payloadPath
}
else {
    $outputPath = [IO.Path]::GetFullPath($OutputRoot)
    $payloadPath = Join-Path $outputPath 'payload'
}

$outputPath = [IO.Path]::GetFullPath($outputPath)
$payloadPath = [IO.Path]::GetFullPath($payloadPath)
$artifactsPrefix = $artifactsRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not $outputPath.StartsWith($artifactsPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputRoot must be a child of $artifactsRoot."
}

if (Test-Path -LiteralPath $payloadPath) {
    Remove-Item -LiteralPath $payloadPath -Recurse -Force
}
New-Item -ItemType Directory -Path $payloadPath -Force | Out-Null

$appProject = Join-Path $repoRoot 'src\TrainingDeskCalendar.App\TrainingDeskCalendar.App.csproj'
& dotnet publish $appProject `
    --configuration Release `
    --runtime $runtimeIdentifier `
    --self-contained true `
    --output $payloadPath `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=false `
    -p:SatelliteResourceLanguages=zh-Hans `
    -p:DebugType=None `
    -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) {
    throw "Application publish failed with exit code $LASTEXITCODE."
}

$applicationPath = Join-Path $payloadPath 'TrainingDeskCalendar.App.exe'
if (-not (Test-Path -LiteralPath $applicationPath)) {
    throw 'The self-contained Training Desk Calendar executable was not produced.'
}

$publishedFiles = @(Get-ChildItem -LiteralPath $payloadPath -File -Recurse)
if ($publishedFiles.Count -ne 1 -or
    $publishedFiles[0].FullName -ne $applicationPath) {
    throw 'The uncompressed single-file publish unexpectedly produced loose runtime assets.'
}

$application = Get-Item -LiteralPath $applicationPath
$manifest = [ordered]@{
    applicationVersion = $appVersion
    runtimeIdentifier = $runtimeIdentifier
    deployment = 'self-contained-single-file-uncompressed'
    payloadBytes = 0
    executableBytes = $application.Length
    executableSha256 = (Get-FileHash -LiteralPath $applicationPath -Algorithm SHA256).Hash.ToLowerInvariant()
}
$manifestPath = Join-Path $payloadPath 'package-manifest.json'
$payloadBytes = $application.Length
$manifestWriteAttempts = 0
while ($payloadBytes -ne $manifest.payloadBytes) {
    $manifest.payloadBytes = $payloadBytes
    $manifest | ConvertTo-Json | Set-Content -LiteralPath $manifestPath -Encoding utf8
    $payloadBytes = (Get-ChildItem -LiteralPath $payloadPath -File -Recurse |
        Measure-Object -Property Length -Sum).Sum
    $manifestWriteAttempts++
    if ($manifestWriteAttempts -gt 5) {
        throw 'Package manifest size did not stabilize.'
    }
}
if ($payloadBytes -ge 150MB) {
    throw "The installed payload is $payloadBytes bytes, exceeding the 150MB limit."
}

Write-Host "Windows payload created at $payloadPath"
