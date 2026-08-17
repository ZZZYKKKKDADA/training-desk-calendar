using System.IO;
using Microsoft.Win32;
using TrainingDeskCalendar.App.Persistence;

namespace TrainingDeskCalendar.App.Windows;

internal sealed class StartupRegistration : IStartupRegistration
{
    internal const string RunKeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    internal const string ValueName = "TrainingDeskCalendar";

    private readonly string executablePath;
    private readonly IUserStartupStore store;

    public StartupRegistration(
        string executablePath,
        IUserStartupStore? store = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        if (!Path.IsPathFullyQualified(executablePath))
        {
            throw new ArgumentException("The executable path must be absolute.", nameof(executablePath));
        }

        this.executablePath = executablePath;
        this.store = store ?? new RegistryStartupStore();
    }

    public bool IsEnabled
    {
        get
        {
            try
            {
                string? value = store.GetValue(RunKeyPath, ValueName);
                return string.Equals(
                    value?.Trim().Trim('"'),
                    executablePath,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }

    public void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            store.SetValue(RunKeyPath, ValueName, $"\"{executablePath}\"");
        }
        else
        {
            store.DeleteValue(RunKeyPath, ValueName);
        }
    }

    private sealed class RegistryStartupStore : IUserStartupStore
    {
        public string? GetValue(string path, string name)
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(path, writable: false);
            return key?.GetValue(name) as string;
        }

        public void SetValue(string path, string name, string value)
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(path, writable: true)
                ?? throw new InvalidOperationException("The current-user startup key could not be opened.");
            key.SetValue(name, value, RegistryValueKind.String);
        }

        public void DeleteValue(string path, string name)
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(path, writable: true);
            key?.DeleteValue(name, throwOnMissingValue: false);
        }
    }
}

internal sealed class StartupSettingsCoordinator(IStartupRegistration registration)
{
    public async Task<AppSettings> SetEnabledAsync(
        AppSettings current,
        bool enabled,
        Func<AppSettings, Task> saveSettings)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(saveSettings);
        bool previous = registration.IsEnabled;
        AppSettings updated = current with { StartWithWindows = enabled };

        registration.SetEnabled(enabled);
        try
        {
            await saveSettings(updated);
            return updated;
        }
        catch (Exception saveException)
        {
            try
            {
                registration.SetEnabled(previous);
            }
            catch (Exception rollbackException)
            {
                throw new AggregateException(
                    "Startup registration changed, but settings persistence and rollback both failed.",
                    saveException,
                    rollbackException);
            }

            throw;
        }
    }
}
