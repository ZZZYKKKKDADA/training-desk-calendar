using System.Runtime.InteropServices;
using System.ComponentModel;
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
using TrainingDeskCalendar.App.Settings;
using TrainingDeskCalendar.App.Services;
using TrainingDeskCalendar.App.Windowing;

namespace TrainingDeskCalendar.App;

public partial class MainWindow : Window
{
    private readonly AppComposition composition;
    private readonly DesktopHostService desktopHostService = new(new Win32DesktopWindowApi());
    private readonly DispatcherTimer settingsSaveTimer;
    private readonly WindowInteractionState interactionState = new();
    private readonly WindowClosePolicy closePolicy = new();
    private readonly WindowDragService windowDragService = new();
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
        settingsSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        settingsSaveTimer.Tick += OnSettingsSaveTimerTick;
        LocationChanged += OnWindowGeometryChanged;
        SizeChanged += OnWindowGeometryChanged;
        SourceInitialized += OnSourceInitialized;
        ContentRendered += OnContentRendered;
        Closing += OnClosing;
        Closed += OnClosed;
    }

    private async void OnContentRendered(object? sender, EventArgs e)
    {
        try
        {
            await composition.Calendar.LoadAsync();
            PrototypeReadySignal.Write(App.ReadyFilePath);
            if (!string.IsNullOrWhiteSpace(App.SaveLatencyFilePath) &&
                App.SaveLatencySamples > 0)
            {
                await AutosaveLatencyProbe.WriteAsync(
                    App.SaveLatencyFilePath,
                    App.SaveLatencySamples,
                    sample => composition.Autosave.QueueAsync(
                        TrainingPlan.Create(
                            DateOnly.FromDateTime(DateTime.Today).AddDays(sample),
                            $"性能探针 {sample}",
                            TaskColorId.Gray)));
            }
            if (App.ExitAfter is TimeSpan delay)
            {
                var timer = new DispatcherTimer { Interval = delay };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    if (Application.Current is App app)
                    {
                        _ = app.RequestExitAsync();
                    }
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
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!closePolicy.ShouldHide)
        {
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        settingsSaveTimer.Stop();
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
            : "桌面层：普通窗口（桌面嵌入不可用）";
    }

    private async void OnWindowPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DayCardViewModel? editingCard = composition.Calendar.Days
            .FirstOrDefault(card => card.IsEditing);
        if (editingCard is null || IsWithinCard(e.OriginalSource as DependencyObject, editingCard))
        {
            return;
        }

        await UiCommandRunner.RunAsync(
            () => composition.Calendar.SaveEditAsync(editingCard),
            exception => DesktopStatusText.Text = $"保存失败：{exception.Message}");
    }

    private static bool IsWithinCard(DependencyObject? source, DayCardViewModel card)
    {
        while (source is not null)
        {
            if (source is FrameworkElement element &&
                ReferenceEquals(element.DataContext, card))
            {
                return true;
            }

            source = source switch
            {
                Visual visual => VisualTreeHelper.GetParent(visual),
                FrameworkContentElement content => ContentOperations.GetParent(content),
                _ => LogicalTreeHelper.GetParent(source)
            };
        }

        return false;
    }

    private void OnHeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (interactionState.CanMove &&
            e.ButtonState == MouseButtonState.Pressed &&
            sender is UIElement header &&
            header.CaptureMouse())
        {
            windowDragService.Begin(
                GetPointerPositionInDips(e),
                new Point(Left, Top));
            e.Handled = true;
        }
    }

    private void OnHeaderMouseMove(object sender, MouseEventArgs e)
    {
        if (!interactionState.CanMove || e.LeftButton != MouseButtonState.Pressed)
        {
            EndHeaderDrag(sender);
            return;
        }

        if (windowDragService.TryGetPosition(
                GetPointerPositionInDips(e),
                out Point windowPosition))
        {
            Left = windowPosition.X;
            Top = windowPosition.Y;
            e.Handled = true;
        }
    }

    private void OnHeaderMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        EndHeaderDrag(sender);
        e.Handled = true;
    }

    private void OnHeaderLostMouseCapture(object sender, MouseEventArgs e) =>
        windowDragService.End();

    private Point GetPointerPositionInDips(MouseEventArgs e)
    {
        Point screenPosition = PointToScreen(e.GetPosition(this));
        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        return new Point(
            screenPosition.X / dpi.DpiScaleX,
            screenPosition.Y / dpi.DpiScaleY);
    }

    private void EndHeaderDrag(object sender)
    {
        windowDragService.End();
        if (sender is UIElement element && element.IsMouseCaptured)
        {
            element.ReleaseMouseCapture();
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
        if (sender is FrameworkElement element && element.DataContext is DayCardViewModel card && int.TryParse(element.Tag?.ToString(), out int colorId))
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
    private async void OnNextClick(object sender, RoutedEventArgs e) => await composition.Calendar.NextAsync();
    private async void OnLockClick(object sender, RoutedEventArgs e) =>
        await UiCommandRunner.RunAsync(
            () => SetLockedAsync(!IsLocked),
            exception => DesktopStatusText.Text = $"锁定状态保存失败：{exception.Message}");
    private void OnHideClick(object sender, RoutedEventArgs e) => Hide();

    private void OnSettingsClick(object sender, RoutedEventArgs e) =>
        OpenSettings();

    internal bool IsLocked => interactionState.IsLocked;

    internal void AllowExplicitClose() => closePolicy.RequestExit();

    internal void OpenSettings()
    {
        var viewModel = new SettingsViewModel(
            composition.Settings,
            SaveSettingsAsync,
            SetStartupAsync,
            ExportDataAsync,
            ImportDataAsync,
            () => composition.UpdateCheckCoordinator.CheckAsync(UpdateCheckMode.Manual),
            () => composition.Settings,
            composition.BuildMetadata.Repository,
            composition.UriLauncher.Open);
        var window = new SettingsWindow(viewModel) { Owner = this };
        window.ShowDialog();
        interactionState.SetLocked(composition.Settings.IsLocked);
        ApplySavedWindowState();
        ApplyAppearance();
    }

    internal async Task SetLockedAsync(bool locked)
    {
        await SaveSettingsAsync(composition.Settings with { IsLocked = locked });
        ResizeMode = locked ? ResizeMode.NoResize : ResizeMode.CanResizeWithGrip;
    }

    private async Task SaveSettingsAsync(AppSettings settings)
    {
        await composition.SaveSettingsAsync(settings);
        interactionState.SetLocked(settings.IsLocked);
        ApplyAppearance();
    }

    private async Task SetStartupAsync(bool enabled)
    {
        await composition.SetStartWithWindowsAsync(enabled);
    }

    private async Task ExportDataAsync(string path)
    {
        await composition.Calendar.FlushAsync();
        await composition.TransferService.ExportAsync(path);
    }

    private async Task ImportDataAsync(string path)
    {
        await composition.ImportAsync(path);
        interactionState.SetLocked(composition.Settings.IsLocked);
        ApplySavedWindowState();
        ApplyAppearance();
    }

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
