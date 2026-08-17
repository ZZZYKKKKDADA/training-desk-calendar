using Xunit;

namespace TrainingDeskCalendar.App.Tests.Release;

public sealed class DataRecoveryScriptContractTests
{
    [Fact]
    public void DataRecoveryScript_UsesUniqueRootRunsAuditAndWritesHashEvidence()
    {
        string root = FindSolutionRoot();
        string script = File.ReadAllText(Path.Combine(root, "scripts", "test-data-recovery.ps1"));

        Assert.Contains("TRAINING_DESK_CALENDAR_RECOVERY_REPORT", script, StringComparison.Ordinal);
        Assert.Contains("Test-Path", script, StringComparison.Ordinal);
        Assert.Contains("Get-FileHash", script, StringComparison.Ordinal);
        Assert.Contains("Unique", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dotnet test", script, StringComparison.Ordinal);
        Assert.Contains("data-recovery-results.json", script, StringComparison.Ordinal);
        Assert.Contains("logs", script, StringComparison.OrdinalIgnoreCase);
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
