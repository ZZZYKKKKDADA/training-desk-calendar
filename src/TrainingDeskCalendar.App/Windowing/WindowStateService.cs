using TrainingDeskCalendar.App.Persistence;

namespace TrainingDeskCalendar.App.Windowing;

internal sealed class WindowStateService
{
    public WindowPlacement ToPlacement(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        return new WindowPlacement(
            settings.MonitorId,
            settings.WindowX,
            settings.WindowY,
            settings.WindowWidth,
            settings.WindowHeight);
    }

    public AppSettings WithPlacement(AppSettings settings, WindowPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(placement);
        return (settings with
        {
            WindowX = placement.X,
            WindowY = placement.Y,
            WindowWidth = placement.Width,
            WindowHeight = placement.Height,
            MonitorId = placement.MonitorId
        }).Validate();
    }
}
