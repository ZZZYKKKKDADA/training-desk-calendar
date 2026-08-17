using TrainingDeskCalendar.App.Persistence;
using TrainingDeskCalendar.App.Windowing;
using Xunit;

namespace TrainingDeskCalendar.App.Tests.Calendar;

public sealed class WindowInteractionTests
{
    [Fact]
    public void LockedState_DisablesMovementAndResizeTogether()
    {
        var state = new WindowInteractionState();

        Assert.True(state.CanMove);
        Assert.True(state.CanResize);
        state.SetLocked(true);

        Assert.True(state.IsLocked);
        Assert.False(state.CanMove);
        Assert.False(state.CanResize);
        state.SetLocked(false);
        Assert.True(state.CanMove);
        Assert.True(state.CanResize);
    }

    [Theory]
    [InlineData(0, "#F7F8FA", "#20262B")]
    [InlineData(1, "#20262B", "#F7F8FA")]
    public void AppearancePalette_UsesContrastingSurfaceAndText(
        int themeId,
        string surface,
        string foreground)
    {
        AppTheme theme = (AppTheme)themeId;
        AppearancePalette palette = AppearancePalette.Create(theme, 0.75);

        Assert.Equal(surface, palette.SurfaceHex);
        Assert.Equal(foreground, palette.ForegroundHex);
        Assert.Equal(0.75, palette.Opacity);
        Assert.NotEqual(palette.SurfaceHex, palette.ForegroundHex);
    }

    [Theory]
    [InlineData(0.39)]
    [InlineData(1.01)]
    public void AppearancePalette_RejectsOpacityOutsideApprovedRange(double opacity)
    {
        Assert.Throws<InvalidDataException>(() => AppearancePalette.Create(AppTheme.Light, opacity));
    }

    [Fact]
    public void WindowStateService_RoundTripsPlacementAndMonitor()
    {
        AppSettings settings = AppSettings.Defaults with
        {
            WindowX = 30,
            WindowY = 40,
            WindowWidth = 960,
            WindowHeight = 420,
            MonitorId = "monitor-1"
        };
        var service = new WindowStateService();

        WindowPlacement placement = service.ToPlacement(settings);
        AppSettings updated = service.WithPlacement(settings, placement with
        {
            X = 80,
            Y = 90,
            Width = 1000,
            Height = 440,
            MonitorId = "monitor-2"
        });

        Assert.Equal("monitor-2", updated.MonitorId);
        Assert.Equal(80, updated.WindowX);
        Assert.Equal(440, updated.WindowHeight);
        Assert.Equal(1, updated.Version);
    }
}
