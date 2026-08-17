[CmdletBinding()]
param(
    [int]$Runs = 5,
    [int]$IdleSeconds = 15
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src\TrainingDeskCalendar.App\TrainingDeskCalendar.App.csproj'
$artifactRoot = Join-Path $repoRoot 'artifacts\prototype'
$frameworkDir = Join-Path $artifactRoot 'framework-dependent'
$selfContainedDir = Join-Path $artifactRoot 'self-contained'
$resultPath = Join-Path $artifactRoot 'results.json'
$reportPath = Join-Path $repoRoot 'docs\validation\desktop-prototype-results.md'

New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $reportPath) | Out-Null

dotnet publish $project --configuration Release --runtime win-x64 --self-contained false --output $frameworkDir
if ($LASTEXITCODE -ne 0) { throw 'Framework-dependent publish failed.' }

dotnet publish $project --configuration Release --runtime win-x64 --self-contained true --output $selfContainedDir
if ($LASTEXITCODE -ne 0) { throw 'Self-contained publish failed.' }

function Get-DirectorySize([string]$Path) {
    return (Get-ChildItem -LiteralPath $Path -File -Recurse | Measure-Object Length -Sum).Sum
}

function Measure-Run([string]$Executable, [int]$RunNumber) {
    $readyFile = Join-Path $artifactRoot "ready-$RunNumber.txt"
    Remove-Item -LiteralPath $readyFile -Force -ErrorAction SilentlyContinue

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $process = Start-Process -FilePath $Executable -ArgumentList @(
        '--ready-file', $readyFile,
        '--exit-after-seconds', ($IdleSeconds + 15)
    ) -PassThru

    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    while (-not (Test-Path -LiteralPath $readyFile)) {
        if ($process.HasExited) { throw "Prototype exited before ready signal on run $RunNumber." }
        if ([DateTime]::UtcNow -ge $deadline) { throw "Ready timeout on run $RunNumber." }
        Start-Sleep -Milliseconds 25
        $process.Refresh()
    }
    $stopwatch.Stop()

    Start-Sleep -Seconds 2
    $process.Refresh()
    $cpuStart = $process.TotalProcessorTime
    Start-Sleep -Seconds $IdleSeconds
    $process.Refresh()
    $cpuEnd = $process.TotalProcessorTime
    $cpuPercent = (($cpuEnd - $cpuStart).TotalSeconds / ($IdleSeconds * [Environment]::ProcessorCount)) * 100

    $measurement = [ordered]@{
        run = $RunNumber
        startupMilliseconds = [Math]::Round($stopwatch.Elapsed.TotalMilliseconds, 1)
        workingSetBytes = $process.WorkingSet64
        idleCpuPercent = [Math]::Round($cpuPercent, 3)
    }

    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
        $process.WaitForExit()
    }

    return [pscustomobject]$measurement
}

$executable = Join-Path $selfContainedDir 'TrainingDeskCalendar.App.exe'
$measurements = 1..$Runs | ForEach-Object { Measure-Run -Executable $executable -RunNumber $_ }

$frameworkZip = Join-Path $artifactRoot 'framework-dependent.zip'
$selfContainedZip = Join-Path $artifactRoot 'self-contained.zip'
Remove-Item -LiteralPath $frameworkZip, $selfContainedZip -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $frameworkDir '*') -DestinationPath $frameworkZip -CompressionLevel Optimal
Compress-Archive -Path (Join-Path $selfContainedDir '*') -DestinationPath $selfContainedZip -CompressionLevel Optimal

$summary = [ordered]@{
    measuredAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    runs = $Runs
    averageStartupMilliseconds = [Math]::Round(($measurements.startupMilliseconds | Measure-Object -Average).Average, 1)
    maximumWorkingSetBytes = ($measurements.workingSetBytes | Measure-Object -Maximum).Maximum
    averageIdleCpuPercent = [Math]::Round(($measurements.idleCpuPercent | Measure-Object -Average).Average, 3)
    frameworkDependentDirectoryBytes = Get-DirectorySize $frameworkDir
    selfContainedDirectoryBytes = Get-DirectorySize $selfContainedDir
    frameworkDependentZipBytes = (Get-Item -LiteralPath $frameworkZip).Length
    selfContainedZipBytes = (Get-Item -LiteralPath $selfContainedZip).Length
    measurements = $measurements
}

$summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $resultPath -Encoding utf8

$memoryMb = [Math]::Round($summary.maximumWorkingSetBytes / 1MB, 1)
$frameworkDirectoryMb = [Math]::Round($summary.frameworkDependentDirectoryBytes / 1MB, 1)
$selfContainedDirectoryMb = [Math]::Round($summary.selfContainedDirectoryBytes / 1MB, 1)
$frameworkZipMb = [Math]::Round($summary.frameworkDependentZipBytes / 1MB, 1)
$selfContainedZipMb = [Math]::Round($summary.selfContainedZipBytes / 1MB, 1)

$startupPass = $summary.averageStartupMilliseconds -le 2000
$memoryPass = $memoryMb -le 100
$cpuPass = $summary.averageIdleCpuPercent -lt 0.5
$selfContainedSizePass = $selfContainedDirectoryMb -le 150 -and $selfContainedZipMb -le 80
$packagingDecision = if ($selfContainedSizePass) {
    'Self-contained win-x64 publish satisfies the prototype size gate.'
} else {
    'Use a framework-dependent app with a per-user .NET Desktop Runtime bootstrapper; validate final installer size in phase 3.'
}

$report = @"
# Desktop Prototype Validation Results

- Measured UTC: $($summary.measuredAtUtc)
- Runs: $Runs
- Average cold startup: $($summary.averageStartupMilliseconds) ms — $(if ($startupPass) { 'PASS' } else { 'FAIL' })
- Maximum working set: $memoryMb MB — $(if ($memoryPass) { 'PASS' } else { 'FAIL' })
- Average idle CPU: $($summary.averageIdleCpuPercent)% — $(if ($cpuPass) { 'PASS' } else { 'FAIL' })
- Framework-dependent directory: $frameworkDirectoryMb MB
- Framework-dependent ZIP: $frameworkZipMb MB
- Self-contained directory: $selfContainedDirectoryMb MB
- Self-contained ZIP: $selfContainedZipMb MB

## Packaging Decision

$packagingDecision

## Automated Gate

Overall automated result: $(if ($startupPass -and $memoryPass -and $cpuPass) { 'PASS' } else { 'FAIL' })
"@

$report | Set-Content -LiteralPath $reportPath -Encoding utf8
Write-Host $report

# Publish with a RID adds a transient lock-file target; normalize it for clean reruns.
dotnet restore $project --force-evaluate | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Failed to normalize the project lock file after measurement.' }

if (-not ($startupPass -and $memoryPass -and $cpuPass)) {
    exit 1
}
