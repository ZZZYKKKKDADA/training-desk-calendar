using TrainingDeskCalendar.App.Windows;
using TrainingDeskCalendar.App.Persistence;
using Xunit;

namespace TrainingDeskCalendar.App.Tests.Windows;

public sealed class StartupRegistrationTests
{
    [Fact]
    public void Enable_WritesAQuotedCurrentUserCommand()
    {
        var store = new FakeStartupStore();
        var registration = new StartupRegistration(
            "C:\\Users\\tester\\TrainingDeskCalendar.App.exe",
            store);

        registration.SetEnabled(true);

        Assert.True(registration.IsEnabled);
        Assert.Equal(
            "\"C:\\Users\\tester\\TrainingDeskCalendar.App.exe\"",
            store.Values[StartupRegistration.ValueName]);
        Assert.Equal(StartupRegistration.RunKeyPath, store.LastPath);
    }

    [Fact]
    public void Disable_RemovesTheCurrentUserCommand()
    {
        var store = new FakeStartupStore();
        var registration = new StartupRegistration(
            "C:\\Users\\tester\\TrainingDeskCalendar.App.exe",
            store);
        registration.SetEnabled(true);

        registration.SetEnabled(false);

        Assert.False(registration.IsEnabled);
        Assert.DoesNotContain(StartupRegistration.ValueName, store.Values.Keys);
    }

    [Fact]
    public void RelativeExecutablePath_IsRejected()
    {
        var store = new FakeStartupStore();

        Assert.Throws<ArgumentException>(() =>
            new StartupRegistration("TrainingDeskCalendar.App.exe", store));
    }

    [Fact]
    public async Task SettingsCoordinator_RollsBackRegistrationWhenSettingsSaveFails()
    {
        var registration = new FakeRegistration(isEnabled: true);
        var coordinator = new StartupSettingsCoordinator(registration);

        await Assert.ThrowsAsync<IOException>(() => coordinator.SetEnabledAsync(
            AppSettings.Defaults,
            enabled: false,
            _ => Task.FromException(new IOException("settings write failed"))));

        Assert.True(registration.IsEnabled);
        Assert.Equal([false, true], registration.Changes);
    }

    [Fact]
    public async Task SettingsCoordinator_ReturnsUpdatedSettingsAfterBothWritesSucceed()
    {
        var registration = new FakeRegistration(isEnabled: true);
        var coordinator = new StartupSettingsCoordinator(registration);

        AppSettings updated = await coordinator.SetEnabledAsync(
            AppSettings.Defaults,
            enabled: false,
            _ => Task.CompletedTask);

        Assert.False(registration.IsEnabled);
        Assert.False(updated.StartWithWindows);
    }

    private sealed class FakeStartupStore : IUserStartupStore
    {
        public string? LastPath { get; private set; }
        public Dictionary<string, string> Values { get; } = [];

        public string? GetValue(string path, string name)
        {
            LastPath = path;
            return Values.GetValueOrDefault(name);
        }

        public void SetValue(string path, string name, string value)
        {
            LastPath = path;
            Values[name] = value;
        }

        public void DeleteValue(string path, string name)
        {
            LastPath = path;
            Values.Remove(name);
        }
    }

    private sealed class FakeRegistration(bool isEnabled) : IStartupRegistration
    {
        public List<bool> Changes { get; } = [];
        public bool IsEnabled { get; private set; } = isEnabled;

        public void SetEnabled(bool enabled)
        {
            Changes.Add(enabled);
            IsEnabled = enabled;
        }
    }
}
