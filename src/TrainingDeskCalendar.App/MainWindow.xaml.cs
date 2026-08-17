using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TrainingDeskCalendar.App.Calendar;
using TrainingDeskCalendar.App.Domain;
using TrainingDeskCalendar.App.Desktop;
using TrainingDeskCalendar.App.Diagnostics;
using TrainingDeskCalendar.App.Persistence;
using TrainingDeskCalendar.App.Services;
using TrainingDeskCalendar.App.Windowing;

namespace TrainingDeskCalendar.App;

public partial class MainWindow : Window
{
    private readonly AppComposition composition;
    private readonly DesktopHostService desktopHostService = new(new Win32DesktopWindowApi());
    private readonly DispatcherTimer desktopWatchdog;
    private readonly DispatcherTimer settingsSaveTimer;
    private readonly WindowInteractionState interactionState = new();
    private readonly WindowStateService windowStateService = new();
    private readonly uint taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");
    private WindowPlacementCoordinator? placementCoordinator;
    private nint windowHandle;

    internal MainWindow(AppComposition composition)
    {
        this.composition = composition ?? throw new ArgumentNullException(nameof(composition));
        InitializeComponent();
        DataContext = composition.Calendar;
        ApplySavedWindowState();
        interactionState.SetLocked(composition.Settings.IsLocked);
        ApplyAppearance();
        desktopWatchdog = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        desktopWatchdog.Tick += OnDesktopWatchdogTick;
        settingsSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        settingsSaveTimer.Tick += OnSettingsSaveTimerTick;
        LocationChanged += OnWindowGeometryChanged;
        SizeChanged += OnWindowGeometryChanged;
        SourceInitialized += OnSourceInitialized;
        ContentRendered += OnContentRendered;
        Closed += OnClosed;
    }

    private async void OnContentRendered(object? sender, EventArgs e)
    {
        try
        {
            await composition.Calendar.LoadAsync();
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
        catch (Exception exception)
        {
            DesktopStatusText.Text = $"数据加载失败：{exception.Message}";
        }
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var helper = new WindowInteropHelper(this);
        windowHandle = helper.Handle;
        placementCoordinator = new WindowPlacementCoordinator(
            this,
            windowHandle,
            new Win32MonitorWorkAreaReader(),
            new WindowPlacementService());
        HwndSource.FromHwnd(windowHandle)?.AddHook(WindowMessageHook);
        AttachToDesktop();
        placementCoordinator.EnsureVisible();
        desktopWatchdog.Start();
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        desktopWatchdog.Stop();
        settingsSaveTimer.Stop();
        try
        {
            await composition.DisposeAsync();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, $"退出前保存失败：{exception.Message}", "训练桌历", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private nint WindowMessageHook(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if ((uint)message == taskbarCreatedMessage)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(AttachToDesktop));
        }

        return nint.Zero;
    }

    private void AttachToDesktop()
    {
        DesktopAttachResult result = desktopHostService.Attach(windowHandle);
        DesktopStatusText.Text = result.Status == DesktopAttachStatus.Attached
            ? "桌面层：已连接"
            : $"桌面层：普通窗口 · {result.FailureReason}";
    }

    private void OnDesktopWatchdogTick(object? sender, EventArgs e)
    {
        AttachToDesktop();
        placementCoordinator?.EnsureVisible();
    }

    private void OnHeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (interactionState.CanMove && e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OnCardMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is TextBox or Button or CheckBox)
        {
            return;
        }

        if (sender is FrameworkElement element && element.DataContext is DayCardViewModel card)
        {
            composition.Calendar.BeginEdit(card);
        }
    }

    private async void OnCompletedClick(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.DataContext is DayCardViewModel card)
        {
            await composition.Calendar.SetCompletedAsync(card, checkBox.IsChecked == true);
        }
    }

    private void OnColorClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is DayCardViewModel card && int.TryParse(button.Tag?.ToString(), out int colorId))
        {
            card.SelectColor((TaskColorId)colorId);
        }
    }

    private async void OnSaveCardClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is DayCardViewModel card)
        {
            await composition.Calendar.SaveEditAsync(card);
        }
    }

    private async void OnPreviousClick(object sender, RoutedEventArgs e) => await composition.Calendar.PreviousAsync();
    private async void OnTodayClick(object sender, RoutedEventArgs e) => await composition.Calendar.GoToTodayAsync();
    private async void OnNextClick(object sender, RoutedEventArgs e) => await composition.Calendar.NextAsync();

    private void OnSettingsClick(object sender, RoutedEventArgs e) =>
        MessageBox.Show(this, "设置窗口将在下一项任务中启用。", "训练桌历", MessageBoxButton.OK, MessageBoxImage.Information);

    private void OnWindowGeometryChanged(object? sender, EventArgs e)
    {
        if (placementCoordinator is null || e is SizeChangedEventArgs && !interactionState.CanResize)
        {
            return;
        }

        placementCoordinator.TrackCurrentMonitor();
        settingsSaveTimer.Stop();
        settingsSaveTimer.Start();
    }

    private async void OnSettingsSaveTimerTick(object? sender, EventArgs e)
    {
        settingsSaveTimer.Stop();
        if (placementCoordinator is null)
        {
            return;
        }

        try
        {
            WindowPlacement current = new(
                placementCoordinator.CurrentMonitorId,
                Left,
                Top,
                ActualWidth,
                ActualHeight);
            AppSettings updated = windowStateService.WithPlacement(composition.Settings, current);
            await composition.SaveSettingsAsync(updated);
        }
        catch (Exception exception)
        {
            DesktopStatusText.Text = $"窗口状态保存失败：{exception.Message}";
        }
    }

    private void ApplySavedWindowState()
    {
        AppSettings settings = composition.Settings;
        Left = settings.WindowX;
        Top = settings.WindowY;
        Width = settings.WindowWidth;
        Height = settings.WindowHeight;
        ResizeMode = settings.IsLocked ? ResizeMode.NoResize : ResizeMode.CanResizeWithGrip;
    }

    private void ApplyAppearance()
    {
        AppearancePalette palette = AppearancePalette.Create(
            composition.Settings.Theme,
            composition.Settings.Opacity);
        Color surface = (Color)ColorConverter.ConvertFromString(palette.SurfaceHex)!;
        surface.A = (byte)Math.Round(palette.Opacity * 255);
        SurfaceBorder.Background = new SolidColorBrush(surface);
        SurfaceBorder.BorderBrush = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(palette.BorderHex)!);
        DesktopStatusText.Foreground = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(palette.ForegroundHex)!);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterWindowMessage(string messageName);
}
