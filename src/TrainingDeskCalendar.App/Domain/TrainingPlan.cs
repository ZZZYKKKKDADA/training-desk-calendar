namespace TrainingDeskCalendar.App.Domain;

internal sealed record TrainingPlan(
    DateOnly Date,
    string Text,
    TaskColorId Color,
    bool IsCompleted,
    DateTimeOffset UpdatedAtUtc)
{
    public static TrainingPlan Create(
        DateOnly date,
        string text,
        TaskColorId color = TaskColorId.Gray,
        bool isCompleted = false,
        DateTimeOffset? updatedAtUtc = null)
    {
        if (!Enum.IsDefined(color))
        {
            throw new ArgumentOutOfRangeException(nameof(color));
        }

        return new TrainingPlan(
            date,
            text ?? throw new ArgumentNullException(nameof(text)),
            color,
            isCompleted,
            (updatedAtUtc ?? DateTimeOffset.UtcNow).ToUniversalTime());
    }

    public bool IsDefaultEmpty =>
        string.IsNullOrWhiteSpace(Text) && !IsCompleted && Color == TaskColorId.Gray;
}
