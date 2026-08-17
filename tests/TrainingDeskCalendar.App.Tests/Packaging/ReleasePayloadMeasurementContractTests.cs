using Xunit;

namespace TrainingDeskCalendar.App.Tests.Packaging;

public sealed class ReleasePayloadMeasurementContractTests
{
    [Fact]
    public void Script_RequiresFiveRunsAndEnforcesPerformanceLimits()
    {
        string script = ReadScript();

        Assert.Contains("[int]$Runs = 5", script, StringComparison.Ordinal);
        Assert.Contains("[int]$IdleSampleSeconds = 15", script, StringComparison.Ordinal);
        Assert.Contains("[long]$MaximumWorkingSetBytes = 200MB", script, StringComparison.Ordinal);
        Assert.Contains("$Runs -lt 5", script, StringComparison.Ordinal);
        Assert.Contains("2000", script, StringComparison.Ordinal);
        Assert.Contains("$summary.maximumWorkingSetBytes -le $MaximumWorkingSetBytes", script, StringComparison.Ordinal);
        Assert.Contains("maximumStartupMilliseconds", script, StringComparison.Ordinal);
        Assert.Contains("maximumWorkingSetBytes", script, StringComparison.Ordinal);
        Assert.Contains("processorMillisecondsAtReady", script, StringComparison.Ordinal);
        Assert.Contains("--ready-file", script, StringComparison.Ordinal);
        Assert.Contains("--exit-after-seconds", script, StringComparison.Ordinal);
        Assert.Contains("$runPayloadPath", script, StringComparison.Ordinal);
        Assert.Contains("Copy-Item", script, StringComparison.Ordinal);
        Assert.Contains("fresh-materialized-path", script, StringComparison.Ordinal);
        Assert.Contains("Join-Path $runPayloadPath 'TrainingDeskCalendar.App.exe'", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Script_RestoresStartupRegistrationAndWritesRawResults()
    {
        string script = ReadScript();

        Assert.Contains("HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Run", script, StringComparison.Ordinal);
        Assert.Contains("TrainingDeskCalendar", script, StringComparison.Ordinal);
        Assert.Contains("finally", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Set-ItemProperty", script, StringComparison.Ordinal);
        Assert.Contains("Remove-ItemProperty", script, StringComparison.Ordinal);
        Assert.Contains("measurements", script, StringComparison.Ordinal);
        Assert.Contains("ConvertTo-Json", script, StringComparison.Ordinal);
        Assert.Contains("phase3a-payload-results.json", script, StringComparison.Ordinal);
        Assert.Contains("sourceExecutableSha256", script, StringComparison.Ordinal);
        Assert.Contains("payloadBytes", script, StringComparison.Ordinal);
        Assert.Contains("payloadFileCount", script, StringComparison.Ordinal);
        Assert.Contains("osVersion", script, StringComparison.Ordinal);
        Assert.Contains("gitCommit", script, StringComparison.Ordinal);
        Assert.Contains("maximumWorkingSetBytes = $MaximumWorkingSetBytes", script, StringComparison.Ordinal);
        Assert.Contains("| Run |", script, StringComparison.Ordinal);
        Assert.Contains("Remove-Item -LiteralPath $runPayloadPath -Recurse", script, StringComparison.Ordinal);
        Assert.DoesNotContain("stored in `artifacts", script, StringComparison.Ordinal);
    }

    private static string ReadScript()
    {
        string? directory = AppContext.BaseDirectory;
        while (directory is not null &&
               !File.Exists(Path.Combine(directory, "TrainingDeskCalendar.sln")))
        {
            directory = Directory.GetParent(directory)?.FullName;
        }

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine(directory, "scripts", "measure-release-payload.ps1"));
    }
}
