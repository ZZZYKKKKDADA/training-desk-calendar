using TrainingDeskCalendar.App.Domain;
using Xunit;

namespace TrainingDeskCalendar.App.Tests.Domain;

public sealed class TrainingPlanTests
{
    [Fact]
    public void Create_PreservesMultilineTextAndUtcTimestamp()
    {
        var timestamp = new DateTimeOffset(2026, 8, 19, 8, 30, 0, TimeSpan.FromHours(8));

        TrainingPlan result = TrainingPlan.Create(
            new DateOnly(2026, 8, 19),
            "胸部训练\n卧推 4 × 8",
            TaskColorId.Teal,
            isCompleted: true,
            timestamp);

        Assert.Equal("胸部训练\n卧推 4 × 8", result.Text);
        Assert.Equal(TaskColorId.Teal, result.Color);
        Assert.True(result.IsCompleted);
        Assert.Equal(TimeSpan.Zero, result.UpdatedAtUtc.Offset);
        Assert.Equal(timestamp.UtcDateTime, result.UpdatedAtUtc.UtcDateTime);
    }

    [Fact]
    public void Create_RejectsAnUndefinedTaskColor()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TrainingPlan.Create(
            new DateOnly(2026, 8, 19),
            "计划",
            (TaskColorId)7));
    }

    [Fact]
    public void DefaultEmptyPlanIsOmittable()
    {
        TrainingPlan result = TrainingPlan.Create(new DateOnly(2026, 8, 19), string.Empty);

        Assert.True(result.IsDefaultEmpty);
    }
}
