using TrainingDeskCalendar.App.Windows;
using Xunit;

namespace TrainingDeskCalendar.App.Tests.Windows;

public sealed class TrayServiceTests
{
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
}
