using Xunit;

namespace TrainingDeskCalendar.App.Tests.Packaging;

public sealed class InstallerValidationScriptContractTests
{
    [Fact]
    public void Script_ValidatesCurrentUserInstallEntryPointsAndInstalledLaunch()
    {
        string script = ReadScript();

        Assert.Contains("TrainingDeskCalendar-Setup-0.1.1-x64.exe", script, StringComparison.Ordinal);
        Assert.Contains("/VERYSILENT", script, StringComparison.Ordinal);
        Assert.Contains("/CURRENTUSER", script, StringComparison.Ordinal);
        Assert.Contains("[Guid]::NewGuid()", script, StringComparison.Ordinal);
        Assert.Contains("TrainingDeskCalendar.App.exe", script, StringComparison.Ordinal);
        Assert.Contains("$shortcutFileName", script, StringComparison.Ordinal);
        Assert.Contains("[char]0x8BAD", script, StringComparison.Ordinal);
        Assert.Contains("[char]0x7EC3", script, StringComparison.Ordinal);
        Assert.Contains("[char]0x684C", script, StringComparison.Ordinal);
        Assert.Contains("[char]0x5386", script, StringComparison.Ordinal);
        Assert.DoesNotContain("训练桌历.lnk", script, StringComparison.Ordinal);
        Assert.Contains("WScript.Shell", script, StringComparison.Ordinal);
        Assert.Contains("--ready-file", script, StringComparison.Ordinal);
        Assert.Contains("HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Run", script, StringComparison.Ordinal);
        Assert.Contains("TrainingDeskCalendar", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Script_ValidatesUpgradeAndBothUninstallDataPolicies()
    {
        string script = ReadScript();

        Assert.Contains("upgrade-sentinel", script, StringComparison.Ordinal);
        Assert.Contains("preserve-sentinel", script, StringComparison.Ordinal);
        Assert.Contains("delete-sentinel", script, StringComparison.Ordinal);
        Assert.Contains("/DELETEUSERDATA", script, StringComparison.Ordinal);
        Assert.Contains("unins000.exe", script, StringComparison.Ordinal);
        Assert.Contains("installedDirectoryBytes", script, StringComparison.Ordinal);
        Assert.Contains("installerSha256", script, StringComparison.Ordinal);
        Assert.Contains("package-manifest.json", script, StringComparison.Ordinal);
        Assert.Contains("applicationVersion", script, StringComparison.Ordinal);
        Assert.Contains("$installer.Length -ge 80MB", script, StringComparison.Ordinal);
        Assert.Contains("$installedDirectoryBytes -ge 150MB", script, StringComparison.Ordinal);
        Assert.Contains("installer-results.json", script, StringComparison.Ordinal);
        Assert.Contains("``artifacts/installer-validation/installer-results.json``", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Script_RefusesUnsafeStateAndRestoresCurrentUserStateInFinally()
    {
        string script = ReadScript();

        Assert.Contains("Get-Process -Name 'TrainingDeskCalendar.App'", script, StringComparison.Ordinal);
        Assert.Contains("existing Training Desk Calendar installation", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("finally", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Set-ItemProperty", script, StringComparison.Ordinal);
        Assert.Contains("Remove-ItemProperty", script, StringComparison.Ordinal);
        Assert.Contains("Move-Item", script, StringComparison.Ordinal);
        Assert.Contains("Remove-Item -LiteralPath $testRoot -Recurse", script, StringComparison.Ordinal);
        Assert.Contains("StartsWith($artifactsPrefix", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Script_AllowsValidationWhenStartupValueDoesNotExist()
    {
        string script = ReadScript();

        Assert.Contains("Get-ItemProperty -LiteralPath $runKey", script, StringComparison.Ordinal);
        Assert.Contains("PSObject.Properties.Name -contains $runName", script, StringComparison.Ordinal);
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
        return File.ReadAllText(Path.Combine(directory, "scripts", "test-installer.ps1"));
    }
}
