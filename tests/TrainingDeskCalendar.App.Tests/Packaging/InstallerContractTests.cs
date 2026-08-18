using Xunit;

namespace TrainingDeskCalendar.App.Tests.Packaging;

public sealed class InstallerContractTests
{
    [Fact]
    public void Installer_UsesCurrentUserScopeWithoutAdministratorOverride()
    {
        string script = ReadInstaller();

        Assert.Contains("PrivilegesRequired=lowest", script, StringComparison.Ordinal);
        Assert.Matches(@"(?m)^PrivilegesRequiredOverridesAllowed=\r?$", script);
        Assert.Contains("DefaultDirName={localappdata}\\Programs\\TrainingDeskCalendar", script, StringComparison.Ordinal);
        Assert.Contains("MinVersion=10.0.19041", script, StringComparison.Ordinal);
        Assert.DoesNotContain("HKLM", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("{autopf}", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Installer_EntryPointsTargetApplicationExecutableDirectly()
    {
        string script = ReadInstaller();

        Assert.Contains("Filename: \"{app}\\TrainingDeskCalendar.App.exe\"; Description: \"{cm:LaunchProgram,{#AppDisplayName}}\"", script, StringComparison.Ordinal);
        Assert.Contains("Name: \"{autodesktop}\\{#AppDisplayName}\"; Filename: \"{app}\\TrainingDeskCalendar.App.exe\"", script, StringComparison.Ordinal);
        Assert.Contains("Name: \"{autoprograms}\\{#AppDisplayName}\"; Filename: \"{app}\\TrainingDeskCalendar.App.exe\"", script, StringComparison.Ordinal);
        Assert.Contains("Root: HKCU; Subkey: \"Software\\Microsoft\\Windows\\CurrentVersion\\Run\"; ValueName: \"TrainingDeskCalendar\"; ValueData: \"\"\"{app}\\TrainingDeskCalendar.App.exe\"\"\"", script, StringComparison.Ordinal);
        Assert.Matches("(?m)^Name: \"desktopicon\"; Description: .+\\r?$", script);
        Assert.DoesNotMatch("(?m)^Name: \"desktopicon\";.*Flags: unchecked", script);
    }

    [Fact]
    public void Installer_DefaultUninstallRetainsPersonalDataAndRemovesInstalledEntryPoints()
    {
        string script = ReadInstaller();

        Assert.Contains("uninsdeletevalue", script, StringComparison.Ordinal);
        Assert.Contains("[Icons]", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Name: \"{localappdata}\\TrainingDeskCalendar\"; Type: filesandordirs", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CreateCustomForm", script, StringComparison.Ordinal);
        Assert.Contains("TNewCheckBox", script, StringComparison.Ordinal);
        Assert.Contains("DeletePersonalDataCheckBox.Checked", script, StringComparison.Ordinal);
        Assert.Contains("DeletePersonalData", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_ValidatesExactCurrentUserDataDirectoryBeforeRecursiveDeletion()
    {
        string script = ReadInstaller();

        Assert.Contains("ExpandConstant('{localappdata}\\TrainingDeskCalendar')", script, StringComparison.Ordinal);
        Assert.Contains("RemoveBackslashUnlessRoot", script, StringComparison.Ordinal);
        Assert.Contains("CompareText(DeleteTarget, ExpectedTarget) <> 0", script, StringComparison.Ordinal);
        Assert.Contains("DelTree(DeleteTarget, True, True, True)", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_SilentUninstallPreservesDataUnlessDeletionIsExplicitlyRequested()
    {
        string script = ReadInstaller();

        Assert.Contains("CmdLineParamExists('/SILENT')", script, StringComparison.Ordinal);
        Assert.Contains("CmdLineParamExists('/VERYSILENT')", script, StringComparison.Ordinal);
        Assert.Contains("CmdLineParamExists('/DELETEUSERDATA')", script, StringComparison.Ordinal);
        Assert.Contains("if IsSilentUninstall then", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_PackagesFixedPayloadWithinReleaseSizeLimitsAndSupportsUpgrade()
    {
        string script = ReadInstaller();

        Assert.Contains("Source: \"..\\artifacts\\windows-x64\\payload\\*\"", script, StringComparison.Ordinal);
        Assert.Contains("OutputDir=..\\artifacts\\installer", script, StringComparison.Ordinal);
        Assert.Contains("#define OutputBaseFilename \"TrainingDeskCalendar-Setup-0.1.3-x64\"", script, StringComparison.Ordinal);
        Assert.Contains("OutputBaseFilename={#OutputBaseFilename}", script, StringComparison.Ordinal);
        Assert.Contains("LZMA2", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CloseApplications=yes", script, StringComparison.Ordinal);
        Assert.Contains("RestartApplications=no", script, StringComparison.Ordinal);
        Assert.Contains("CloseApplicationsFilter=TrainingDeskCalendar.App.exe", script, StringComparison.Ordinal);
        Assert.Contains("postinstall", script, StringComparison.Ordinal);
        Assert.DoesNotContain("MaximumInstalledPayloadBytes", script, StringComparison.Ordinal);
        Assert.DoesNotContain("MaximumInstallerBytes", script, StringComparison.Ordinal);
    }

    private static string ReadInstaller()
    {
        string? directory = AppContext.BaseDirectory;
        while (directory is not null &&
               !File.Exists(Path.Combine(directory, "TrainingDeskCalendar.sln")))
        {
            directory = Directory.GetParent(directory)?.FullName;
        }

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine(directory, "installer", "TrainingDeskCalendar.iss"));
    }
}
