using System.Windows;
using System.Windows.Threading;
using TrainingDeskCalendar.App.Diagnostics;
using TrainingDeskCalendar.App.Domain;
using TrainingDeskCalendar.App.Persistence;
using TrainingDeskCalendar.App.Services;
using TrainingDeskCalendar.App.Windows;

namespace TrainingDeskCalendar.App;

public partial class App : System.Windows.Application
{
    internal static string? ReadyFilePath { get; private set; }
    internal static TimeSpan? ExitAfter { get; private set; }
    internal static string? DataRoot { get; private set; }
    internal static string? SaveLatencyFilePath { get; private set; }
    internal static int SaveLatencySamples { get; private set; }
    private AppComposition? composition;
    private AppSingleInstance? singleInstance;
    private ITrayService? trayService;
    private readonly AsyncOnce exitOnce = new();

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        singleInstance = new AppSingleInstance();
        if (!singleInstance.TryAcquire("TrainingDeskCalendar", ExistingInstanceNotifier.Show))
        {
            Shutdown();
            return;
        }

        DiagnosticRunOptions options = DiagnosticRunOptions.Parse(e.Args);
        ReadyFilePath = options.ReadyFile;
        ExitAfter = options.ExitAfter;
        DataRoot = options.DataRoot;
        SaveLatencyFilePath = options.SaveLatencyFile;
        SaveLatencySamples = options.SaveLatencySamples;

        try
        {
            AppDataPaths dataPaths = string.IsNullOrWhiteSpace(DataRoot)
                ? AppDataPaths.ForCurrentUser()
                : AppDataPaths.ForRoot(DataRoot);
            composition = await AppComposition.CreateAsync(
                dataPaths,
                DateOnly.FromDateTime(DateTime.Today));
            try
            {
                composition.StartupRegistration.SetEnabled(composition.Settings.StartWithWindows);
            }
            catch
            {
                // Startup registration is optional and must not block local use.
            }
            var window = new MainWindow(composition);
            MainWindow = window;
            window.Show();
            trayService = new TrayService();
            trayService.Start(
                new TrayState(true, composition.Settings.IsLocked, composition.Settings.StartWithWindows),
                ExecuteTrayCommand);
            _ = Dispatcher.BeginInvoke(
                DispatcherPriority.ApplicationIdle,
                new Action(() => _ = CheckForUpdatesAutomaticallyAsync()));
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"训练桌历无法启动：{exception.Message}",
                "训练桌历",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            if (composition is not null)
            {
                try
                {
                    await composition.DisposeAsync();
                }
                catch
                {
                    // The startup error remains the primary failure shown to the user.
                }
            }
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        trayService?.Dispose();
        singleInstance?.Dispose();

        base.OnExit(e);
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        e.Cancel = true;
        _ = RequestExitAsync();
        base.OnSessionEnding(e);
    }

    private async void ExecuteTrayCommand(TrayCommand command)
    {
        try
        {
            await ExecuteTrayCommandAsync(command);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                exception.Message,
                "训练桌历",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async Task ExecuteTrayCommandAsync(TrayCommand command)
    {
        if (MainWindow is not MainWindow window || composition is null) return;

        switch (command)
        {
            case TrayCommand.Show:
                window.WindowState = WindowState.Normal;
                window.Show();
                window.Activate();
                break;
            case TrayCommand.ToggleLock:
                await window.SetLockedAsync(!window.IsLocked);
                break;
            case TrayCommand.ToggleStartup:
                await composition.SetStartWithWindowsAsync(!composition.Settings.StartWithWindows);
                break;
            case TrayCommand.OpenSettings:
                window.OpenSettings();
                break;
            case TrayCommand.CheckUpdates:
                await composition.UpdateCheckCoordinator.CheckAsync(UpdateCheckMode.Manual);
                break;
            case TrayCommand.Exit:
                await RequestExitAsync();
                break;
        }

        trayService?.Update(new TrayState(window.IsVisible, window.IsLocked, composition.Settings.StartWithWindows));
    }

    internal Task RequestExitAsync() => exitOnce.Run(RequestExitCoreAsync);

    private async Task CheckForUpdatesAutomaticallyAsync()
    {
        try
        {
            if (composition is not null)
            {
                await composition.UpdateCheckCoordinator.CheckAsync(UpdateCheckMode.Automatic);
            }
        }
        catch
        {
            // Automatic update checks must never interrupt local calendar use.
        }
    }

    private async Task RequestExitCoreAsync()
    {
        if (composition is not null)
        {
            await composition.DisposeAsync();
        }

        trayService?.Dispose();
        trayService = null;
        if (MainWindow is MainWindow window)
        {
            window.AllowExplicitClose();
            window.Close();
        }

        Shutdown();
    }
}
