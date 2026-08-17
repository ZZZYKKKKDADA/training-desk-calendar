using TrainingDeskCalendar.App.Domain;

namespace TrainingDeskCalendar.App.Services;

internal interface ITrainingPlanStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<TrainingPlan?> GetAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrainingPlan>> GetRangeAsync(
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken = default);
    Task SaveAsync(TrainingPlan plan, CancellationToken cancellationToken = default);
    Task SaveManyAsync(
        IReadOnlyCollection<TrainingPlan> plans,
        CancellationToken cancellationToken = default);
    Task DeleteAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrainingPlan>> GetAllAsync(CancellationToken cancellationToken = default);
    Task ReplaceAllAsync(
        IReadOnlyCollection<TrainingPlan> plans,
        CancellationToken cancellationToken = default);
}
