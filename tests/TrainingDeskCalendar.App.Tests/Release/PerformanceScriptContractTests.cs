using Xunit;

namespace TrainingDeskCalendar.App.Tests.Release;

public sealed class PerformanceScriptContractTests
{
    [Fact]
    public void ReleasePerformanceScript_EnforcesAllApprovedGatesAndRecordsRawSamples()
    {
        string root = FindSolutionRoot();
        string script = File.ReadAllText(Path.Combine(root, "scripts", "measure-release.ps1"));

        Assert.Contains("Runs -lt 5", script, StringComparison.Ordinal);
        Assert.Contains("IdleSampleSeconds -lt 60", script, StringComparison.Ordinal);
        Assert.Contains("SaveLatencySamples -lt 10", script, StringComparison.Ordinal);
        Assert.Contains("MaximumWorkingSetBytes", script, StringComparison.Ordinal);
        Assert.Contains("MaximumIdleCpuPercent", script, StringComparison.Ordinal);
        Assert.Contains("MaximumSaveLatencyMilliseconds", script, StringComparison.Ordinal);
        Assert.Contains("fresh-materialized-path", script, StringComparison.Ordinal);
        Assert.Contains("--data-root", script, StringComparison.Ordinal);
        Assert.Contains("--save-latency-file", script, StringComparison.Ordinal);
        Assert.Contains("Get-FileHash", script, StringComparison.Ordinal);
        Assert.Contains("installerBytes", script, StringComparison.Ordinal);
        Assert.Contains("installedDirectoryBytes", script, StringComparison.Ordinal);
        Assert.Contains("finally", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TrainingDeskCalendar", script, StringComparison.Ordinal);
    }

    private static string FindSolutionRoot()
    {
        string? directory = AppContext.BaseDirectory;
        while (directory is not null &&
               !File.Exists(Path.Combine(directory, "TrainingDeskCalendar.sln")))
        {
            directory = Directory.GetParent(directory)?.FullName;
        }

        return directory ?? throw new DirectoryNotFoundException();
    }
}
