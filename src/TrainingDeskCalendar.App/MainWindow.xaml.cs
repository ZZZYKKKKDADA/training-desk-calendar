using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TrainingDeskCalendar.App.Desktop;
using TrainingDeskCalendar.App.Diagnostics;

namespace TrainingDeskCalendar.App;

public partial class MainWindow : Window
{
    private readonly DesktopHostService desktopHostService =
        new(new Win32DesktopWindowApi());
    private readonly DispatcherTimer desktopWatchdog;
    private readonly uint taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");
    private nint windowHandle;

    public MainWindow()
    {
        InitializeComponent();
        Days = CreateDays();
        DataContext = this;
        desktopWatchdog = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        desktopWatchdog.Tick += (_, _) => AttachToDesktop();
        SourceInitialized += OnSourceInitialized;
        ContentRendered += OnContentRendered;
        Closed += (_, _) => desktopWatchdog.Stop();
    }

    public ObservableCollection<PrototypeDay> Days { get; }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var helper = new WindowInteropHelper(this);
        windowHandle = helper.Handle;
        HwndSource.FromHwnd(windowHandle)?.AddHook(WindowMessageHook);
        AttachToDesktop();
        desktopWatchdog.Start();
    }

    private void OnContentRendered(object? sender, EventArgs e)
    {
        PrototypeReadySignal.Write(App.ReadyFilePath);

        if (App.ExitAfter is TimeSpan delay)
        {
            var timer = new DispatcherTimer { Interval = delay };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                Application.Current.Shutdown();
            };
            timer.Start();
        }
    }

    private nint WindowMessageHook(
        nint hwnd,
        int message,
        nint wParam,
        nint lParam,
        ref bool handled)
    {
        if ((uint)message == taskbarCreatedMessage)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.ApplicationIdle,
                new Action(AttachToDesktop));
        }

        return nint.Zero;
    }

    private void AttachToDesktop()
    {
        DesktopAttachResult result = desktopHostService.Attach(windowHandle);
        DesktopStatusText.Text = result.Status == DesktopAttachStatus.Attached
            ? "Desktop host: attached"
            : $"Desktop host: fallback · {result.FailureReason}";
    }

    private void OnHeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private static ObservableCollection<PrototypeDay> CreateDays()
    {
        string[] colors =
        [
            "#BFE3DA", "#D5DADF", "#C7D8F2", "#BFE3DA", "#D9C7E8", "#F1C2C2", "#D5DADF",
            "#BFE3DA", "#D5DADF", "#C7D8F2", "#BFE3DA", "#F4D1A6", "#D9C7E8", "#D5DADF"
        ];
        string[] plans =
        [
            "胸部训练\n卧推 4 × 8", "恢复日\n步行 30 分钟", "慢跑 5 km", "背部训练",
            "核心训练", "腿部训练", "完全休息", "肩部训练", "泡沫轴", "间歇跑",
            "硬拉 4 × 6", "灵活性训练", "全身循环", "完全休息"
        ];
        string[] weekdays = ["周一", "周二", "周三", "周四", "周五", "周六", "周日"];

        return new ObservableCollection<PrototypeDay>(
            Enumerable.Range(0, 14).Select(index => new PrototypeDay(
                weekdays[index % 7],
                17 + index,
                plans[index],
                (Brush)new BrushConverter().ConvertFromString(colors[index])!,
                index == 0)));
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterWindowMessage(string messageName);
}
