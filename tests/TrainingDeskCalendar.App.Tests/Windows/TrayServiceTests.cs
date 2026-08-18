using TrainingDeskCalendar.App.Windows;
using Xunit;

namespace TrainingDeskCalendar.App.Tests.Windows;

public sealed class TrayServiceTests
{
    [Fact]
    public void MessageWindowLayout_IsHiddenTopLevelWindowThatReceivesBroadcasts()
    {
        TrayMessageWindowLayout layout = TrayMessageWindowLayout.CreateBroadcastReceiver();

        Assert.Equal(nint.Zero, layout.ParentWindow);
        Assert.Equal(0, layout.WindowStyle);
    }

    [Fact]
    public void ApplicationAssembly_DoesNotReferenceWindowsForms()
    {
        Assert.DoesNotContain(
            typeof(TrayService).Assembly.GetReferencedAssemblies(),
            assembly => assembly.Name == "System.Windows.Forms");
    }

    [Fact]
    public void ApplicationProject_EmbedsTheCalendarIcon()
    {
        string project = ReadRepositoryFile(
            "src",
            "TrainingDeskCalendar.App",
            "TrainingDeskCalendar.App.csproj");

        Assert.Contains(
            "<ApplicationIcon>Assets\\calendar.ico</ApplicationIcon>",
            project,
            StringComparison.Ordinal);
        Assert.True(File.Exists(ReadRepositoryPath(
            "src",
            "TrainingDeskCalendar.App",
            "Assets",
            "calendar.ico")));
    }

    [Fact]
    public void TrayService_LoadsTheEmbeddedApplicationIconFromItsModule()
    {
        string source = File.ReadAllText(ReadRepositoryPath(
            "src",
            "TrainingDeskCalendar.App",
            "Windows",
            "TrayService.cs"));

        Assert.Contains("GetModuleHandle(null)", source, StringComparison.Ordinal);
        Assert.Contains(
            "LoadIcon(GetModuleHandle(null), new nint(ApplicationIcon))",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void MenuModel_ContainsTheApprovedCommandsAndStateLabels()
    {
        IReadOnlyList<TrayMenuItem> items = TrayMenuModel.Create(
            new TrayState(IsVisible: true, IsLocked: false, StartWithWindows: true));

        Assert.Equal(
            [
                TrayCommand.Show,
                TrayCommand.ToggleLock,
                TrayCommand.ToggleStartup,
                TrayCommand.OpenSettings,
                TrayCommand.CheckUpdates,
                TrayCommand.Exit
            ],
            items.Select(item => item.Command));
        Assert.Contains(items, item => item.Text == "锁定组件");
        Assert.Contains(items, item => item.Text == "关闭开机自启动");
        Assert.Contains(items, item => item.Text == "显示组件");
    }

    [Fact]
    public void MenuModel_UsesUnlockAndShowLabelsWhenStateIsHiddenAndLocked()
    {
        IReadOnlyList<TrayMenuItem> items = TrayMenuModel.Create(
            new TrayState(IsVisible: false, IsLocked: true, StartWithWindows: false));

        Assert.Contains(items, item => item.Text == "显示组件");
        Assert.Contains(items, item => item.Text == "解锁组件");
        Assert.Contains(items, item => item.Text == "开启开机自启动");
    }

    [Fact]
    public void NativeMenuCommandMap_RoundTripsEachApprovedCommand()
    {
        IReadOnlyList<TrayMenuItem> items = TrayMenuModel.Create(
            new TrayState(IsVisible: true, IsLocked: false, StartWithWindows: true));
        TrayMenuCommandMap map = TrayMenuCommandMap.Create(items);

        Assert.Equal(items.Count, map.Items.Count);
        Assert.Equal(items.Count, map.Items.Select(item => item.Id).Distinct().Count());
        foreach (TrayMenuItem item in items)
        {
            TrayNativeMenuItem nativeItem = Assert.Single(
                map.Items,
                candidate => candidate.Command == item.Command);
            Assert.Equal(item.Text, nativeItem.Text);
            Assert.Equal(item.Command, map.Resolve(nativeItem.Id));
        }

        Assert.Null(map.Resolve(uint.MaxValue));
    }

    private static string ReadRepositoryPath(params string[] pathParts)
    {
        string? directory = AppContext.BaseDirectory;
        while (directory is not null &&
               !File.Exists(Path.Combine(directory, "TrainingDeskCalendar.sln")))
        {
            directory = Directory.GetParent(directory)?.FullName;
        }

        Assert.NotNull(directory);
        return Path.Combine(new[] { directory }.Concat(pathParts).ToArray());
    }

    private static string ReadRepositoryFile(params string[] pathParts) =>
        File.ReadAllText(ReadRepositoryPath(pathParts));
}
