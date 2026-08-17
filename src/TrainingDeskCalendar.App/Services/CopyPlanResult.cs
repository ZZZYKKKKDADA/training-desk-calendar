namespace TrainingDeskCalendar.App.Services;

internal sealed record CopyConflict(DateOnly SourceDate, DateOnly TargetDate);

internal sealed record CopyPlanResult(
    bool Applied,
    IReadOnlyList<CopyConflict> Conflicts);
