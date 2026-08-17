using TrainingDeskCalendar.App.Domain;

namespace TrainingDeskCalendar.App.Persistence;

internal sealed record SnapshotFormat(
    int FormatVersion,
    DateTimeOffset ExportedAtUtc,
    IReadOnlyList<TrainingPlan> Plans,
    AppSettings Settings);
