namespace TrainingDeskCalendar.App.Domain;

internal sealed class CalendarRangeService
{
    public TwoWeekRange Containing(DateOnly date)
    {
        int daysFromMonday = ((int)date.DayOfWeek + 6) % 7;
        DateOnly start = date.AddDays(-daysFromMonday);
        return new TwoWeekRange(start, start.AddDays(13));
    }

    public TwoWeekRange Move(TwoWeekRange current, int pages)
    {
        ArgumentNullException.ThrowIfNull(current);
        DateOnly start = current.Start.AddDays(pages * 14);
        return new TwoWeekRange(start, start.AddDays(13));
    }
}
