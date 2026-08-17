using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;

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
internal sealed record TrayNativeMenuItem(uint Id, TrayCommand Command, string Text);
internal readonly record struct TrayMessageWindowLayout(nint ParentWindow, int WindowStyle)
{
    public static TrayMessageWindowLayout CreateBroadcastReceiver() => new(nint.Zero, 0);
}

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

internal sealed class TrayMenuCommandMap
{
    private const uint FirstCommandId = 1001;
    private readonly IReadOnlyDictionary<uint, TrayCommand> commands;

    private TrayMenuCommandMap(IReadOnlyList<TrayNativeMenuItem> items)
    {
        Items = items;
        commands = items.ToDictionary(item => item.Id, item => item.Command);
    }

    public IReadOnlyList<TrayNativeMenuItem> Items { get; }

    public static TrayMenuCommandMap Create(IReadOnlyList<TrayMenuItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return new TrayMenuCommandMap(items
            .Select((item, index) => new TrayNativeMenuItem(
                FirstCommandId + (uint)index,
                item.Command,
                item.Text))
            .ToArray());
    }

    public TrayCommand? Resolve(uint id) =>
        commands.TryGetValue(id, out TrayCommand command) ? command : null;
}

internal interface ITrayService : IDisposable
{
    void Start(TrayState state, Action<TrayCommand> execute);
    void Update(TrayState state);
}

internal sealed class TrayService : ITrayService
{
    private const int CallbackMessage = 0x8001;
    private const int WindowLeftButtonDoubleClick = 0x0203;
    private const int WindowRightButtonUp = 0x0205;
    private const int WindowContextMenu = 0x007B;
    private const uint NotifyMessage = 0x00000001;
    private const uint NotifyIcon = 0x00000002;
    private const uint NotifyTip = 0x00000004;
    private const uint NotifyAdd = 0x00000000;
    private const uint NotifyDelete = 0x00000002;
    private const uint MenuString = 0x00000000;
    private const uint MenuSeparator = 0x00000800;
    private const uint TrackRightButton = 0x0002;
    private const uint TrackReturnCommand = 0x0100;
    private const uint TrackNoNotify = 0x0080;
    private const uint WindowNull = 0x0000;
    private const int ApplicationIcon = 32512;

    private readonly uint taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");
    private HwndSource? messageWindow;
    private Action<TrayCommand>? execute;
    private TrayState? state;
    private nint iconHandle;
    private bool iconAdded;

    public void Start(TrayState state, Action<TrayCommand> execute)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (messageWindow is not null)
        {
            throw new InvalidOperationException("The tray service has already started.");
        }

        this.state = state;
        this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
        TrayMessageWindowLayout layout = TrayMessageWindowLayout.CreateBroadcastReceiver();
        var parameters = new HwndSourceParameters("TrainingDeskCalendar.Tray")
        {
            ParentWindow = layout.ParentWindow,
            Width = 0,
            Height = 0,
            WindowStyle = layout.WindowStyle
        };
        messageWindow = new HwndSource(parameters);
        messageWindow.AddHook(WindowHook);
        iconHandle = LoadIcon(nint.Zero, new nint(ApplicationIcon));
        if (iconHandle == nint.Zero)
        {
            Dispose();
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        AddIcon();
    }

    public void Update(TrayState state) =>
        this.state = state ?? throw new ArgumentNullException(nameof(state));

    public void Dispose()
    {
        if (messageWindow is not null && iconAdded)
        {
            NotifyIconData data = CreateIconData(messageWindow.Handle);
            _ = ShellNotifyIcon(NotifyDelete, ref data);
            iconAdded = false;
        }

        messageWindow?.Dispose();
        messageWindow = null;
        execute = null;
        state = null;
        iconHandle = nint.Zero;
    }

    private void AddIcon()
    {
        if (messageWindow is null)
        {
            return;
        }

        NotifyIconData data = CreateIconData(messageWindow.Handle);
        if (!ShellNotifyIcon(NotifyAdd, ref data))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        iconAdded = true;
    }

    private NotifyIconData CreateIconData(nint windowHandle) => new()
    {
        Size = (uint)Marshal.SizeOf<NotifyIconData>(),
        WindowHandle = windowHandle,
        Id = 1,
        Flags = NotifyMessage | NotifyIcon | NotifyTip,
        CallbackMessage = CallbackMessage,
        IconHandle = iconHandle,
        Tip = "训练桌历",
        Info = string.Empty,
        InfoTitle = string.Empty
    };

    private nint WindowHook(
        nint window,
        int message,
        nint wParam,
        nint lParam,
        ref bool handled)
    {
        if ((uint)message == taskbarCreatedMessage)
        {
            iconAdded = false;
            AddIcon();
            return nint.Zero;
        }

        if (message != CallbackMessage)
        {
            return nint.Zero;
        }

        int notification = unchecked((int)lParam.ToInt64());
        if (notification == WindowLeftButtonDoubleClick)
        {
            execute?.Invoke(TrayCommand.Show);
            handled = true;
        }
        else if (notification is WindowRightButtonUp or WindowContextMenu)
        {
            ShowContextMenu(window);
            handled = true;
        }

        return nint.Zero;
    }

    private void ShowContextMenu(nint window)
    {
        if (state is null)
        {
            return;
        }

        TrayMenuCommandMap map = TrayMenuCommandMap.Create(TrayMenuModel.Create(state));
        nint menu = CreatePopupMenu();
        if (menu == nint.Zero)
        {
            return;
        }

        try
        {
            foreach (TrayNativeMenuItem item in map.Items)
            {
                if (item.Command == TrayCommand.Exit)
                {
                    _ = AppendMenu(menu, MenuSeparator, 0, null);
                }

                _ = AppendMenu(menu, MenuString, item.Id, item.Text);
            }

            _ = GetCursorPos(out Point point);
            _ = SetForegroundWindow(window);
            uint selected = TrackPopupMenu(
                menu,
                TrackRightButton | TrackReturnCommand | TrackNoNotify,
                point.X,
                point.Y,
                0,
                window,
                nint.Zero);
            _ = PostMessage(window, WindowNull, nint.Zero, nint.Zero);
            TrayCommand? command = map.Resolve(selected);
            if (command is not null)
            {
                execute?.Invoke(command.Value);
            }
        }
        finally
        {
            _ = DestroyMenu(menu);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint Size;
        public nint WindowHandle;
        public uint Id;
        public uint Flags;
        public uint CallbackMessage;
        public nint IconHandle;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State;
        public uint StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint TimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
        public uint InfoFlags;
        public Guid ItemGuid;
        public nint BalloonIconHandle;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct Point
    {
        public readonly int X;
        public readonly int Y;
    }

    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShellNotifyIcon(uint message, ref NotifyIconData data);

    [DllImport("user32.dll", EntryPoint = "LoadIconW", SetLastError = true)]
    private static extern nint LoadIcon(nint instance, nint iconName);

    [DllImport("user32.dll", EntryPoint = "CreatePopupMenu", SetLastError = true)]
    private static extern nint CreatePopupMenu();

    [DllImport("user32.dll", EntryPoint = "AppendMenuW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AppendMenu(nint menu, uint flags, uint id, string? text);

    [DllImport("user32.dll", EntryPoint = "TrackPopupMenuEx", SetLastError = true)]
    private static extern uint TrackPopupMenu(
        nint menu,
        uint flags,
        int x,
        int y,
        nint reserved,
        nint window,
        nint parameters);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyMenu(nint menu);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint window);

    [DllImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(nint window, uint message, nint wParam, nint lParam);

    [DllImport("user32.dll", EntryPoint = "RegisterWindowMessageW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterWindowMessage(string messageName);
}
