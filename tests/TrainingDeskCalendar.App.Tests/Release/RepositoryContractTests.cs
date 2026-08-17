using Xunit;

namespace TrainingDeskCalendar.App.Tests.Release;

public sealed class RepositoryContractTests
{
    [Fact]
    public void Repository_ContainsLicenseAndChineseUserDocumentation()
    {
        string root = FindSolutionRoot();
        string license = File.ReadAllText(Path.Combine(root, "LICENSE"));
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));
        string contributing = File.ReadAllText(Path.Combine(root, "CONTRIBUTING.md"));

        Assert.Contains("MIT License", license, StringComparison.Ordinal);
        Assert.Contains("Permission is hereby granted, free of charge", license, StringComparison.Ordinal);
        Assert.Contains("安装", readme, StringComparison.Ordinal);
        Assert.Contains("两周", readme, StringComparison.Ordinal);
        Assert.Contains("%LOCALAPPDATA%\\TrainingDeskCalendar", readme, StringComparison.Ordinal);
        Assert.Contains("导入", readme, StringComparison.Ordinal);
        Assert.Contains("导出", readme, StringComparison.Ordinal);
        Assert.Contains("卸载默认保留", readme, StringComparison.Ordinal);
        Assert.Contains("dotnet test", readme, StringComparison.Ordinal);
        Assert.Contains("提交", contributing, StringComparison.Ordinal);
        Assert.Contains("测试", contributing, StringComparison.Ordinal);
    }

    [Fact]
    public void ContinuousIntegration_UsesLockedRestoreAndBothConfigurations()
    {
        string workflow = File.ReadAllText(Path.Combine(
            FindSolutionRoot(), ".github", "workflows", "ci.yml"));

        Assert.Contains("contents: read", workflow, StringComparison.Ordinal);
        Assert.Contains("actions/checkout@v4", workflow, StringComparison.Ordinal);
        Assert.Contains("actions/setup-dotnet@v4", workflow, StringComparison.Ordinal);
        Assert.Contains("dotnet restore TrainingDeskCalendar.sln --runtime win-x64 --locked-mode", workflow, StringComparison.Ordinal);
        Assert.Contains("dotnet build TrainingDeskCalendar.sln --configuration Release --no-restore", workflow, StringComparison.Ordinal);
        Assert.Contains("dotnet test TrainingDeskCalendar.sln --configuration Debug --no-restore", workflow, StringComparison.Ordinal);
        Assert.Contains("dotnet test TrainingDeskCalendar.sln --configuration Release --no-restore", workflow, StringComparison.Ordinal);
        Assert.Contains("git diff --check", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("secrets.", workflow, StringComparison.OrdinalIgnoreCase);
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
