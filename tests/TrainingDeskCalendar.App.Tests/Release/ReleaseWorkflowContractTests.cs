using Xunit;

namespace TrainingDeskCalendar.App.Tests.Release;

public sealed class ReleaseWorkflowContractTests
{
    [Fact]
    public void ReleaseWorkflow_ValidatesTagBuildsPayloadAndPublishesAssets()
    {
        string root = FindSolutionRoot();
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "release.yml"));
        string installer = File.ReadAllText(Path.Combine(root, "installer", "TrainingDeskCalendar.iss"));

        Assert.Contains("contents: write", workflow, StringComparison.Ordinal);
        Assert.Contains("validate-release-tag.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("Versions.props", workflow, StringComparison.Ordinal);
        Assert.Contains("dotnet restore TrainingDeskCalendar.sln --runtime win-x64 --locked-mode", workflow, StringComparison.Ordinal);
        Assert.Contains("scripts/package-windows.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("RepositoryUrl", workflow, StringComparison.Ordinal);
        Assert.Contains("ISCC.exe", workflow, StringComparison.Ordinal);
        Assert.Contains("choco install innosetup --version=6.7.1", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("--version=6.7.3", workflow, StringComparison.Ordinal);
        Assert.Contains("RELEASE_VERSION", workflow, StringComparison.Ordinal);
        Assert.Contains("/DAppVersion=", workflow, StringComparison.Ordinal);
        Assert.Contains("write-checksums.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("gh release create", workflow, StringComparison.Ordinal);
        Assert.Contains("--notes-file", workflow, StringComparison.Ordinal);
        Assert.Contains("TrainingDeskCalendar-Setup-$env:RELEASE_VERSION-x64.exe", workflow, StringComparison.Ordinal);
        Assert.Contains("sha256", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("certificate", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#ifndef AppVersion", installer, StringComparison.Ordinal);
        Assert.Contains("#ifndef OutputBaseFilename", installer, StringComparison.Ordinal);
        Assert.Contains("OutputBaseFilename={#OutputBaseFilename}", installer, StringComparison.Ordinal);
    }

    [Fact]
    public void TagValidation_AndChecksumScriptsHaveExplicitBoundaries()
    {
        string root = FindSolutionRoot();
        string tagScript = File.ReadAllText(Path.Combine(root, "scripts", "validate-release-tag.ps1"));
        string checksumScript = File.ReadAllText(Path.Combine(root, "scripts", "write-checksums.ps1"));

        Assert.Contains("^v\\d+\\.\\d+\\.\\d+$", tagScript, StringComparison.Ordinal);
        Assert.Contains("VersionPrefix", tagScript, StringComparison.Ordinal);
        Assert.Contains("SHA256", checksumScript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Test-Path", checksumScript, StringComparison.Ordinal);
        Assert.Contains("ConvertTo-Json", checksumScript, StringComparison.Ordinal);
        Assert.Contains("output", checksumScript, StringComparison.OrdinalIgnoreCase);
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
