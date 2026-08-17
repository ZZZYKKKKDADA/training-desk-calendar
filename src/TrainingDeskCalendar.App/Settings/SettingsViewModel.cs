using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using TrainingDeskCalendar.App.Persistence;

namespace TrainingDeskCalendar.App.Settings;

internal sealed class SettingsViewModel : INotifyPropertyChanged
{
    private readonly Func<AppSettings, Task> saveSettings;
    private readonly Func<bool, Task> setStartup;
    private readonly Func<string, Task> exportData;
    private readonly Func<string, Task> importData;
    private readonly Func<Task> checkUpdates;
    private readonly Func<AppSettings>? currentSettings;
    private AppSettings baseline;
    private AppTheme theme;
    private double opacity;
    private bool isLocked;
    private bool startWithWindows;
    private string? errorMessage;
    private string? statusMessage;

    public SettingsViewModel(
        AppSettings settings,
        Func<AppSettings, Task> saveSettings,
        Func<bool, Task> setStartup,
        Func<string, Task> exportData,
        Func<string, Task> importData,
        Func<Task> checkUpdates,
        Func<AppSettings>? currentSettings = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        baseline = settings.Validate();
        this.saveSettings = saveSettings ?? throw new ArgumentNullException(nameof(saveSettings));
        this.setStartup = setStartup ?? throw new ArgumentNullException(nameof(setStartup));
        this.exportData = exportData ?? throw new ArgumentNullException(nameof(exportData));
        this.importData = importData ?? throw new ArgumentNullException(nameof(importData));
        this.checkUpdates = checkUpdates ?? throw new ArgumentNullException(nameof(checkUpdates));
        this.currentSettings = currentSettings;
        theme = settings.Theme;
        opacity = settings.Opacity;
        isLocked = settings.IsLocked;
        startWithWindows = settings.StartWithWindows;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AppTheme Theme
    {
        get => theme;
        set
        {
            if (!Enum.IsDefined(value)) throw new InvalidDataException("Theme is invalid.");
            if (theme == value) return;
            theme = value;
            OnPropertyChanged();
        }
    }

    public double Opacity
    {
        get => opacity;
        set
        {
            if (!double.IsFinite(value) || value is < 0.4 or > 1.0)
            {
                throw new InvalidDataException("Opacity must be between 0.4 and 1.0.");
            }

            if (Math.Abs(opacity - value) < double.Epsilon) return;
            opacity = value;
            OnPropertyChanged();
        }
    }

    public bool IsLocked
    {
        get => isLocked;
        set { if (isLocked == value) return; isLocked = value; OnPropertyChanged(); }
    }

    public bool StartWithWindows
    {
        get => startWithWindows;
        private set { if (startWithWindows == value) return; startWithWindows = value; OnPropertyChanged(); }
    }

    public string? ErrorMessage
    {
        get => errorMessage;
        private set { if (errorMessage == value) return; errorMessage = value; OnPropertyChanged(); }
    }

    public string? StatusMessage
    {
        get => statusMessage;
        private set { if (statusMessage == value) return; statusMessage = value; OnPropertyChanged(); }
    }

    public string VersionText => $"版本 {typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3) ?? "开发版"}";

    public async Task ApplyAsync()
    {
        try
        {
            AppSettings updated = CreateSettings();
            await saveSettings(updated);
            ErrorMessage = null;
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            throw;
        }
    }

    public async Task SetStartWithWindowsAsync(bool enabled)
    {
        try
        {
            await setStartup(enabled);
            StartWithWindows = enabled;
            ErrorMessage = null;
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            throw;
        }
    }

    public void ResetWindow()
    {
        AppSettings defaults = AppSettings.Defaults;
        baseline = baseline with
        {
            WindowX = defaults.WindowX,
            WindowY = defaults.WindowY,
            WindowWidth = defaults.WindowWidth,
            WindowHeight = defaults.WindowHeight,
            MonitorId = defaults.MonitorId
        };
    }

    public Task ExportAsync(string path) => RunDataActionAsync(() => exportData(path));
    public async Task ImportAsync(string path)
    {
        await RunDataActionAsync(() => importData(path));
        if (currentSettings is not null)
        {
            LoadSettings(currentSettings());
        }
    }

    public async Task CheckUpdatesAsync()
    {
        await RunDataActionAsync(checkUpdates);
        StatusMessage = "更新检查将在阶段 3 提供。";
    }

    private AppSettings CreateSettings() => baseline with
    {
        Theme = Theme,
        Opacity = Opacity,
        IsLocked = IsLocked,
        StartWithWindows = StartWithWindows
    };

    private async Task RunDataActionAsync(Func<Task> action)
    {
        try
        {
            await action();
            ErrorMessage = null;
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            throw;
        }
    }

    private void LoadSettings(AppSettings settings)
    {
        baseline = settings.Validate();
        Theme = settings.Theme;
        Opacity = settings.Opacity;
        IsLocked = settings.IsLocked;
        StartWithWindows = settings.StartWithWindows;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
