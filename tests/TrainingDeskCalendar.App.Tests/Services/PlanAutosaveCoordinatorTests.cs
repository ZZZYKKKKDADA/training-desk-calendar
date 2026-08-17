using TrainingDeskCalendar.App.Domain;
using TrainingDeskCalendar.App.Services;
using Xunit;

namespace TrainingDeskCalendar.App.Tests.Services;

public sealed class PlanAutosaveCoordinatorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 17, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task QueueAsync_SavesOnlyTheLatestPlanAfterDebounce()
    {
        var store = new FakePlanStore();
        var delay = new ControlledDelay();
        await using var coordinator = CreateCoordinator(store, delay);
        DateOnly date = new(2026, 8, 19);

        Task first = coordinator.QueueAsync(Plan(date, "第一次"));
        Task second = coordinator.QueueAsync(Plan(date, "最后一次"));

        Assert.False(first.IsCompleted);
        Assert.False(second.IsCompleted);
        Assert.Empty(store.SavedPlans);
        Assert.Equal(2, delay.Waiters.Count);

        delay.Release(1);
        await second;

        TrainingPlan saved = Assert.Single(store.SavedPlans);
        Assert.Equal("最后一次", saved.Text);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            first.WaitAsync(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task FlushAsync_SavesImmediatelyAndCancelsPendingDelay()
    {
        var store = new FakePlanStore();
        var delay = new ControlledDelay();
        await using var coordinator = CreateCoordinator(store, delay);
        DateOnly date = new(2026, 8, 19);
        Task queued = coordinator.QueueAsync(Plan(date, "待保存"));

        await coordinator.FlushAsync();

        Assert.Equal("待保存", Assert.Single(store.SavedPlans).Text);
        Assert.True(queued.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task DisposeAsync_FlushesLatestPlan()
    {
        var store = new FakePlanStore();
        var delay = new ControlledDelay();
        var coordinator = CreateCoordinator(store, delay);
        DateOnly date = new(2026, 8, 19);
        _ = coordinator.QueueAsync(Plan(date, "退出前内容"));

        await coordinator.DisposeAsync();

        Assert.Equal("退出前内容", Assert.Single(store.SavedPlans).Text);
    }

    [Fact]
    public async Task FailedSave_RemainsPendingAndFlushCanRetry()
    {
        var store = new FakePlanStore { RemainingSaveFailures = 1 };
        var delay = new ControlledDelay();
        await using var coordinator = CreateCoordinator(store, delay);
        Task queued = coordinator.QueueAsync(Plan(new DateOnly(2026, 8, 19), "重试内容"));
        delay.Release(0);

        await Assert.ThrowsAsync<IOException>(() => queued);
        await coordinator.FlushAsync();

        Assert.Equal("重试内容", Assert.Single(store.SavedPlans).Text);
    }

    [Fact]
    public async Task ReplacedSaveFailure_CancelsTheSupersededTask()
    {
        var store = new FakePlanStore { RemainingSaveFailures = 1 };
        var delay = new ControlledDelay();
        await using var coordinator = CreateCoordinator(store, delay);
        DateOnly date = new(2026, 8, 19);
        Task first = coordinator.QueueAsync(Plan(date, "旧内容"));
        Task latest = coordinator.QueueAsync(Plan(date, "失败内容"));

        delay.Release(1);

        await Assert.ThrowsAsync<IOException>(() => latest);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            first.WaitAsync(TimeSpan.FromSeconds(1)));
    }

    private static PlanAutosaveCoordinator CreateCoordinator(
        FakePlanStore store,
        ControlledDelay delay) =>
        new(
            new TrainingPlanService(store, new FixedTimeProvider(Now)),
            delay.WaitAsync,
            TimeSpan.FromMilliseconds(250));

    private static TrainingPlan Plan(DateOnly date, string text) =>
        TrainingPlan.Create(date, text, TaskColorId.Teal, updatedAtUtc: Now);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class ControlledDelay
    {
        public List<TaskCompletionSource<bool>> Waiters { get; } = [];

        public Task WaitAsync(TimeSpan _, CancellationToken cancellationToken)
        {
            var waiter = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            Waiters.Add(waiter);
            return waiter.Task.WaitAsync(cancellationToken);
        }

        public void Release(int index) => Waiters[index].SetResult(true);
    }

    private sealed class FakePlanStore : ITrainingPlanStore
    {
        public List<TrainingPlan> SavedPlans { get; } = [];
        public int RemainingSaveFailures { get; set; }

        public Task InitializeAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<TrainingPlan?> GetAsync(
            DateOnly date,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(SavedPlans.LastOrDefault(plan => plan.Date == date));

        public Task<IReadOnlyList<TrainingPlan>> GetRangeAsync(
            DateOnly start,
            DateOnly end,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TrainingPlan>>(SavedPlans
                .Where(plan => plan.Date >= start && plan.Date <= end)
                .ToArray());

        public Task SaveAsync(
            TrainingPlan plan,
            CancellationToken cancellationToken = default)
        {
            if (RemainingSaveFailures > 0)
            {
                RemainingSaveFailures--;
                throw new IOException("Simulated save failure.");
            }

            SavedPlans.RemoveAll(existing => existing.Date == plan.Date);
            SavedPlans.Add(plan);
            return Task.CompletedTask;
        }

        public Task SaveManyAsync(
            IReadOnlyCollection<TrainingPlan> plans,
            CancellationToken cancellationToken = default)
        {
            foreach (TrainingPlan plan in plans)
            {
                SavedPlans.RemoveAll(existing => existing.Date == plan.Date);
                SavedPlans.Add(plan);
            }

            return Task.CompletedTask;
        }

        public Task DeleteAsync(
            DateOnly date,
            CancellationToken cancellationToken = default)
        {
            SavedPlans.RemoveAll(existing => existing.Date == date);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<TrainingPlan>> GetAllAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TrainingPlan>>(SavedPlans.ToArray());

        public Task ReplaceAllAsync(
            IReadOnlyCollection<TrainingPlan> plans,
            CancellationToken cancellationToken = default)
        {
            SavedPlans.Clear();
            SavedPlans.AddRange(plans);
            return Task.CompletedTask;
        }
    }
}
