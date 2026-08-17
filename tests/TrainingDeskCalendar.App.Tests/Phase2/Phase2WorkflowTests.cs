using TrainingDeskCalendar.App.Calendar;
using TrainingDeskCalendar.App.Domain;
using TrainingDeskCalendar.App.Persistence;
using TrainingDeskCalendar.App.Services;
using TrainingDeskCalendar.App.Windows;
using Xunit;

namespace TrainingDeskCalendar.App.Tests.Phase2;

public sealed class Phase2WorkflowTests : IDisposable
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 19, 6, 0, 0, TimeSpan.Zero);

    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        "training-desk-calendar-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Composition_RoundTripsEditCompletionAndCopyWorkflow()
    {
        AppDataPaths paths = AppDataPaths.ForRoot(root);
        await using (AppComposition first = await AppComposition.CreateAsync(
                           paths,
                           new DateOnly(2026, 8, 19),
                           new FixedTimeProvider(Now)))
        {
            await first.Calendar.LoadAsync();
            DayCardViewModel card = first.Calendar.Days[2];
            first.Calendar.BeginEdit(card);
            card.Text = "间歇跑 5 km";
            card.SelectColor(TaskColorId.Orange);
            await first.Calendar.SaveEditAsync(card);
            await first.Calendar.SetCompletedAsync(card, true);

            TrainingPlan saved = Assert.Single(await first.PlanStore.GetAllAsync());
            Assert.Equal("间歇跑 5 km", saved.Text);
            Assert.True(saved.IsCompleted);
            Assert.Equal(TaskColorId.Orange, saved.Color);

            CopyPlanResult dayCopy = await first.PlanService.CopyDayToNextWeekAsync(
                card.Date,
                overwrite: false);
            Assert.True(dayCopy.Applied);

            CopyPlanResult conflict = await first.PlanService.CopyWeekToNextWeekAsync(
                new DateOnly(2026, 8, 17),
                overwrite: false);
            Assert.Single(conflict.Conflicts);
            Assert.False(conflict.Applied);

            CopyPlanResult overwrite = await first.PlanService.CopyWeekToNextWeekAsync(
                new DateOnly(2026, 8, 17),
                overwrite: true);
            Assert.True(overwrite.Applied);
            await first.Calendar.FlushAsync();
        }

        await using AppComposition second = await AppComposition.CreateAsync(
            paths,
            new DateOnly(2026, 8, 19),
            new FixedTimeProvider(Now));
        await second.Calendar.LoadAsync();

        Assert.Equal("间歇跑 5 km", second.Calendar.Days[2].Text);
        Assert.True(second.Calendar.Days[2].IsCompleted);
        Assert.Equal("间歇跑 5 km", second.Calendar.Days[9].Text);
        Assert.False(second.Calendar.Days[9].IsCompleted);
    }

    [Fact]
    public async Task Composition_DisposeFlushesAnUnfinishedEdit()
    {
        AppDataPaths paths = AppDataPaths.ForRoot(root);
        AppComposition first = await AppComposition.CreateAsync(
            paths,
            new DateOnly(2026, 8, 19),
            new FixedTimeProvider(Now));
        await first.Calendar.LoadAsync();
        DayCardViewModel card = first.Calendar.Days[2];
        first.Calendar.BeginEdit(card);
        card.Text = "退出前自动保存";

        await first.DisposeAsync();

        await using AppComposition second = await AppComposition.CreateAsync(
            paths,
            new DateOnly(2026, 8, 19),
            new FixedTimeProvider(Now));
        await second.Calendar.LoadAsync();

        Assert.Equal("退出前自动保存", second.Calendar.Days[2].Text);
    }

    [Fact]
    public async Task Composition_ImportRefreshesCalendarSettingsAndStartupState()
    {
        string sourceRoot = Path.Combine(root, "source");
        string targetRoot = Path.Combine(root, "target");
        string exportPath = Path.Combine(root, "import.json");
        var sourceStartup = new FakeStartupRegistration(true);
        await using (AppComposition source = await AppComposition.CreateAsync(
                         AppDataPaths.ForRoot(sourceRoot),
                         new DateOnly(2026, 8, 19),
                         new FixedTimeProvider(Now),
                         sourceStartup))
        {
            await source.PlanService.SaveAsync(TrainingPlan.Create(
                new DateOnly(2026, 8, 19),
                "导入后的计划",
                TaskColorId.Purple,
                updatedAtUtc: Now));
            await source.SaveSettingsAsync(source.Settings with
            {
                Theme = AppTheme.Dark,
                Opacity = 0.6,
                IsLocked = true,
                StartWithWindows = false
            });
            await source.TransferService.ExportAsync(exportPath);
        }

        var targetStartup = new FakeStartupRegistration(true);
        await using AppComposition target = await AppComposition.CreateAsync(
            AppDataPaths.ForRoot(targetRoot),
            new DateOnly(2026, 8, 19),
            new FixedTimeProvider(Now),
            targetStartup);

        await target.ImportAsync(exportPath);

        Assert.Equal(AppTheme.Dark, target.Settings.Theme);
        Assert.Equal(0.6, target.Settings.Opacity);
        Assert.True(target.Settings.IsLocked);
        Assert.False(target.Settings.StartWithWindows);
        Assert.False(targetStartup.IsEnabled);
        Assert.Equal("导入后的计划", target.Calendar.Days[2].Text);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeStartupRegistration(bool isEnabled) : IStartupRegistration
    {
        public bool IsEnabled { get; private set; } = isEnabled;
        public void SetEnabled(bool enabled) => IsEnabled = enabled;
    }
}
