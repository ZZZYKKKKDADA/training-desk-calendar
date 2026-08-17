using TrainingDeskCalendar.App.Domain;
using TrainingDeskCalendar.App.Calendar;
using TrainingDeskCalendar.App.Services;
using Xunit;

namespace TrainingDeskCalendar.App.Tests.Calendar;

public sealed class CalendarViewModelTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 19, 5, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task LoadAsync_CreatesTwoRowsOfSevenDaysWithStoredPlans()
    {
        var store = new FakePlanStore(
            TrainingPlan.Create(
                new DateOnly(2026, 8, 19),
                "慢跑",
                TaskColorId.Blue,
                updatedAtUtc: Now));
        await using var autosave = CreateAutosave(store);
        var viewModel = CreateViewModel(store, autosave);

        await viewModel.LoadAsync();

        Assert.Equal(new DateOnly(2026, 8, 17), viewModel.Range.Start);
        Assert.Equal(new DateOnly(2026, 8, 30), viewModel.Range.End);
        Assert.Equal(14, viewModel.Days.Count);
        Assert.Equal("慢跑", viewModel.Days[2].Text);
        Assert.Equal(TaskColorId.Blue, viewModel.Days[2].SelectedColor);
        Assert.Equal(new DateOnly(2026, 8, 30), viewModel.Days[^1].Date);
    }

    [Fact]
    public async Task PreviousNextAndToday_MoveByFourteenDays()
    {
        var store = new FakePlanStore();
        await using var autosave = CreateAutosave(store);
        var viewModel = CreateViewModel(store, autosave);
        await viewModel.LoadAsync();

        await viewModel.NextAsync();
        Assert.Equal(new DateOnly(2026, 8, 31), viewModel.Range.Start);
        await viewModel.PreviousAsync();
        Assert.Equal(new DateOnly(2026, 8, 17), viewModel.Range.Start);

        await viewModel.NextAsync();
        await viewModel.GoToTodayAsync();
        Assert.Equal(new DateOnly(2026, 8, 17), viewModel.Range.Start);
    }

    [Fact]
    public async Task BeginEditOnAnotherCard_SavesAndCollapsesThePreviousCard()
    {
        var store = new FakePlanStore();
        await using var autosave = CreateAutosave(store);
        var viewModel = CreateViewModel(store, autosave);
        await viewModel.LoadAsync();
        DayCardViewModel first = viewModel.Days[0];
        DayCardViewModel second = viewModel.Days[1];

        viewModel.BeginEdit(first);
        first.Text = "第一天";
        viewModel.BeginEdit(second);
        await viewModel.FlushAsync();

        Assert.False(first.IsEditing);
        Assert.True(second.IsEditing);
        Assert.Equal("第一天", Assert.Single(await store.GetAllAsync()).Text);
    }

    private static CalendarViewModel CreateViewModel(
        FakePlanStore store,
        PlanAutosaveCoordinator autosave) =>
        new(
            new TrainingPlanService(store, new FixedTimeProvider(Now)),
            autosave,
            new CalendarRangeService(),
            new DateOnly(2026, 8, 19),
            new FixedTimeProvider(Now));

    private static PlanAutosaveCoordinator CreateAutosave(FakePlanStore store) =>
        new(
            new TrainingPlanService(store, new FixedTimeProvider(Now)),
            static (_, cancellationToken) => Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakePlanStore(params TrainingPlan[] initialPlans) : ITrainingPlanStore
    {
        private readonly Dictionary<DateOnly, TrainingPlan> plans =
            initialPlans.ToDictionary(plan => plan.Date);

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<TrainingPlan?> GetAsync(DateOnly date, CancellationToken cancellationToken = default) =>
            Task.FromResult(plans.GetValueOrDefault(date));
        public Task<IReadOnlyList<TrainingPlan>> GetRangeAsync(DateOnly start, DateOnly end, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TrainingPlan>>(plans.Values.Where(p => p.Date >= start && p.Date <= end).OrderBy(p => p.Date).ToArray());
        public Task SaveAsync(TrainingPlan plan, CancellationToken cancellationToken = default)
        {
            if (plan.IsDefaultEmpty) plans.Remove(plan.Date); else plans[plan.Date] = plan;
            return Task.CompletedTask;
        }
        public Task SaveManyAsync(IReadOnlyCollection<TrainingPlan> saved, CancellationToken cancellationToken = default)
        {
            foreach (TrainingPlan plan in saved) SaveAsync(plan, cancellationToken);
            return Task.CompletedTask;
        }
        public Task DeleteAsync(DateOnly date, CancellationToken cancellationToken = default) { plans.Remove(date); return Task.CompletedTask; }
        public Task<IReadOnlyList<TrainingPlan>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TrainingPlan>>(plans.Values.OrderBy(p => p.Date).ToArray());
        public Task ReplaceAllAsync(IReadOnlyCollection<TrainingPlan> replacement, CancellationToken cancellationToken = default)
        {
            plans.Clear(); foreach (TrainingPlan plan in replacement) plans[plan.Date] = plan; return Task.CompletedTask;
        }
    }
}
