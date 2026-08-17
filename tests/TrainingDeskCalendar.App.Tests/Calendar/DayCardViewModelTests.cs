using TrainingDeskCalendar.App.Domain;
using TrainingDeskCalendar.App.Calendar;
using TrainingDeskCalendar.App.Services;
using Xunit;

namespace TrainingDeskCalendar.App.Tests.Calendar;

public sealed class DayCardViewModelTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 17, 5, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task BeginEditCancelEdit_RestoresTheOriginalDraft()
    {
        var store = new FakePlanStore(Plan("原始计划", TaskColorId.Teal));
        await using var autosave = CreateAutosave(store);
        var service = new TrainingPlanService(store, new FixedTimeProvider(Now));
        TrainingPlan original = Assert.Single(await store.GetAllAsync());
        var card = new DayCardViewModel(
            original.Date,
            original,
            service,
            autosave,
            new FixedTimeProvider(Now));

        card.BeginEdit();
        card.Text = "临时修改";
        card.SelectColor(TaskColorId.Red);
        card.CancelEdit();
        await autosave.FlushAsync();

        Assert.Equal("原始计划", card.Text);
        Assert.Equal(TaskColorId.Teal, card.SelectedColor);
        Assert.False(card.IsEditing);
        Assert.Equal("原始计划", Assert.Single(await store.GetAllAsync()).Text);
    }

    [Fact]
    public async Task SaveEdit_PersistsTextAndColor()
    {
        var store = new FakePlanStore(Plan("原始计划", TaskColorId.Teal));
        await using var autosave = CreateAutosave(store);
        var service = new TrainingPlanService(store, new FixedTimeProvider(Now));
        TrainingPlan original = Assert.Single(await store.GetAllAsync());
        var card = new DayCardViewModel(
            original.Date,
            original,
            service,
            autosave,
            new FixedTimeProvider(Now));

        card.BeginEdit();
        card.Text = "更新后的计划";
        card.SelectColor(TaskColorId.Purple);
        await card.SaveEditAsync();

        TrainingPlan saved = Assert.Single(await store.GetAllAsync());
        Assert.Equal("更新后的计划", saved.Text);
        Assert.Equal(TaskColorId.Purple, saved.Color);
        Assert.False(card.IsEditing);
        Assert.False(card.IsDirty);
    }

    [Fact]
    public async Task SelectColor_RejectsColorsOutsideTheFixedPalette()
    {
        var store = new FakePlanStore();
        await using var autosave = CreateAutosave(store);
        var service = new TrainingPlanService(store, new FixedTimeProvider(Now));
        var card = new DayCardViewModel(
            new DateOnly(2026, 8, 19),
            plan: null,
            service,
            autosave,
            new FixedTimeProvider(Now));

        Assert.Throws<ArgumentOutOfRangeException>(() => card.SelectColor((TaskColorId)7));
    }

    [Fact]
    public async Task SetCompleted_DoesNotEnterEditMode()
    {
        var store = new FakePlanStore();
        await using var autosave = CreateAutosave(store);
        var service = new TrainingPlanService(store, new FixedTimeProvider(Now));
        var card = new DayCardViewModel(
            new DateOnly(2026, 8, 19),
            plan: null,
            service,
            autosave,
            new FixedTimeProvider(Now));

        await card.SetCompletedAsync(true);

        Assert.True(card.IsCompleted);
        Assert.False(card.IsEditing);
        Assert.True(Assert.Single(await store.GetAllAsync()).IsCompleted);
    }

    private static PlanAutosaveCoordinator CreateAutosave(FakePlanStore store) =>
        new(
            new TrainingPlanService(store, new FixedTimeProvider(Now)),
            static (_, cancellationToken) => Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken));

    private static TrainingPlan Plan(string text, TaskColorId color) =>
        TrainingPlan.Create(new DateOnly(2026, 8, 19), text, color, updatedAtUtc: Now);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakePlanStore(params TrainingPlan[] initialPlans) : ITrainingPlanStore
    {
        private readonly Dictionary<DateOnly, TrainingPlan> plans =
            initialPlans.ToDictionary(plan => plan.Date);

        public Task InitializeAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<TrainingPlan?> GetAsync(DateOnly date, CancellationToken cancellationToken = default) =>
            Task.FromResult(plans.GetValueOrDefault(date));

        public Task<IReadOnlyList<TrainingPlan>> GetRangeAsync(
            DateOnly start,
            DateOnly end,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TrainingPlan>>(plans.Values
                .Where(plan => plan.Date >= start && plan.Date <= end)
                .OrderBy(plan => plan.Date)
                .ToArray());

        public Task SaveAsync(TrainingPlan plan, CancellationToken cancellationToken = default)
        {
            if (plan.IsDefaultEmpty)
            {
                plans.Remove(plan.Date);
            }
            else
            {
                plans[plan.Date] = plan;
            }

            return Task.CompletedTask;
        }

        public Task SaveManyAsync(IReadOnlyCollection<TrainingPlan> plansToSave, CancellationToken cancellationToken = default)
        {
            foreach (TrainingPlan plan in plansToSave)
            {
                SaveAsync(plan, cancellationToken);
            }

            return Task.CompletedTask;
        }

        public Task DeleteAsync(DateOnly date, CancellationToken cancellationToken = default)
        {
            plans.Remove(date);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<TrainingPlan>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TrainingPlan>>(plans.Values.OrderBy(plan => plan.Date).ToArray());

        public Task ReplaceAllAsync(IReadOnlyCollection<TrainingPlan> replacement, CancellationToken cancellationToken = default)
        {
            plans.Clear();
            foreach (TrainingPlan plan in replacement)
            {
                plans[plan.Date] = plan;
            }

            return Task.CompletedTask;
        }
    }
}
