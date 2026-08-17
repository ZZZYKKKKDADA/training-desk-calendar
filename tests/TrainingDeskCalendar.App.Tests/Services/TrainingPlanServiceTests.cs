using TrainingDeskCalendar.App.Domain;
using TrainingDeskCalendar.App.Services;
using Xunit;

namespace TrainingDeskCalendar.App.Tests.Services;

public sealed class TrainingPlanServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 17, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Save_DefaultEmptyPlanDeletesTheDate()
    {
        var store = new FakePlanStore();
        var service = CreateService(store);
        DateOnly date = new(2026, 8, 19);

        await service.SaveAsync(TrainingPlan.Create(date, string.Empty));

        Assert.Equal([date], store.DeletedDates);
        Assert.Empty(store.SavedBatches);
    }

    [Fact]
    public async Task SetCompleted_CreatesACompletedDefaultPlanWhenDateIsMissing()
    {
        var store = new FakePlanStore();
        var service = CreateService(store);
        DateOnly date = new(2026, 8, 19);

        await service.SetCompletedAsync(date, isCompleted: true);

        TrainingPlan saved = Assert.Single(Assert.Single(store.SavedBatches));
        Assert.Equal(date, saved.Date);
        Assert.True(saved.IsCompleted);
        Assert.Equal(TaskColorId.Gray, saved.Color);
        Assert.Equal(Now, saved.UpdatedAtUtc);
    }

    [Fact]
    public async Task CopyDay_ReturnsConflictWithoutWritingWhenTargetExists()
    {
        DateOnly sourceDate = new(2026, 8, 17);
        DateOnly targetDate = sourceDate.AddDays(7);
        var store = new FakePlanStore(
            Plan(sourceDate, "胸部训练", TaskColorId.Teal, completed: true),
            Plan(targetDate, "已有计划", TaskColorId.Red));
        var service = CreateService(store);

        CopyPlanResult result = await service.CopyDayToNextWeekAsync(sourceDate, overwrite: false);

        Assert.False(result.Applied);
        Assert.Equal(new CopyConflict(sourceDate, targetDate), Assert.Single(result.Conflicts));
        Assert.Empty(store.SavedBatches);
    }

    [Fact]
    public async Task CopyDay_OverwritesTextAndColorButResetsCompletion()
    {
        DateOnly sourceDate = new(2026, 8, 17);
        DateOnly targetDate = sourceDate.AddDays(7);
        var store = new FakePlanStore(
            Plan(sourceDate, "胸部训练", TaskColorId.Teal, completed: true),
            Plan(targetDate, "已有计划", TaskColorId.Red, completed: true));
        var service = CreateService(store);

        CopyPlanResult result = await service.CopyDayToNextWeekAsync(sourceDate, overwrite: true);

        Assert.True(result.Applied);
        TrainingPlan saved = Assert.Single(Assert.Single(store.SavedBatches));
        Assert.Equal(targetDate, saved.Date);
        Assert.Equal("胸部训练", saved.Text);
        Assert.Equal(TaskColorId.Teal, saved.Color);
        Assert.False(saved.IsCompleted);
        Assert.Equal(Now, saved.UpdatedAtUtc);
    }

    [Fact]
    public async Task CopyWeek_ListsEveryConflictBeforeWriting()
    {
        DateOnly monday = new(2026, 8, 17);
        var store = new FakePlanStore(
            Plan(monday, "计划 A", TaskColorId.Teal),
            Plan(monday.AddDays(2), "计划 B", TaskColorId.Blue),
            Plan(monday.AddDays(7), "冲突 A", TaskColorId.Red),
            Plan(monday.AddDays(9), "冲突 B", TaskColorId.Purple));
        var service = CreateService(store);

        CopyPlanResult result = await service.CopyWeekToNextWeekAsync(monday, overwrite: false);

        Assert.False(result.Applied);
        Assert.Equal(2, result.Conflicts.Count);
        Assert.Contains(new CopyConflict(monday, monday.AddDays(7)), result.Conflicts);
        Assert.Contains(new CopyConflict(monday.AddDays(2), monday.AddDays(9)), result.Conflicts);
        Assert.Empty(store.SavedBatches);
    }

    [Fact]
    public async Task CopyWeek_IgnoresEmptySourceDatesAndSavesOneBatch()
    {
        DateOnly monday = new(2026, 8, 17);
        var store = new FakePlanStore(
            Plan(monday, "计划 A", TaskColorId.Teal),
            Plan(monday.AddDays(8), "目标周二原计划", TaskColorId.Red));
        var service = CreateService(store);

        CopyPlanResult result = await service.CopyWeekToNextWeekAsync(monday, overwrite: true);

        Assert.True(result.Applied);
        TrainingPlan saved = Assert.Single(Assert.Single(store.SavedBatches));
        Assert.Equal(monday.AddDays(7), saved.Date);
        Assert.Equal("计划 A", saved.Text);
        Assert.DoesNotContain(store.DeletedDates, date => date == monday.AddDays(8));
    }

    [Fact]
    public async Task CopyWeek_RejectsANonMondayStart()
    {
        var service = CreateService(new FakePlanStore());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CopyWeekToNextWeekAsync(new DateOnly(2026, 8, 18), overwrite: false));
    }

    private static TrainingPlanService CreateService(FakePlanStore store) =>
        new(store, new FixedTimeProvider(Now));

    private static TrainingPlan Plan(
        DateOnly date,
        string text,
        TaskColorId color,
        bool completed = false) =>
        TrainingPlan.Create(date, text, color, completed, Now.AddDays(-1));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakePlanStore(params TrainingPlan[] plans) : ITrainingPlanStore
    {
        private readonly Dictionary<DateOnly, TrainingPlan> plans =
            plans.ToDictionary(plan => plan.Date);

        public List<IReadOnlyCollection<TrainingPlan>> SavedBatches { get; } = [];
        public List<DateOnly> DeletedDates { get; } = [];

        public Task InitializeAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<TrainingPlan?> GetAsync(
            DateOnly date,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(plans.GetValueOrDefault(date));

        public Task<IReadOnlyList<TrainingPlan>> GetRangeAsync(
            DateOnly start,
            DateOnly end,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TrainingPlan>>(plans.Values
                .Where(plan => plan.Date >= start && plan.Date <= end)
                .OrderBy(plan => plan.Date)
                .ToArray());

        public Task SaveAsync(
            TrainingPlan plan,
            CancellationToken cancellationToken = default) =>
            SaveManyAsync([plan], cancellationToken);

        public Task SaveManyAsync(
            IReadOnlyCollection<TrainingPlan> savedPlans,
            CancellationToken cancellationToken = default)
        {
            TrainingPlan[] batch = savedPlans.ToArray();
            SavedBatches.Add(batch);
            foreach (TrainingPlan plan in batch)
            {
                plans[plan.Date] = plan;
            }

            return Task.CompletedTask;
        }

        public Task DeleteAsync(
            DateOnly date,
            CancellationToken cancellationToken = default)
        {
            DeletedDates.Add(date);
            plans.Remove(date);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<TrainingPlan>> GetAllAsync(
            CancellationToken cancellationToken = default) =>
            GetRangeAsync(DateOnly.MinValue, DateOnly.MaxValue, cancellationToken);

        public Task ReplaceAllAsync(
            IReadOnlyCollection<TrainingPlan> replacement,
            CancellationToken cancellationToken = default)
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
