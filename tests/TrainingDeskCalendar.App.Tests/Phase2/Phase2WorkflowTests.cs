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

    [Fact]
    public async Task Composition_GatesRangeNavigationLatestDraftAndAppearancePersistence()
    {
        AppDataPaths paths = AppDataPaths.ForRoot(root);
        await using (AppComposition first = await AppComposition.CreateAsync(
                         paths,
                         new DateOnly(2026, 8, 19),
                         new FixedTimeProvider(Now),
                         new FakeStartupRegistration(true)))
        {
            await first.Calendar.LoadAsync();
            Assert.Equal(new DateOnly(2026, 8, 17), first.Calendar.Range.Start);
            Assert.Equal(14, first.Calendar.Days.Count);

            DateOnly initialStart = first.Calendar.Range.Start;
            await first.Calendar.NextAsync();
            Assert.Equal(initialStart.AddDays(14), first.Calendar.Range.Start);
            await first.Calendar.PreviousAsync();
            Assert.Equal(initialStart, first.Calendar.Range.Start);

            DayCardViewModel card = first.Calendar.Days[4];
            first.Calendar.BeginEdit(card);
            card.Text = "第一版草稿";
            card.Text = "最终训练计划";
            card.SelectColor(TaskColorId.Blue);
            await first.Calendar.FlushAsync();

            await first.SaveSettingsAsync(first.Settings with
            {
                Theme = AppTheme.Dark,
                Opacity = 0.7,
                IsLocked = true
            });
        }

        await using AppComposition second = await AppComposition.CreateAsync(
            paths,
            new DateOnly(2026, 8, 19),
            new FixedTimeProvider(Now),
            new FakeStartupRegistration(true));
        await second.Calendar.LoadAsync();

        Assert.Equal("最终训练计划", second.Calendar.Days[4].Text);
        Assert.Equal(TaskColorId.Blue, second.Calendar.Days[4].SelectedColor);
        Assert.Equal(AppTheme.Dark, second.Settings.Theme);
        Assert.Equal(0.7, second.Settings.Opacity);
        Assert.True(second.Settings.IsLocked);
        await second.Calendar.NextAsync();
        await second.Calendar.GoToTodayAsync();
        Assert.Equal(new DateOnly(2026, 8, 17), second.Calendar.Range.Start);
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
