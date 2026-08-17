using System.Text.Json;
using TrainingDeskCalendar.App.Domain;
using TrainingDeskCalendar.App.Persistence;
using TrainingDeskCalendar.App.Services;
using Xunit;

namespace TrainingDeskCalendar.App.Tests.Services;

public sealed class DataTransferServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 17, 3, 0, 0, TimeSpan.Zero);

    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        "training-desk-calendar-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Export_WritesVersionedPlansAndSettingsWithoutDiagnostics()
    {
        (AppDataPaths paths, SqlitePlanStore plans, SettingsStore settings) =
            await CreateStoresAsync();
        await plans.SaveAsync(Plan(new DateOnly(2026, 8, 19), "力量训练", TaskColorId.Blue));
        await settings.SaveAsync(AppSettings.Defaults with { Opacity = 0.75 });
        string exportPath = Path.Combine(root, "exports", "training-plan.json");

        await CreateService(paths, plans, settings).ExportAsync(exportPath);

        using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(exportPath));
        JsonElement snapshot = document.RootElement;
        Assert.Equal(1, snapshot.GetProperty("formatVersion").GetInt32());
        Assert.Equal(Now, snapshot.GetProperty("exportedAtUtc").GetDateTimeOffset());
        JsonElement exportedPlan = Assert.Single(snapshot.GetProperty("plans").EnumerateArray());
        Assert.Equal("2026-08-19", exportedPlan.GetProperty("date").GetString());
        Assert.Equal((int)TaskColorId.Blue, exportedPlan.GetProperty("colorId").GetInt32());
        Assert.Equal(0.75, snapshot.GetProperty("settings").GetProperty("opacity").GetDouble());
        Assert.False(snapshot.TryGetProperty("diagnostics", out _));
        Assert.False(snapshot.TryGetProperty("logs", out _));
    }

    [Fact]
    public async Task Import_CreatesBackupAndReplacesPlansAndSettings()
    {
        (AppDataPaths paths, SqlitePlanStore plans, SettingsStore settings) =
            await CreateStoresAsync();
        await plans.SaveAsync(Plan(new DateOnly(2026, 8, 18), "旧计划", TaskColorId.Gray));
        await settings.SaveAsync(AppSettings.Defaults with { Opacity = 0.9 });
        string importPath = await WriteValidImportAsync(
            Plan(new DateOnly(2026, 8, 22), "新计划", TaskColorId.Purple),
            AppSettings.Defaults with { Theme = AppTheme.Dark, Opacity = 0.6 });

        await CreateService(paths, plans, settings).ImportAsync(importPath);

        TrainingPlan imported = Assert.Single(await plans.GetAllAsync());
        Assert.Equal(new DateOnly(2026, 8, 22), imported.Date);
        Assert.Equal("新计划", imported.Text);
        AppSettings importedSettings = await settings.LoadAsync();
        Assert.Equal(AppTheme.Dark, importedSettings.Theme);
        Assert.Equal(0.6, importedSettings.Opacity);
        Assert.Single(Directory.GetFiles(paths.BackupDirectory, "backup-*.json"));
    }

    [Theory]
    [InlineData("{not-json")]
    [InlineData("""
        {
          "formatVersion": 2,
          "exportedAtUtc": "2026-08-17T03:00:00+00:00",
          "plans": [],
          "settings": null
        }
        """)]
    [InlineData("""
        {
          "formatVersion": 1,
          "exportedAtUtc": "2026-08-17T03:00:00+00:00",
          "plans": [{
            "date": "2026-08-22",
            "text": "非法颜色",
            "colorId": 7,
            "isCompleted": false,
            "updatedAtUtc": "2026-08-17T03:00:00+00:00"
          }],
          "settings": {
            "version": 1,
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
        }
        """)]
    public async Task Import_InvalidSnapshotLeavesCurrentDataUnchanged(string invalidJson)
    {
        (AppDataPaths paths, SqlitePlanStore plans, SettingsStore settings) =
            await CreateStoresAsync();
        TrainingPlan originalPlan = Plan(
            new DateOnly(2026, 8, 18),
            "保留计划",
            TaskColorId.Teal);
        AppSettings originalSettings = AppSettings.Defaults with { Opacity = 0.85 };
        await plans.SaveAsync(originalPlan);
        await settings.SaveAsync(originalSettings);
        string importPath = Path.Combine(root, "invalid.json");
        await File.WriteAllTextAsync(importPath, invalidJson);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            CreateService(paths, plans, settings).ImportAsync(importPath));

        Assert.Equal(originalPlan, Assert.Single(await plans.GetAllAsync()));
        Assert.Equal(originalSettings, await settings.LoadAsync());
        Assert.False(Directory.Exists(paths.BackupDirectory));
    }

    [Fact]
    public async Task Import_WhenSettingsCommitFails_RollsBackPlansAndSettings()
    {
        (AppDataPaths paths, SqlitePlanStore plans, SettingsStore settings) =
            await CreateStoresAsync();
        TrainingPlan originalPlan = Plan(
            new DateOnly(2026, 8, 18),
            "回滚计划",
            TaskColorId.Orange);
        AppSettings originalSettings = AppSettings.Defaults with { Opacity = 0.85 };
        await plans.SaveAsync(originalPlan);
        await settings.SaveAsync(originalSettings);
        string importPath = await WriteValidImportAsync(
            Plan(new DateOnly(2026, 8, 23), "不应保留", TaskColorId.Red),
            AppSettings.Defaults with { Opacity = 0.55 });
        int commitAttempts = 0;
        var failingSettings = new SettingsStore(
            paths.SettingsPath,
            commit: (temporaryPath, destinationPath) =>
            {
                commitAttempts++;
                if (commitAttempts == 1)
                {
                    throw new IOException("Simulated settings commit failure.");
                }

                File.Move(temporaryPath, destinationPath, overwrite: true);
            });

        await Assert.ThrowsAsync<IOException>(() =>
            CreateService(paths, plans, failingSettings).ImportAsync(importPath));

        Assert.Equal(2, commitAttempts);
        Assert.Equal(originalPlan, Assert.Single(await plans.GetAllAsync()));
        Assert.Equal(originalSettings, await settings.LoadAsync());
        Assert.Single(Directory.GetFiles(paths.BackupDirectory, "backup-*.json"));
    }

    [Fact]
    public async Task Import_KeepsOnlyTheFiveNewestRecoveryBackups()
    {
        (AppDataPaths paths, SqlitePlanStore plans, SettingsStore settings) =
            await CreateStoresAsync();
        string importPath = await WriteValidImportAsync(
            Plan(new DateOnly(2026, 8, 23), "重复导入", TaskColorId.Red),
            AppSettings.Defaults);
        DataTransferService service = CreateService(paths, plans, settings);

        for (int index = 0; index < 6; index++)
        {
            await service.ImportAsync(importPath);
        }

        Assert.Equal(5, Directory.GetFiles(paths.BackupDirectory, "backup-*.json").Length);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private async Task<(AppDataPaths, SqlitePlanStore, SettingsStore)> CreateStoresAsync()
    {
        AppDataPaths paths = AppDataPaths.ForRoot(root);
        var plans = new SqlitePlanStore(paths.DatabasePath);
        await plans.InitializeAsync();
        return (paths, plans, new SettingsStore(paths.SettingsPath));
    }

    private DataTransferService CreateService(
        AppDataPaths paths,
        ITrainingPlanStore plans,
        SettingsStore settings) =>
        new(plans, settings, paths, new FixedTimeProvider(Now));

    private async Task<string> WriteValidImportAsync(
        TrainingPlan plan,
        AppSettings settings)
    {
        string path = Path.Combine(root, $"import-{Guid.NewGuid():N}.json");
        string json = $$"""
            {
              "formatVersion": 1,
              "exportedAtUtc": "{{Now:O}}",
              "plans": [{
                "date": "{{plan.Date:yyyy-MM-dd}}",
                "text": {{JsonSerializer.Serialize(plan.Text)}},
                "colorId": {{(int)plan.Color}},
                "isCompleted": {{plan.IsCompleted.ToString().ToLowerInvariant()}},
                "updatedAtUtc": "{{plan.UpdatedAtUtc:O}}"
              }],
              "settings": {{JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                })}}
            }
            """;
        await File.WriteAllTextAsync(path, json);
        return path;
    }

    private static TrainingPlan Plan(DateOnly date, string text, TaskColorId color) =>
        TrainingPlan.Create(date, text, color, updatedAtUtc: Now.AddHours(-1));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
