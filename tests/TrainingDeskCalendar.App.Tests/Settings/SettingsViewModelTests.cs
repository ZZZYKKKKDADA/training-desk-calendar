using TrainingDeskCalendar.App.Persistence;
using TrainingDeskCalendar.App.Settings;
using System.Xml.Linq;
using Xunit;

namespace TrainingDeskCalendar.App.Tests.Settings;

public sealed class SettingsViewModelTests
{
    [Fact]
    public void SettingsWindow_StartupBinding_IsExplicitlyOneWay()
    {
        XDocument document = XDocument.Load(Path.Combine(
            FindSolutionRoot(),
            "src",
            "TrainingDeskCalendar.App",
            "Settings",
            "SettingsWindow.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XElement startupCheckBox = Assert.Single(
            document.Descendants(presentation + "CheckBox"),
            element => (string?)element.Attribute("Content") == "开机自动启动");

        Assert.Equal(
            "{Binding StartWithWindows, Mode=OneWay}",
            (string?)startupCheckBox.Attribute("IsChecked"));
    }

    [Fact]
    public async Task ApplyAsync_EmitsValidatedSettingsAndCanResetWindow()
    {
        AppSettings applied = AppSettings.Defaults;
        var viewModel = new SettingsViewModel(
            AppSettings.Defaults,
            settings =>
            {
                applied = settings;
                return Task.CompletedTask;
            },
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            () => Task.CompletedTask);
        viewModel.Theme = AppTheme.Dark;
        viewModel.Opacity = 0.6;
        viewModel.IsLocked = true;
        viewModel.ResetWindow();

        await viewModel.ApplyAsync();

        Assert.Equal(AppTheme.Dark, applied.Theme);
        Assert.Equal(0.6, applied.Opacity);
        Assert.True(applied.IsLocked);
        Assert.Equal(AppSettings.Defaults.WindowWidth, applied.WindowWidth);
        Assert.Equal(AppSettings.Defaults.WindowHeight, applied.WindowHeight);
    }

    [Fact]
    public void Opacity_RejectsValuesOutsideTheApprovedRange()
    {
        var viewModel = CreateViewModel();

        Assert.Throws<InvalidDataException>(() => viewModel.Opacity = 0.39);
        Assert.Throws<InvalidDataException>(() => viewModel.Opacity = 1.01);
    }

    [Fact]
    public async Task ImportExportAndUpdateCommands_InvokeTheirServiceBoundaries()
    {
        var calls = new List<string>();
        var viewModel = new SettingsViewModel(
            AppSettings.Defaults,
            _ => Task.CompletedTask,
            _ => { calls.Add("startup"); return Task.CompletedTask; },
            path => { calls.Add($"export:{path}"); return Task.CompletedTask; },
            path => { calls.Add($"import:{path}"); return Task.CompletedTask; },
            () => { calls.Add("update"); return Task.CompletedTask; });

        await viewModel.SetStartWithWindowsAsync(false);
        await viewModel.ExportAsync("export.json");
        await viewModel.ImportAsync("import.json");
        await viewModel.CheckUpdatesAsync();

        Assert.Equal(["startup", "export:export.json", "import:import.json", "update"], calls);
        Assert.Equal("更新检查将在阶段 3 提供。", viewModel.StatusMessage);
    }

    [Fact]
    public async Task ImportFailure_IsExposedWithoutChangingTheCurrentSettings()
    {
        var viewModel = new SettingsViewModel(
            AppSettings.Defaults,
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            _ => Task.FromException(new InvalidDataException("文件格式无效")),
            () => Task.CompletedTask);

        await Assert.ThrowsAsync<InvalidDataException>(() => viewModel.ImportAsync("broken.json"));

        Assert.Equal("文件格式无效", viewModel.ErrorMessage);
        Assert.Equal(AppTheme.Light, viewModel.Theme);
    }

    [Fact]
    public async Task ImportSuccess_RefreshesEditableSettingsBeforeApply()
    {
        AppSettings imported = AppSettings.Defaults with
        {
            Theme = AppTheme.Dark,
            Opacity = 0.6,
            IsLocked = true,
            StartWithWindows = false
        };
        AppSettings applied = AppSettings.Defaults;
        var viewModel = new SettingsViewModel(
            AppSettings.Defaults,
            settings => { applied = settings; return Task.CompletedTask; },
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            () => Task.CompletedTask,
            () => imported);

        await viewModel.ImportAsync("import.json");
        await viewModel.ApplyAsync();

        Assert.Equal(AppTheme.Dark, viewModel.Theme);
        Assert.Equal(0.6, viewModel.Opacity);
        Assert.True(viewModel.IsLocked);
        Assert.False(viewModel.StartWithWindows);
        Assert.Equal(imported, applied);
    }

    private static SettingsViewModel CreateViewModel() => new(
        AppSettings.Defaults,
        _ => Task.CompletedTask,
        _ => Task.CompletedTask,
        _ => Task.CompletedTask,
        _ => Task.CompletedTask,
        () => Task.CompletedTask);

    private static string FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "TrainingDeskCalendar.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException(
            "Unable to locate TrainingDeskCalendar.sln from the test output directory.");
    }
}
