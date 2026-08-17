using TrainingDeskCalendar.App.Windowing;
using Xunit;

namespace TrainingDeskCalendar.App.Tests.Windowing;

public sealed class WindowPlacementServiceTests
{
    private static readonly MonitorWorkArea Primary =
        new("primary", 0, 0, 1920, 1040, IsPrimary: true);

    [Fact]
    public void Normalize_PreservesAVisiblePlacementOnTheSavedMonitor()
    {
        var service = new WindowPlacementService();
        var saved = new WindowPlacement("primary", 100, 120, 1120, 470);

        WindowPlacement result = service.Normalize(saved, [Primary]);

        Assert.Equal(saved, result);
    }

    [Fact]
    public void Normalize_MovesToPrimaryMonitorWhenSavedMonitorIsMissing()
    {
        var service = new WindowPlacementService();
        var saved = new WindowPlacement("removed", 2600, 100, 1120, 470);

        WindowPlacement result = service.Normalize(saved, [Primary]);

        Assert.Equal("primary", result.MonitorId);
        Assert.Equal(400, result.X);
        Assert.Equal(285, result.Y);
    }

    [Fact]
    public void Normalize_ClampsSizeAndKeepsNinetySixPixelsVisible()
    {
        var service = new WindowPlacementService();
        var saved = new WindowPlacement("primary", 1900, 1020, 400, 200);

        WindowPlacement result = service.Normalize(saved, [Primary]);

        Assert.Equal(840, result.Width);
        Assert.Equal(360, result.Height);
        Assert.True(result.X <= Primary.Right - 96);
        Assert.True(result.Y <= Primary.Bottom - 96);
    }
}
