using System.Drawing;
using System.Windows.Forms;

namespace TrainingDeskCalendar.App.Windows;

internal enum TrayCommand
{
    Show,
    ToggleLock,
    ToggleStartup,
    OpenSettings,
    CheckUpdates,
    Exit
}

internal sealed record TrayState(bool IsVisible, bool IsLocked, bool StartWithWindows);
internal sealed record TrayMenuItem(TrayCommand Command, string Text);

internal static class TrayMenuModel
{
    public static IReadOnlyList<TrayMenuItem> Create(TrayState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return [
            new(TrayCommand.Show, "显示组件"),
            new(TrayCommand.ToggleLock, state.IsLocked ? "解锁组件" : "锁定组件"),
            new(TrayCommand.ToggleStartup, state.StartWithWindows ? "关闭开机自启动" : "开启开机自启动"),
            new(TrayCommand.OpenSettings, "打开设置"),
            new(TrayCommand.CheckUpdates, "手动检查更新"),
            new(TrayCommand.Exit, "退出程序")
        ];
    }
}

internal interface ITrayService : IDisposable
{
    void Start(TrayState state, Action<TrayCommand> execute);
    void Update(TrayState state);
}

internal sealed class TrayService : ITrayService
{
    private NotifyIcon? notifyIcon;
    private Action<TrayCommand>? execute;

    public void Start(TrayState state, Action<TrayCommand> execute)
    {
        ArgumentNullException.ThrowIfNull(state);
        this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
        notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = "训练桌历"
        };
        notifyIcon.DoubleClick += (_, _) => this.execute(TrayCommand.Show);
        BuildMenu(state);
    }

    public void Update(TrayState state)
    {
        if (notifyIcon is null)
        {
            return;
        }

        BuildMenu(state);
    }

    public void Dispose()
    {
        if (notifyIcon is null)
        {
            return;
        }

        notifyIcon.Visible = false;
        notifyIcon.Dispose();
        notifyIcon = null;
        execute = null;
    }

    private void BuildMenu(TrayState state)
    {
        if (notifyIcon is null)
        {
            return;
        }

        var menu = new ContextMenuStrip();
        foreach (TrayMenuItem item in TrayMenuModel.Create(state))
        {
            ToolStripMenuItem menuItem = new(item.Text);
            menuItem.Click += (_, _) => execute?.Invoke(item.Command);
            menu.Items.Add(menuItem);
        }

        ContextMenuStrip? previous = notifyIcon.ContextMenuStrip;
        notifyIcon.ContextMenuStrip = menu;
        previous?.Dispose();
    }
}
