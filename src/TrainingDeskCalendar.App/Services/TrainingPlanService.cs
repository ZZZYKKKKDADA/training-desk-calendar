using TrainingDeskCalendar.App.Domain;

namespace TrainingDeskCalendar.App.Services;

internal sealed class TrainingPlanService(
    ITrainingPlanStore store,
    TimeProvider? timeProvider = null)
{
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    public Task<IReadOnlyList<TrainingPlan>> GetRangeAsync(
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken = default) =>
        store.GetRangeAsync(start, end, cancellationToken);

    public Task SaveAsync(
        TrainingPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return plan.IsDefaultEmpty
            ? store.DeleteAsync(plan.Date, cancellationToken)
            : store.SaveAsync(plan, cancellationToken);
    }

    public async Task SetCompletedAsync(
        DateOnly date,
        bool isCompleted,
        CancellationToken cancellationToken = default)
    {
        TrainingPlan? existing = await store.GetAsync(date, cancellationToken);
        if (existing is null && !isCompleted)
        {
            return;
        }

        TrainingPlan updated = TrainingPlan.Create(
            date,
            existing?.Text ?? string.Empty,
            existing?.Color ?? TaskColorId.Gray,
            isCompleted,
            timeProvider.GetUtcNow());
        await SaveAsync(updated, cancellationToken);
    }

    public async Task<CopyPlanResult> CopyDayToNextWeekAsync(
        DateOnly sourceDate,
        bool overwrite,
        CancellationToken cancellationToken = default)
    {
        TrainingPlan? source = await store.GetAsync(sourceDate, cancellationToken);
        if (source is null)
        {
            return new CopyPlanResult(true, []);
        }

        DateOnly targetDate = sourceDate.AddDays(7);
        TrainingPlan? target = await store.GetAsync(targetDate, cancellationToken);
        CopyConflict[] conflicts = target is null
            ? []
            : [new CopyConflict(sourceDate, targetDate)];
        if (conflicts.Length > 0 && !overwrite)
        {
            return new CopyPlanResult(false, conflicts);
        }

        TrainingPlan copy = CreateCopy(source, targetDate);
        await store.SaveManyAsync([copy], cancellationToken);
        return new CopyPlanResult(true, conflicts);
    }

    public async Task<CopyPlanResult> CopyWeekToNextWeekAsync(
        DateOnly weekStart,
        bool overwrite,
        CancellationToken cancellationToken = default)
    {
        if (weekStart.DayOfWeek != DayOfWeek.Monday)
        {
            throw new ArgumentException("Week copy must start on Monday.", nameof(weekStart));
        }

        IReadOnlyList<TrainingPlan> sources = await store.GetRangeAsync(
            weekStart,
            weekStart.AddDays(6),
            cancellationToken);
        IReadOnlyList<TrainingPlan> targets = await store.GetRangeAsync(
            weekStart.AddDays(7),
            weekStart.AddDays(13),
            cancellationToken);
        HashSet<DateOnly> targetDates = targets.Select(plan => plan.Date).ToHashSet();
        CopyConflict[] conflicts = sources
            .Where(source => targetDates.Contains(source.Date.AddDays(7)))
            .Select(source => new CopyConflict(source.Date, source.Date.AddDays(7)))
            .ToArray();

        if (conflicts.Length > 0 && !overwrite)
        {
            return new CopyPlanResult(false, conflicts);
        }

        TrainingPlan[] copies = sources
            .Select(source => CreateCopy(source, source.Date.AddDays(7)))
            .ToArray();
        if (copies.Length > 0)
        {
            await store.SaveManyAsync(copies, cancellationToken);
        }

        return new CopyPlanResult(true, conflicts);
    }

    private TrainingPlan CreateCopy(TrainingPlan source, DateOnly targetDate) =>
        TrainingPlan.Create(
            targetDate,
            source.Text,
            source.Color,
            isCompleted: false,
            timeProvider.GetUtcNow());
}
