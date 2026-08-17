namespace TrainingDeskCalendar.App.Domain;

internal sealed record TwoWeekRange(DateOnly Start, DateOnly End)
{
    public IReadOnlyList<DateOnly> Days =>
        Enumerable.Range(0, End.DayNumber - Start.DayNumber + 1)
            .Select(Start.AddDays)
            .ToArray();
}
