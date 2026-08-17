[CmdletBinding()]
param(
    [int]$Runs = 5,
    [int]$ReadyTimeoutSeconds = 15,
    [int]$IdleSampleSeconds = 60,
    [int]$ExitAfterSeconds = 75,
    [int]$SaveLatencySamples = 10,
    [long]$MaximumWorkingSetBytes = 200MB,
    [double]$MaximumIdleCpuPercent = 0.5,
    [double]$MaximumSaveLatencyMilliseconds = 300,
    [string]$PayloadRoot,
    [string]$InstallerPath,
    [string]$InstalledRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($Runs -lt 5) { throw 'Release performance validation requires at least 5 runs.' }
if ($ReadyTimeoutSeconds -lt 1) { throw 'ReadyTimeoutSeconds must be positive.' }
if ($IdleSampleSeconds -lt 60) { throw 'IdleSampleSeconds must be at least 60.' }
if ($ExitAfterSeconds -lt ($IdleSampleSeconds + 5)) {
    throw 'ExitAfterSeconds must leave time for idle sampling and shutdown.'
}
if ($SaveLatencySamples -lt 10) {
    throw 'SaveLatencySamples must be at least 10.'
}
if ($MaximumWorkingSetBytes -le 0 -or
    $MaximumIdleCpuPercent -le 0 -or
    $MaximumSaveLatencyMilliseconds -le 0) {
    throw 'Performance gates must be positive.'
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))
$versionsPath = Join-Path $repoRoot 'eng\Versions.props'
[xml]$versions = Get-Content -LiteralPath $versionsPath -Raw
$version = [string]$versions.Project.PropertyGroup.VersionPrefix

if ([string]::IsNullOrWhiteSpace($PayloadRoot)) {
    $PayloadRoot = Join-Path $artifactsRoot 'windows-x64\payload'
}
$payloadPath = [IO.Path]::GetFullPath($PayloadRoot)
$artifactsPrefix = $artifactsRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) +
    [IO.Path]::DirectorySeparatorChar
if (-not $payloadPath.StartsWith($artifactsPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "PayloadRoot must be a child of $artifactsRoot."
}

if ([string]::IsNullOrWhiteSpace($InstallerPath)) {
    $InstallerPath = Join-Path $artifactsRoot "installer\TrainingDeskCalendar-Setup-$version-x64.exe"
}
if ([string]::IsNullOrWhiteSpace($InstalledRoot)) {
    $InstalledRoot = Join-Path $env:LOCALAPPDATA 'Programs\TrainingDeskCalendar'
}

$applicationPath = Join-Path $payloadPath 'TrainingDeskCalendar.App.exe'
if (-not (Test-Path -LiteralPath $applicationPath -PathType Leaf)) {
    throw "Release payload executable was not found at $applicationPath."
}
if (-not (Test-Path -LiteralPath $InstallerPath -PathType Leaf)) {
    throw "Installer was not found at $InstallerPath."
}
if (-not (Test-Path -LiteralPath $InstalledRoot -PathType Container)) {
    throw "Installed directory was not found at $InstalledRoot. Install the release first."
}

function Get-DirectoryBytes([string]$Path) {
    [long]$sum = (Get-ChildItem -LiteralPath $Path -File -Recurse |
        Measure-Object -Property Length -Sum).Sum
    return $sum
}

$payloadFiles = @(Get-ChildItem -LiteralPath $payloadPath -File -Recurse)
$payloadBytes = [long](($payloadFiles | Measure-Object -Property Length -Sum).Sum)
$installerFile = Get-Item -LiteralPath $InstallerPath
$installerBytes = [long]$installerFile.Length
$installedDirectoryBytes = Get-DirectoryBytes $InstalledRoot
$sourceExecutableSha256 = (Get-FileHash -LiteralPath $applicationPath -Algorithm SHA256).Hash.ToLowerInvariant()
$installerSha256 = (Get-FileHash -LiteralPath $InstallerPath -Algorithm SHA256).Hash.ToLowerInvariant()
$gitCommit = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Could not resolve the Git commit for the measurement record.' }

$measurementRoot = Join-Path $artifactsRoot 'release-measurement'
$runsRoot = Join-Path $measurementRoot 'runs'
$resultPath = Join-Path $measurementRoot 'release-performance-results.json'
$reportPath = Join-Path $repoRoot 'docs\validation\release-performance-results.md'
New-Item -ItemType Directory -Path $runsRoot -Force | Out-Null
New-Item -ItemType Directory -Path (Split-Path -Parent $reportPath) -Force | Out-Null

$startupKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$startupName = 'TrainingDeskCalendar'
$startupBefore = Get-ItemProperty -LiteralPath $startupKey -ErrorAction SilentlyContinue
$startupHadValue = $null -ne $startupBefore -and
    $startupBefore.PSObject.Properties.Name -contains $startupName
$startupValue = if ($startupHadValue) { [string]$startupBefore.$startupName } else { $null }
$activeProcess = $null
$activeRunRoot = $null
$measurements = @()

try {
    $existing = Get-Process -Name 'TrainingDeskCalendar.App' -ErrorAction SilentlyContinue
    if ($null -ne $existing) {
        throw 'Close Training Desk Calendar before measuring the release.'
    }

    foreach ($run in 1..$Runs) {
        $runId = [Guid]::NewGuid().ToString('N')
        $activeRunRoot = Join-Path $runsRoot "run-$run-$runId"
        $dataRoot = Join-Path $activeRunRoot 'data'
        $readyFile = Join-Path $activeRunRoot 'ready.txt'
        $latencyFile = Join-Path $activeRunRoot 'save-latency.json'
        New-Item -ItemType Directory -Path $activeRunRoot -Force | Out-Null
        Copy-Item -Path (Join-Path $payloadPath '*') -Destination $activeRunRoot -Recurse -Force

        $runApplicationPath = Join-Path $activeRunRoot 'TrainingDeskCalendar.App.exe'
        $stopwatch = [Diagnostics.Stopwatch]::StartNew()
        $activeProcess = Start-Process -FilePath $runApplicationPath -ArgumentList @(
            '--data-root', $dataRoot,
            '--ready-file', $readyFile,
            '--save-latency-file', $latencyFile,
            '--save-latency-samples', [string]$SaveLatencySamples,
            '--exit-after-seconds', [string]$ExitAfterSeconds
        ) -PassThru

        $readyDeadline = [DateTime]::UtcNow.AddSeconds($ReadyTimeoutSeconds)
        while (-not (Test-Path -LiteralPath $readyFile -PathType Leaf)) {
            $activeProcess.Refresh()
            if ($activeProcess.HasExited) { throw "Run $run exited before ready signal." }
            if ([DateTime]::UtcNow -ge $readyDeadline) { throw "Run $run exceeded ready timeout." }
            Start-Sleep -Milliseconds 10
        }
        $stopwatch.Stop()

        $latencyDeadline = [DateTime]::UtcNow.AddSeconds(10)
        while (-not (Test-Path -LiteralPath $latencyFile -PathType Leaf)) {
            $activeProcess.Refresh()
            if ($activeProcess.HasExited) { throw "Run $run exited before save latency output." }
            if ([DateTime]::UtcNow -ge $latencyDeadline) { throw "Run $run exceeded save probe timeout." }
            Start-Sleep -Milliseconds 10
        }
        $latency = Get-Content -LiteralPath $latencyFile -Raw | ConvertFrom-Json
        if ($latency.sampleCount -ne $SaveLatencySamples -or
            @($latency.samples).Count -ne $SaveLatencySamples) {
            throw "Run $run did not record exactly $SaveLatencySamples save samples."
        }
        $latencySamples = @($latency.samples | ForEach-Object {
            [double]$_.elapsedMilliseconds
        })

        $activeProcess.Refresh()
        $cpuStart = $activeProcess.TotalProcessorTime
        $cpuClock = [Diagnostics.Stopwatch]::StartNew()
        [long]$maximumWorkingSet = 0
        [long]$maximumPrivateBytes = 0
        $idleSamples = 0
        $sampleDeadline = [DateTime]::UtcNow.AddSeconds($IdleSampleSeconds)
        while ([DateTime]::UtcNow -lt $sampleDeadline) {
            $activeProcess.Refresh()
            if ($activeProcess.HasExited) { throw "Run $run exited during idle sampling." }
            $maximumWorkingSet = [Math]::Max($maximumWorkingSet, $activeProcess.WorkingSet64)
            $maximumPrivateBytes = [Math]::Max($maximumPrivateBytes, $activeProcess.PrivateMemorySize64)
            $idleSamples++
            Start-Sleep -Milliseconds 500
        }
        $cpuClock.Stop()
        $activeProcess.Refresh()
        $cpuPercent = (($activeProcess.TotalProcessorTime - $cpuStart).TotalSeconds /
            ($cpuClock.Elapsed.TotalSeconds * [Environment]::ProcessorCount)) * 100

        if (-not $activeProcess.WaitForExit(($ExitAfterSeconds + 15) * 1000)) {
            throw "Run $run did not exit within the allowed time."
        }
        $activeProcess.WaitForExit()
        if ($activeProcess.ExitCode -ne 0) { throw "Run $run exited with code $($activeProcess.ExitCode)." }

        $measurements += [pscustomobject][ordered]@{
            run = $run
            classification = 'fresh-materialized-path'
            startupMilliseconds = [Math]::Round($stopwatch.Elapsed.TotalMilliseconds, 1)
            idleCpuPercent = [Math]::Round($cpuPercent, 3)
            maximumWorkingSetBytes = $maximumWorkingSet
            maximumPrivateBytes = $maximumPrivateBytes
            idleSamples = $idleSamples
            saveLatencyMilliseconds = $latencySamples
            maximumSaveLatencyMilliseconds = [Math]::Round(($latencySamples | Measure-Object -Maximum).Maximum, 1)
            averageSaveLatencyMilliseconds = [Math]::Round(($latencySamples | Measure-Object -Average).Average, 1)
            exitCode = $activeProcess.ExitCode
        }

        $activeProcess.Dispose()
        $activeProcess = $null
        Remove-Item -LiteralPath $activeRunRoot -Recurse -Force
        $activeRunRoot = $null
        Start-Sleep -Milliseconds 500
    }

    $summary = [ordered]@{
        measuredAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        applicationVersion = $version
        osVersion = [Environment]::OSVersion.VersionString
        gitCommit = $gitCommit
        classification = 'fresh-materialized-path'
        sourceExecutableSha256 = $sourceExecutableSha256
        installerSha256 = $installerSha256
        payloadBytes = $payloadBytes
        payloadFileCount = $payloadFiles.Count
        installerBytes = $installerBytes
        installedDirectoryBytes = $installedDirectoryBytes
        parameters = [ordered]@{
            runs = $Runs
            readyTimeoutSeconds = $ReadyTimeoutSeconds
            idleSampleSeconds = $IdleSampleSeconds
            exitAfterSeconds = $ExitAfterSeconds
            saveLatencySamples = $SaveLatencySamples
            maximumWorkingSetBytes = $MaximumWorkingSetBytes
            maximumIdleCpuPercent = $MaximumIdleCpuPercent
            maximumSaveLatencyMilliseconds = $MaximumSaveLatencyMilliseconds
        }
        maximumStartupMilliseconds = [Math]::Round(($measurements.startupMilliseconds | Measure-Object -Maximum).Maximum, 1)
        averageStartupMilliseconds = [Math]::Round(($measurements.startupMilliseconds | Measure-Object -Average).Average, 1)
        maximumIdleCpuPercent = [Math]::Round(($measurements.idleCpuPercent | Measure-Object -Maximum).Maximum, 3)
        maximumWorkingSetBytes = [long](($measurements.maximumWorkingSetBytes | Measure-Object -Maximum).Maximum)
        maximumPrivateBytes = [long](($measurements.maximumPrivateBytes | Measure-Object -Maximum).Maximum)
        maximumSaveLatencyMilliseconds = [Math]::Round(($measurements.maximumSaveLatencyMilliseconds | Measure-Object -Maximum).Maximum, 1)
        measurements = $measurements
    }
    $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resultPath -Encoding utf8

    $startupPass = $summary.maximumStartupMilliseconds -le 2000
    $memoryPass = $summary.maximumWorkingSetBytes -le $MaximumWorkingSetBytes
    $cpuPass = $summary.maximumIdleCpuPercent -lt $MaximumIdleCpuPercent
    $savePass = $summary.maximumSaveLatencyMilliseconds -le $MaximumSaveLatencyMilliseconds
    $payloadPass = $payloadBytes -lt 150MB
    $installerPass = $installerBytes -lt 80MB
    $installedPass = $installedDirectoryBytes -lt 150MB
    $rows = ($measurements | ForEach-Object {
        $memory = [Math]::Round($_.maximumWorkingSetBytes / 1MB, 1)
        "| $($_.run) | $($_.startupMilliseconds) | $($_.idleCpuPercent) | $memory | $($_.maximumSaveLatencyMilliseconds) | $($_.idleSamples) |"
    }) -join [Environment]::NewLine
    $report = @"
# Release Performance Results

- Measured UTC: $($summary.measuredAtUtc)
- Application version: $version
- Classification: fresh-materialized-path
- OS: $($summary.osVersion)
- Git commit: $gitCommit
- Payload: $([Math]::Round($payloadBytes / 1MB, 2)) MiB in $($payloadFiles.Count) files - $(if ($payloadPass) { 'PASS' } else { 'FAIL' })
- Installer: $([Math]::Round($installerBytes / 1MB, 2)) MiB - $(if ($installerPass) { 'PASS' } else { 'FAIL' })
- Installed directory: $([Math]::Round($installedDirectoryBytes / 1MB, 2)) MiB - $(if ($installedPass) { 'PASS' } else { 'FAIL' })
- Maximum startup: $($summary.maximumStartupMilliseconds) ms / 2000 ms - $(if ($startupPass) { 'PASS' } else { 'FAIL' })
- Maximum working set: $([Math]::Round($summary.maximumWorkingSetBytes / 1MB, 1)) MiB / $([Math]::Round($MaximumWorkingSetBytes / 1MB, 1)) MiB - $(if ($memoryPass) { 'PASS' } else { 'FAIL' })
- Maximum idle CPU: $($summary.maximumIdleCpuPercent)% / $MaximumIdleCpuPercent% - $(if ($cpuPass) { 'PASS' } else { 'FAIL' })
- Maximum automatic save latency: $($summary.maximumSaveLatencyMilliseconds) ms / $MaximumSaveLatencyMilliseconds ms - $(if ($savePass) { 'PASS' } else { 'FAIL' })

| Run | Startup ms | Idle CPU % | Max working set MiB | Max save ms | Idle samples |
| ---: | ---: | ---: | ---: | ---: | ---: |
$rows

Raw JSON: artifacts/release-measurement/release-performance-results.json.
"@
    $report | Set-Content -LiteralPath $reportPath -Encoding utf8
    Write-Host $report

    if (-not ($startupPass -and $memoryPass -and $cpuPass -and $savePass -and
              $payloadPass -and $installerPass -and $installedPass)) {
        exit 1
    }
}
finally {
    if ($null -ne $activeProcess) {
        $activeProcess.Refresh()
        if (-not $activeProcess.HasExited) {
            Stop-Process -Id $activeProcess.Id -Force
            $activeProcess.WaitForExit()
        }
        $activeProcess.Dispose()
    }
    if ($null -ne $activeRunRoot -and (Test-Path -LiteralPath $activeRunRoot)) {
        Remove-Item -LiteralPath $activeRunRoot -Recurse -Force
    }
    if ($startupHadValue) {
        New-Item -Path $startupKey -Force | Out-Null
        Set-ItemProperty -LiteralPath $startupKey -Name $startupName -Value $startupValue
    }
    else {
        Remove-ItemProperty -LiteralPath $startupKey -Name $startupName -ErrorAction SilentlyContinue
    }
}
