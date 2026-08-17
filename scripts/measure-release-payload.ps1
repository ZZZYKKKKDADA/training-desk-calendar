[CmdletBinding()]
param(
    [int]$Runs = 5,
    [int]$ReadyTimeoutSeconds = 10,
    [int]$IdleSampleSeconds = 15,
    [int]$ExitAfterSeconds = 20,
    [string]$PayloadRoot,
    [long]$MaximumWorkingSetBytes = 200MB
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($Runs -lt 5) {
    throw 'Release payload validation requires at least 5 runs.'
}
if ($ReadyTimeoutSeconds -lt 1 -or $IdleSampleSeconds -lt 15) {
    throw 'ReadyTimeoutSeconds must be positive and IdleSampleSeconds must be at least 15.'
}
if ($ExitAfterSeconds -lt ($IdleSampleSeconds + 2)) {
    throw 'ExitAfterSeconds must leave time for idle sampling.'
}
if ($MaximumWorkingSetBytes -lt 1) {
    throw 'MaximumWorkingSetBytes must be positive.'
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))
if ([string]::IsNullOrWhiteSpace($PayloadRoot)) {
    $PayloadRoot = Join-Path $artifactsRoot 'windows-x64\payload'
}

$payloadPath = [IO.Path]::GetFullPath($PayloadRoot)
$artifactsPrefix = $artifactsRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) +
    [IO.Path]::DirectorySeparatorChar
if (-not $payloadPath.StartsWith($artifactsPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "PayloadRoot must be a child of $artifactsRoot."
}

$sourceApplicationPath = Join-Path $payloadPath 'TrainingDeskCalendar.App.exe'
if (-not (Test-Path -LiteralPath $sourceApplicationPath -PathType Leaf)) {
    throw "Release payload executable was not found at $sourceApplicationPath."
}

$payloadFiles = @(Get-ChildItem -LiteralPath $payloadPath -File -Recurse)
$payloadBytes = ($payloadFiles | Measure-Object -Property Length -Sum).Sum
$payloadFileCount = $payloadFiles.Count
$sourceExecutableSha256 = (Get-FileHash `
    -LiteralPath $sourceApplicationPath `
    -Algorithm SHA256).Hash.ToLowerInvariant()
$gitCommit = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) {
    throw 'Could not resolve the Git commit for the measurement record.'
}
$osVersion = [Environment]::OSVersion.VersionString

$measurementRoot = Join-Path $artifactsRoot 'phase3a-payload-measurement'
$runsRoot = Join-Path $measurementRoot 'runs'
$resultPath = Join-Path $measurementRoot 'phase3a-payload-results.json'
$reportPath = Join-Path $repoRoot 'docs\validation\phase3a-payload-results.md'
New-Item -ItemType Directory -Path $runsRoot -Force | Out-Null
New-Item -ItemType Directory -Path (Split-Path -Parent $reportPath) -Force | Out-Null

$startupKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$startupName = 'TrainingDeskCalendar'
$startupValue = Get-ItemPropertyValue `
    -LiteralPath $startupKey `
    -Name $startupName `
    -ErrorAction SilentlyContinue
$hadStartupValue = $null -ne $startupValue
$activeProcess = $null
$runPayloadPath = $null
$measurements = @()

try {
    $existing = Get-Process -Name 'TrainingDeskCalendar.App' -ErrorAction SilentlyContinue
    if ($null -ne $existing) {
        throw 'Close Training Desk Calendar before measuring the release payload.'
    }

    foreach ($run in 1..$Runs) {
        $runId = [Guid]::NewGuid().ToString('N')
        $runPayloadPath = Join-Path $runsRoot "run-$run-$runId"
        New-Item -ItemType Directory -Path $runPayloadPath | Out-Null
        Copy-Item `
            -Path (Join-Path $payloadPath '*') `
            -Destination $runPayloadPath `
            -Recurse `
            -Force

        $applicationPath = Join-Path $runPayloadPath 'TrainingDeskCalendar.App.exe'
        $readyFile = Join-Path $measurementRoot "ready-$run-$runId.txt"
        $stopwatch = [Diagnostics.Stopwatch]::StartNew()
        $activeProcess = Start-Process `
            -FilePath $applicationPath `
            -ArgumentList @(
                '--ready-file', $readyFile,
                '--exit-after-seconds', [string]$ExitAfterSeconds
            ) `
            -PassThru

        $readyDeadline = [DateTime]::UtcNow.AddSeconds($ReadyTimeoutSeconds)
        while (-not (Test-Path -LiteralPath $readyFile -PathType Leaf)) {
            $activeProcess.Refresh()
            if ($activeProcess.HasExited) {
                throw "Run $run exited before creating its ready signal."
            }
            if ([DateTime]::UtcNow -ge $readyDeadline) {
                throw "Run $run exceeded the ready timeout."
            }
            Start-Sleep -Milliseconds 10
        }
        $stopwatch.Stop()
        $activeProcess.Refresh()
        $processorMillisecondsAtReady = [Math]::Round(
            $activeProcess.TotalProcessorTime.TotalMilliseconds,
            1)

        $maximumWorkingSet = 0L
        $maximumPrivateBytes = 0L
        $sampleCount = 0
        $sampleDeadline = [DateTime]::UtcNow.AddSeconds($IdleSampleSeconds)
        while ([DateTime]::UtcNow -lt $sampleDeadline) {
            $activeProcess.Refresh()
            if ($activeProcess.HasExited) {
                throw "Run $run exited before idle sampling completed."
            }
            $maximumWorkingSet = [Math]::Max(
                $maximumWorkingSet,
                $activeProcess.WorkingSet64)
            $maximumPrivateBytes = [Math]::Max(
                $maximumPrivateBytes,
                $activeProcess.PrivateMemorySize64)
            $sampleCount++
            Start-Sleep -Milliseconds 500
        }

        if (-not $activeProcess.WaitForExit(($ExitAfterSeconds + 10) * 1000)) {
            throw "Run $run did not exit within the allowed time."
        }
        $activeProcess.WaitForExit()
        if ($activeProcess.ExitCode -ne 0) {
            throw "Run $run exited with code $($activeProcess.ExitCode)."
        }

        $measurements += [pscustomobject][ordered]@{
            run = $run
            classification = 'fresh-materialized-path'
            startupMilliseconds = [Math]::Round($stopwatch.Elapsed.TotalMilliseconds, 1)
            processorMillisecondsAtReady = $processorMillisecondsAtReady
            maximumWorkingSetBytes = $maximumWorkingSet
            maximumPrivateBytes = $maximumPrivateBytes
            idleSamples = $sampleCount
            exitCode = $activeProcess.ExitCode
        }
        $activeProcess.Dispose()
        $activeProcess = $null
        Remove-Item -LiteralPath $readyFile -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $runPayloadPath -Recurse -Force
        $runPayloadPath = $null
        Start-Sleep -Milliseconds 500
    }

    $summary = [ordered]@{
        measuredAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        classification = 'fresh-materialized-path'
        osVersion = $osVersion
        gitCommit = $gitCommit
        sourceExecutableSha256 = $sourceExecutableSha256
        payloadBytes = $payloadBytes
        payloadFileCount = $payloadFileCount
        parameters = [ordered]@{
            runs = $Runs
            readyTimeoutSeconds = $ReadyTimeoutSeconds
            idleSampleSeconds = $IdleSampleSeconds
            exitAfterSeconds = $ExitAfterSeconds
            maximumWorkingSetBytes = $MaximumWorkingSetBytes
        }
        maximumStartupMilliseconds = ($measurements.startupMilliseconds |
            Measure-Object -Maximum).Maximum
        averageStartupMilliseconds = [Math]::Round(
            ($measurements.startupMilliseconds | Measure-Object -Average).Average,
            1)
        maximumWorkingSetBytes = ($measurements.maximumWorkingSetBytes |
            Measure-Object -Maximum).Maximum
        maximumPrivateBytes = ($measurements.maximumPrivateBytes |
            Measure-Object -Maximum).Maximum
        measurements = $measurements
    }
    $summary | ConvertTo-Json -Depth 6 |
        Set-Content -LiteralPath $resultPath -Encoding utf8

    $startupPass = $summary.maximumStartupMilliseconds -le 2000
    $memoryPass = $summary.maximumWorkingSetBytes -le $MaximumWorkingSetBytes
    $memoryMb = [Math]::Round($summary.maximumWorkingSetBytes / 1MB, 1)
    $memoryLimitMb = [Math]::Round($MaximumWorkingSetBytes / 1MB, 1)
    $privateMb = [Math]::Round($summary.maximumPrivateBytes / 1MB, 1)
    $payloadMb = [Math]::Round($payloadBytes / 1MB, 2)
    $measurementRows = ($measurements | ForEach-Object {
        $workingSetMb = [Math]::Round($_.maximumWorkingSetBytes / 1MB, 1)
        $runPrivateMb = [Math]::Round($_.maximumPrivateBytes / 1MB, 1)
        "| $($_.run) | $($_.startupMilliseconds) | $($_.processorMillisecondsAtReady) | $workingSetMb | $runPrivateMb | $($_.idleSamples) |"
    }) -join [Environment]::NewLine
    $report = @"
# Phase 3A Release Payload Results

- Measured UTC: $($summary.measuredAtUtc)
- Classification: fresh-materialized-path
- OS: $osVersion
- Git commit: $gitCommit
- Source EXE SHA-256: $sourceExecutableSha256
- Payload: $payloadMb MB in $payloadFileCount files
- Parameters: runs=$Runs, ready timeout=$ReadyTimeoutSeconds s, idle sample=$IdleSampleSeconds s, exit after=$ExitAfterSeconds s
- Maximum startup: $($summary.maximumStartupMilliseconds) ms - $(if ($startupPass) { 'PASS' } else { 'FAIL' })
- Average startup: $($summary.averageStartupMilliseconds) ms
- Maximum working set: $memoryMb MiB / $memoryLimitMb MiB - $(if ($memoryPass) { 'PASS' } else { 'FAIL' })
- Maximum private bytes: $privateMb MB

| Run | Startup ms | CPU at ready ms | Max working set MB | Max private MB | Idle samples |
| ---: | ---: | ---: | ---: | ---: | ---: |
$measurementRows

The machine-readable results are stored in artifacts/phase3a-payload-measurement/phase3a-payload-results.json.
"@
    $report | Set-Content -LiteralPath $reportPath -Encoding utf8
    Write-Host $report

    if (-not ($startupPass -and $memoryPass)) {
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
    if ($null -ne $runPayloadPath -and (Test-Path -LiteralPath $runPayloadPath)) {
        Remove-Item -LiteralPath $runPayloadPath -Recurse -Force
    }

    if ($hadStartupValue) {
        New-Item -Path $startupKey -Force | Out-Null
        Set-ItemProperty -LiteralPath $startupKey -Name $startupName -Value $startupValue
    }
    else {
        Remove-ItemProperty `
            -LiteralPath $startupKey `
            -Name $startupName `
            -ErrorAction SilentlyContinue
    }
}
