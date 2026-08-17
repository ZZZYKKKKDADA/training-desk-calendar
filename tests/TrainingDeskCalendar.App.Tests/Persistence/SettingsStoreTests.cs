using TrainingDeskCalendar.App.Persistence;
using Xunit;

namespace TrainingDeskCalendar.App.Tests.Persistence;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        "training-desk-calendar-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Defaults_MatchTheApprovedInitialSettings()
    {
        AppSettings settings = AppSettings.Defaults;

        Assert.Equal(1, settings.Version);
        Assert.Equal(AppTheme.Light, settings.Theme);
        Assert.Equal(1.0, settings.Opacity);
        Assert.False(settings.IsLocked);
        Assert.True(settings.StartWithWindows);
        Assert.Equal(1120, settings.WindowWidth);
        Assert.Equal(470, settings.WindowHeight);
    }

    [Theory]
    [InlineData(0.39, 1120, 470)]
    [InlineData(1.01, 1120, 470)]
    [InlineData(1.0, 839, 470)]
    [InlineData(1.0, 1120, 359)]
    public void Validate_RejectsOutOfRangeOpacityOrWindowSize(
        double opacity,
        double width,
        double height)
    {
        AppSettings settings = AppSettings.Defaults with
        {
            Opacity = opacity,
            WindowWidth = width,
            WindowHeight = height
        };

        Assert.Throws<InvalidDataException>(() => settings.Validate());
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsValidatedSettings()
    {
        SettingsStore store = CreateStore();
        AppSettings expected = AppSettings.Defaults with
        {
            WindowX = 240,
            WindowY = 160,
            IsLocked = true,
            Theme = AppTheme.Dark,
            Opacity = 0.7,
            StartWithWindows = false
        };

        await store.SaveAsync(expected);
        AppSettings actual = await store.LoadAsync();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task Load_NewerVersionIsPreservedAsCorruptAndReturnsDefaults()
    {
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(
            SettingsPath,
            """
            {
              "version": 2,
              "windowX": 100,
              "windowY": 100,
              "windowWidth": 1120,
              "windowHeight": 470,
              "monitorId": "",
              "isLocked": false,
              "theme": 0,
              "opacity": 1.0,
              "startWithWindows": true,
              "lastUpdateCheckUtc": null
            }
            """);

        AppSettings result = await CreateStore().LoadAsync();

        Assert.Equal(AppSettings.Defaults, result);
        Assert.False(File.Exists(SettingsPath));
        Assert.Single(Directory.GetFiles(root, "settings.corrupt-*.json"));
    }

    [Fact]
    public async Task Load_MalformedJsonIsPreservedAsCorruptAndReturnsDefaults()
    {
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(SettingsPath, "{not-json");

        AppSettings result = await CreateStore().LoadAsync();

        Assert.Equal(AppSettings.Defaults, result);
        string corruptPath = Assert.Single(
            Directory.GetFiles(root, "settings.corrupt-*.json"));
        Assert.Equal("{not-json", await File.ReadAllTextAsync(corruptPath));
    }

    [Fact]
    public async Task Save_WhenCommitFails_PreservesPreviousValidFile()
    {
        SettingsStore store = CreateStore();
        AppSettings original = AppSettings.Defaults with { Opacity = 0.8 };
        await store.SaveAsync(original);
        var failingStore = new SettingsStore(
            SettingsPath,
            commit: (_, _) => throw new IOException("Simulated commit failure."));

        await Assert.ThrowsAsync<IOException>(() =>
            failingStore.SaveAsync(original with { Opacity = 0.6 }));

        Assert.Equal(original, await store.LoadAsync());
        Assert.Empty(Directory.GetFiles(root, "*.tmp"));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private SettingsStore CreateStore() => new(SettingsPath);

    private string SettingsPath => Path.Combine(root, "settings.json");
}
