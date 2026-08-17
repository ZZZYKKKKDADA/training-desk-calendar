using TrainingDeskCalendar.App.Domain;
using TrainingDeskCalendar.App.Persistence;
using TrainingDeskCalendar.App.Services;
using Xunit;

namespace TrainingDeskCalendar.App.Tests.Phase1;

public sealed class Phase1DataFlowTests : IDisposable
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 17, 4, 0, 0, TimeSpan.Zero);

    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        "training-desk-calendar-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LocalDataFlow_RestoresTheCompleteExportedState()
    {
        AppDataPaths paths = AppDataPaths.ForRoot(root);
        var store = new SqlitePlanStore(paths.DatabasePath);
        await store.InitializeAsync();
        var planService = new TrainingPlanService(store, new FixedTimeProvider(Now));
        var settingsStore = new SettingsStore(paths.SettingsPath);
        var transferService = new DataTransferService(
            store,
            settingsStore,
            paths,
            new FixedTimeProvider(Now));
        var rangeService = new CalendarRangeService();
        await using var autosave = new PlanAutosaveCoordinator(
            planService,
            NeverCompletingDelay,
            TimeSpan.FromMilliseconds(250));
        DateOnly monday = new(2026, 8, 17);

        Task queued = autosave.QueueAsync(Plan(monday, "上肢力量", TaskColorId.Teal));
        await autosave.FlushAsync();
        Assert.True(queued.IsCompletedSuccessfully);
        await autosave.FlushAsync();
        await planService.SaveAsync(Plan(monday.AddDays(2), "间歇跑", TaskColorId.Blue));

        TwoWeekRange range = rangeService.Containing(monday.AddDays(2));
        IReadOnlyList<TrainingPlan> initialRange = await planService.GetRangeAsync(
            range.Start,
            range.End);
        Assert.Equal(2, initialRange.Count);

        await planService.SetCompletedAsync(monday, isCompleted: true);
        CopyPlanResult dayCopy = await planService.CopyDayToNextWeekAsync(
            monday.AddDays(2),
            overwrite: false);
        Assert.True(dayCopy.Applied);

        CopyPlanResult conflict = await planService.CopyWeekToNextWeekAsync(
            monday,
            overwrite: false);
        Assert.False(conflict.Applied);
        Assert.Single(conflict.Conflicts);
        CopyPlanResult weekCopy = await planService.CopyWeekToNextWeekAsync(
            monday,
            overwrite: true);
        Assert.True(weekCopy.Applied);

        AppSettings exportedSettings = AppSettings.Defaults with
        {
            Theme = AppTheme.Dark,
            Opacity = 0.65,
            IsLocked = true
        };
        await settingsStore.SaveAsync(exportedSettings);
        IReadOnlyList<TrainingPlan> exportedPlans = await store.GetAllAsync();
        string exportPath = Path.Combine(root, "phase1-export.json");
        await transferService.ExportAsync(exportPath);

        await store.ReplaceAllAsync([]);
        await settingsStore.SaveAsync(AppSettings.Defaults);
        Assert.Empty(await store.GetAllAsync());

        await transferService.ImportAsync(exportPath);

        Assert.Equal(exportedPlans, await store.GetAllAsync());
        Assert.Equal(exportedSettings, await settingsStore.LoadAsync());
        Assert.Single(Directory.GetFiles(paths.BackupDirectory, "backup-*.json"));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static Task NeverCompletingDelay(
        TimeSpan _,
        CancellationToken cancellationToken) =>
        Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

    private static TrainingPlan Plan(DateOnly date, string text, TaskColorId color) =>
        TrainingPlan.Create(date, text, color, updatedAtUtc: Now.AddHours(-1));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
