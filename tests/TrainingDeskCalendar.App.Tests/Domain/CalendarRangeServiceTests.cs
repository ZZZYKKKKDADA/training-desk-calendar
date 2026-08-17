using TrainingDeskCalendar.App.Domain;
using Xunit;

namespace TrainingDeskCalendar.App.Tests.Domain;

public sealed class CalendarRangeServiceTests
{
    [Fact]
    public void Containing_ReturnsMondayThroughSundayForTwoWeeks()
    {
        var service = new CalendarRangeService();

        TwoWeekRange result = service.Containing(new DateOnly(2026, 8, 19));

        Assert.Equal(new DateOnly(2026, 8, 17), result.Start);
        Assert.Equal(new DateOnly(2026, 8, 30), result.End);
        Assert.Equal(14, result.Days.Count);
        Assert.Equal(DayOfWeek.Monday, result.Start.DayOfWeek);
        Assert.Equal(DayOfWeek.Sunday, result.End.DayOfWeek);
    }

    [Fact]
    public void Containing_HandlesSundayAndYearBoundary()
    {
        var service = new CalendarRangeService();

        TwoWeekRange result = service.Containing(new DateOnly(2027, 1, 3));

        Assert.Equal(new DateOnly(2026, 12, 28), result.Start);
        Assert.Equal(new DateOnly(2027, 1, 10), result.End);
    }

    [Fact]
    public void Move_AdvancesByExactlyFourteenDaysPerPage()
    {
        var service = new CalendarRangeService();
        var current = service.Containing(new DateOnly(2026, 8, 19));

        TwoWeekRange result = service.Move(current, 2);

        Assert.Equal(new DateOnly(2026, 9, 14), result.Start);
        Assert.Equal(new DateOnly(2026, 9, 27), result.End);
    }
}
