[CmdletBinding()]
param(
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $artifactsRoot 'data-recovery\data-recovery-results.json'
}
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
if (-not $OutputPath.StartsWith(
        $artifactsRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputPath must be under $artifactsRoot."
}

$reportDirectory = Split-Path -Parent $OutputPath
$testReportPath = Join-Path $reportDirectory 'test-audit.json'
$markdownPath = Join-Path $repoRoot 'docs\validation\data-recovery-results.md'
$uniqueRoot = Join-Path ([IO.Path]::GetTempPath()) "training-desk-recovery-$([Guid]::NewGuid().ToString('N'))"
$testProject = Join-Path $repoRoot 'tests\TrainingDeskCalendar.App.Tests\TrainingDeskCalendar.App.Tests.csproj'
$rootVariable = 'TRAINING_DESK_CALENDAR_RECOVERY_ROOT'
$reportVariable = 'TRAINING_DESK_CALENDAR_RECOVERY_REPORT'
$oldRoot = [Environment]::GetEnvironmentVariable($rootVariable, 'Process')
$oldReport = [Environment]::GetEnvironmentVariable($reportVariable, 'Process')

try {
    New-Item -ItemType Directory -Path $uniqueRoot -Force | Out-Null
    New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
    [Environment]::SetEnvironmentVariable($rootVariable, $uniqueRoot, 'Process')
    [Environment]::SetEnvironmentVariable($reportVariable, $testReportPath, 'Process')

    & dotnet test $testProject `
        --configuration Debug `
        --no-restore `
        --filter FullyQualifiedName~RecoveryAuditTests
    if ($LASTEXITCODE -ne 0) {
        throw "Recovery audit tests failed with exit code $LASTEXITCODE."
    }
    if (-not (Test-Path -LiteralPath $testReportPath -PathType Leaf)) {
        throw "Recovery audit did not produce $testReportPath."
    }

    $testReport = Get-Content -LiteralPath $testReportPath -Raw | ConvertFrom-Json
    $scenarioRecords = @($testReport.scenarios)
    if ($scenarioRecords.Count -lt 5) {
        throw 'Recovery audit produced fewer than five scenarios.'
    }
    foreach ($record in $scenarioRecords) {
        if ([string]::IsNullOrWhiteSpace($record.before.databaseSha256) -or
            [string]::IsNullOrWhiteSpace($record.after.databaseSha256) -or
            [string]::IsNullOrWhiteSpace($record.before.settingsSha256) -or
            [string]::IsNullOrWhiteSpace($record.after.settingsSha256)) {
            throw "Scenario $($record.scenario) did not include file hashes."
        }
    }

    $logFiles = @(Get-ChildItem -LiteralPath $uniqueRoot -File -Recurse -Filter '*.log' -ErrorAction SilentlyContinue)
    $logsContainPlanText = $false
    foreach ($logFile in $logFiles) {
        $text = Get-Content -LiteralPath $logFile.FullName -Raw
        if ($text.Contains('保留计划', [StringComparison]::Ordinal) -or
            $text.Contains('不应保留', [StringComparison]::Ordinal)) {
            $logsContainPlanText = $true
        }
    }

    $auditSha256 = (Get-FileHash -LiteralPath $testReportPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $passed = [bool]$testReport.allScenariosPassed -and
        -not [bool]$testReport.logsContainPlanText -and
        -not $logsContainPlanText
    $result = [ordered]@{
        auditedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        uniqueRoot = $uniqueRoot
        testReportSha256 = $auditSha256
        scenarioCount = $scenarioRecords.Count
        logsContainPlanText = [bool]$testReport.logsContainPlanText -or $logsContainPlanText
        allScenariosPassed = $passed
        scenarios = $scenarioRecords
    }
    $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutputPath -Encoding utf8

    $rows = ($scenarioRecords | ForEach-Object {
        "| $($_.scenario) | $($_.outcome) | $($_.before.databaseSha256.Substring(0, 12))... | $($_.after.databaseSha256.Substring(0, 12))... | $($_.passed) |"
    }) -join [Environment]::NewLine
    $markdown = @"
# Data Recovery Validation Results

- Audited UTC: $($result.auditedAtUtc)
- Unique temporary root: $uniqueRoot
- Scenario count: $($result.scenarioCount)
- Audit JSON SHA-256: $auditSha256
- Logs contain training plan text: $($result.logsContainPlanText) - $(if (-not $result.logsContainPlanText) { 'PASS' } else { 'FAIL' })
- All scenarios passed: $($result.allScenariosPassed)

Physical database and settings SHA-256 values are retained for every scenario. SQLite rollback may rewrite physical database pages; the audit also records canonical logical plan/settings hashes and only marks rollback successful when logical state is restored.

| Scenario | Outcome | Database before | Database after | Passed |
| --- | --- | --- | --- | --- |
$rows

Machine-readable result: artifacts/data-recovery/data-recovery-results.json.
"@
    $markdown | Set-Content -LiteralPath $markdownPath -Encoding utf8
    Write-Host $markdown

    if (-not $passed) { exit 1 }
}
finally {
    if ($null -eq $oldRoot) {
        [Environment]::SetEnvironmentVariable($rootVariable, $null, 'Process')
    }
    else {
        [Environment]::SetEnvironmentVariable($rootVariable, $oldRoot, 'Process')
    }
    if ($null -eq $oldReport) {
        [Environment]::SetEnvironmentVariable($reportVariable, $null, 'Process')
    }
    else {
        [Environment]::SetEnvironmentVariable($reportVariable, $oldReport, 'Process')
    }
    if (Test-Path -LiteralPath $uniqueRoot) {
        Remove-Item -LiteralPath $uniqueRoot -Recurse -Force
    }
}
