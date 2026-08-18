[CmdletBinding()]
param(
    [string]$InstallerPath,
    [int]$ReadyTimeoutSeconds = 15
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-Condition {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Invoke-CheckedProcess {
    param(
        [string]$FilePath,
        [string[]]$ArgumentList
    )

    $process = Start-Process `
        -FilePath $FilePath `
        -ArgumentList $ArgumentList `
        -Wait `
        -PassThru
    if ($process.ExitCode -ne 0) {
        throw "$FilePath exited with code $($process.ExitCode)."
    }
}

function Get-ShortcutTarget {
    param([string]$ShortcutPath)

    $shell = New-Object -ComObject WScript.Shell
    try {
        return $shell.CreateShortcut($ShortcutPath).TargetPath
    }
    finally {
        [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) | Out-Null
    }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))
$artifactsPrefix = $artifactsRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) +
    [IO.Path]::DirectorySeparatorChar
if ([string]::IsNullOrWhiteSpace($InstallerPath)) {
    $InstallerPath = Join-Path $artifactsRoot `
        'installer\TrainingDeskCalendar-Setup-0.1.2-x64.exe'
}
$InstallerPath = [IO.Path]::GetFullPath($InstallerPath)
if (-not $InstallerPath.StartsWith($artifactsPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'InstallerPath must be a child of the repository artifacts directory.'
}
Assert-Condition `
    (Test-Path -LiteralPath $InstallerPath -PathType Leaf) `
    "Installer not found at $InstallerPath."
$installer = Get-Item -LiteralPath $InstallerPath
if ($installer.Length -ge 80MB) {
    throw "Installer size $($installer.Length) bytes exceeds the 80 MiB limit."
}

$activeProcess = Get-Process -Name 'TrainingDeskCalendar.App' -ErrorAction SilentlyContinue
if ($null -ne $activeProcess) {
    throw 'Close Training Desk Calendar before running installer validation.'
}

$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{50D83759-8D5B-4F74-8BD7-C23C04777BE8}_is1'
if (Test-Path -LiteralPath $uninstallKey) {
    throw 'Remove the existing Training Desk Calendar installation before validation.'
}

$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$runName = 'TrainingDeskCalendar'
$runProperties = Get-ItemProperty -LiteralPath $runKey -ErrorAction SilentlyContinue
$hadRunValue = $null -ne $runProperties -and
    $runProperties.PSObject.Properties.Name -contains $runName
$savedRunValue = if ($hadRunValue) { [string]$runProperties.$runName } else { $null }

$sessionId = [Guid]::NewGuid().ToString('N')
$validationRoot = Join-Path $artifactsRoot 'installer-validation'
$testRoot = Join-Path $validationRoot "session-$sessionId"
$installPath = Join-Path $testRoot 'app'
$dataPath = Join-Path `
    ([Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)) `
    'TrainingDeskCalendar'
$dataBackupPath = Join-Path $testRoot 'user-data-backup'
$shortcutFileName = '{0}{1}{2}{3}.lnk' -f `
    ([char]0x8BAD), `
    ([char]0x7EC3), `
    ([char]0x684C), `
    ([char]0x5386)
$desktopShortcut = Join-Path `
    ([Environment]::GetFolderPath([Environment+SpecialFolder]::DesktopDirectory)) `
    $shortcutFileName
$programsShortcut = Join-Path `
    ([Environment]::GetFolderPath([Environment+SpecialFolder]::Programs)) `
    $shortcutFileName
$desktopBackup = Join-Path $testRoot 'desktop-shortcut-backup.lnk'
$programsBackup = Join-Path $testRoot 'programs-shortcut-backup.lnk'
$resultPath = Join-Path $validationRoot 'installer-results.json'
$reportPath = Join-Path $repoRoot 'docs\validation\installer-results.md'
$readyFile = Join-Path $testRoot 'installed-ready.txt'
$installedProcess = $null
$dataWasBackedUp = $false
$desktopWasBackedUp = $false
$programsWasBackedUp = $false
$installedDirectoryBytes = 0L
$applicationVersion = $null
$checks = [ordered]@{}

New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
New-Item -ItemType Directory -Path (Split-Path -Parent $reportPath) -Force | Out-Null

try {
    if (Test-Path -LiteralPath $dataPath) {
        Move-Item -LiteralPath $dataPath -Destination $dataBackupPath
        $dataWasBackedUp = $true
    }
    if (Test-Path -LiteralPath $desktopShortcut -PathType Leaf) {
        Move-Item -LiteralPath $desktopShortcut -Destination $desktopBackup
        $desktopWasBackedUp = $true
    }
    if (Test-Path -LiteralPath $programsShortcut -PathType Leaf) {
        Move-Item -LiteralPath $programsShortcut -Destination $programsBackup
        $programsWasBackedUp = $true
    }

    $installArguments = @(
        '/VERYSILENT',
        '/CURRENTUSER',
        '/SUPPRESSMSGBOXES',
        '/NORESTART',
        "/DIR=$installPath",
        "/LOG=$(Join-Path $testRoot 'install.log')"
    )
    Invoke-CheckedProcess -FilePath $InstallerPath -ArgumentList $installArguments

    $applicationPath = Join-Path $installPath 'TrainingDeskCalendar.App.exe'
    Assert-Condition (Test-Path -LiteralPath $applicationPath -PathType Leaf) `
        'The installed application executable is missing.'
    $installedManifestPath = Join-Path $installPath 'package-manifest.json'
    Assert-Condition (Test-Path -LiteralPath $installedManifestPath -PathType Leaf) `
        'The installed package manifest is missing.'
    $installedManifest = Get-Content -LiteralPath $installedManifestPath -Raw |
        ConvertFrom-Json
    $applicationVersion = [string]$installedManifest.applicationVersion
    Assert-Condition (-not [string]::IsNullOrWhiteSpace($applicationVersion)) `
        'The installed application version is missing.'
    Assert-Condition (Test-Path -LiteralPath $desktopShortcut -PathType Leaf) `
        'The default desktop shortcut is missing.'
    Assert-Condition (Test-Path -LiteralPath $programsShortcut -PathType Leaf) `
        'The Start menu shortcut is missing.'
    Assert-Condition `
        ((Get-ShortcutTarget $desktopShortcut) -eq $applicationPath) `
        'The desktop shortcut target is incorrect.'
    Assert-Condition `
        ((Get-ShortcutTarget $programsShortcut) -eq $applicationPath) `
        'The Start menu shortcut target is incorrect.'

    $expectedRunValue = '"' + $applicationPath + '"'
    $installedRunValue = Get-ItemPropertyValue -LiteralPath $runKey -Name $runName
    Assert-Condition ($installedRunValue -eq $expectedRunValue) `
        'The installed HKCU Run entry is incorrect.'
    $checks.currentUserInstall = $true
    $checks.desktopShortcut = $true
    $checks.startMenuShortcut = $true
    $checks.defaultStartup = $true

    $installedProcess = Start-Process `
        -FilePath $applicationPath `
        -ArgumentList @('--ready-file', $readyFile, '--exit-after-seconds', '3') `
        -PassThru
    $readyDeadline = [DateTime]::UtcNow.AddSeconds($ReadyTimeoutSeconds)
    while (-not (Test-Path -LiteralPath $readyFile -PathType Leaf)) {
        $installedProcess.Refresh()
        if ($installedProcess.HasExited) {
            throw 'The installed application exited before its ready signal.'
        }
        if ([DateTime]::UtcNow -ge $readyDeadline) {
            throw 'The installed application did not become ready in time.'
        }
        Start-Sleep -Milliseconds 25
    }
    Assert-Condition ($installedProcess.WaitForExit(15000)) `
        'The installed application did not exit after its test timeout.'
    $installedProcess.WaitForExit()
    Assert-Condition ($installedProcess.ExitCode -eq 0) `
        "The installed application exited with code $($installedProcess.ExitCode)."
    $installedProcess.Dispose()
    $installedProcess = $null
    $checks.installedLaunch = $true

    New-Item -ItemType Directory -Path $dataPath -Force | Out-Null
    $upgradeSentinel = Join-Path $dataPath 'upgrade-sentinel'
    Set-Content -LiteralPath $upgradeSentinel -Value $sessionId -Encoding utf8
    Invoke-CheckedProcess -FilePath $InstallerPath -ArgumentList $installArguments
    Assert-Condition (Test-Path -LiteralPath $upgradeSentinel -PathType Leaf) `
        'Same-version upgrade removed application data.'
    $checks.sameVersionUpgradePreservesData = $true

    $installedDirectoryBytes = (Get-ChildItem -LiteralPath $installPath -File -Recurse |
        Measure-Object -Property Length -Sum).Sum
    if ($installedDirectoryBytes -ge 150MB) {
        throw "Installed directory size $installedDirectoryBytes bytes exceeds the 150 MiB limit."
    }
    $preserveSentinel = Join-Path $dataPath 'preserve-sentinel'
    Set-Content -LiteralPath $preserveSentinel -Value $sessionId -Encoding utf8
    $uninstallerPath = Join-Path $installPath 'unins000.exe'
    Invoke-CheckedProcess -FilePath $uninstallerPath -ArgumentList @(
        '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART'
    )
    Assert-Condition (Test-Path -LiteralPath $preserveSentinel -PathType Leaf) `
        'Default uninstall removed personal data.'
    Assert-Condition (-not (Test-Path -LiteralPath $applicationPath)) `
        'Default uninstall left the application executable behind.'
    $checks.defaultUninstallPreservesData = $true

    Invoke-CheckedProcess -FilePath $InstallerPath -ArgumentList $installArguments
    $deleteSentinel = Join-Path $dataPath 'delete-sentinel'
    Set-Content -LiteralPath $deleteSentinel -Value $sessionId -Encoding utf8
    $uninstallerPath = Join-Path $installPath 'unins000.exe'
    Invoke-CheckedProcess -FilePath $uninstallerPath -ArgumentList @(
        '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/DELETEUSERDATA'
    )
    Assert-Condition (-not (Test-Path -LiteralPath $dataPath)) `
        'Explicit data deletion did not remove the application data directory.'
    $checks.explicitUninstallDeletesData = $true

    $result = [ordered]@{
        validatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        osVersion = [Environment]::OSVersion.VersionString
        installerPath = $InstallerPath
        installerBytes = $installer.Length
        installerSha256 = (Get-FileHash -LiteralPath $InstallerPath -Algorithm SHA256).Hash.ToLowerInvariant()
        applicationVersion = $applicationVersion
        installedDirectoryBytes = $installedDirectoryBytes
        checks = $checks
    }
    New-Item -ItemType Directory -Path $validationRoot -Force | Out-Null
    $result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $resultPath -Encoding utf8

    $installerMb = [Math]::Round($installer.Length / 1MB, 2)
    $installedMb = [Math]::Round($installedDirectoryBytes / 1MB, 2)
    $report = @"
# Installer Validation Results

- Validated UTC: $($result.validatedAtUtc)
- OS: $($result.osVersion)
- Installer SHA-256: $($result.installerSha256)
- Application version: $applicationVersion
- Installer size: $installerMb MiB
- Installed directory size: $installedMb MiB
- Current-user install without UAC: PASS
- Chinese desktop and Start menu shortcuts: PASS
- Installed application ready signal: PASS
- Default HKCU startup registration: PASS
- Same-version upgrade preserves data: PASS
- Default uninstall preserves data: PASS
- Explicit `/DELETEUSERDATA` removes the exact application data directory: PASS

Machine-readable results: ``artifacts/installer-validation/installer-results.json``.
"@
    $report | Set-Content -LiteralPath $reportPath -Encoding utf8
    Write-Host $report
}
finally {
    if ($null -ne $installedProcess) {
        $installedProcess.Refresh()
        if (-not $installedProcess.HasExited) {
            Stop-Process -Id $installedProcess.Id -Force
            $installedProcess.WaitForExit()
        }
        $installedProcess.Dispose()
    }

    $remainingUninstaller = Join-Path $installPath 'unins000.exe'
    if (Test-Path -LiteralPath $remainingUninstaller -PathType Leaf) {
        $cleanup = Start-Process `
            -FilePath $remainingUninstaller `
            -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') `
            -Wait `
            -PassThru
        if ($cleanup.ExitCode -ne 0) {
            Write-Warning "Cleanup uninstaller exited with code $($cleanup.ExitCode)."
        }
    }

    if (Test-Path -LiteralPath $dataPath) {
        Remove-Item -LiteralPath $dataPath -Recurse -Force
    }
    if ($dataWasBackedUp -and (Test-Path -LiteralPath $dataBackupPath)) {
        Move-Item -LiteralPath $dataBackupPath -Destination $dataPath
    }

    foreach ($shortcut in @($desktopShortcut, $programsShortcut)) {
        Remove-Item -LiteralPath $shortcut -Force -ErrorAction SilentlyContinue
    }
    if ($desktopWasBackedUp -and (Test-Path -LiteralPath $desktopBackup)) {
        Move-Item -LiteralPath $desktopBackup -Destination $desktopShortcut
    }
    if ($programsWasBackedUp -and (Test-Path -LiteralPath $programsBackup)) {
        Move-Item -LiteralPath $programsBackup -Destination $programsShortcut
    }

    if ($hadRunValue) {
        New-Item -Path $runKey -Force | Out-Null
        Set-ItemProperty -LiteralPath $runKey -Name $runName -Value $savedRunValue
    }
    else {
        Remove-ItemProperty `
            -LiteralPath $runKey `
            -Name $runName `
            -ErrorAction SilentlyContinue
    }

    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
