namespace TrainingDeskCalendar.App.Windowing;

internal sealed class WindowPlacementService
{
    private const double MinimumWidth = 840;
    private const double MinimumHeight = 360;
    private const double MinimumVisible = 96;

    public WindowPlacement Normalize(
        WindowPlacement saved,
        IReadOnlyCollection<MonitorWorkArea> monitors)
    {
        ArgumentNullException.ThrowIfNull(saved);
        ArgumentNullException.ThrowIfNull(monitors);

        MonitorWorkArea? savedMonitor = monitors.FirstOrDefault(
            monitor => monitor.Id == saved.MonitorId);
        MonitorWorkArea target = savedMonitor
            ?? monitors.FirstOrDefault(monitor => monitor.IsPrimary)
            ?? throw new InvalidOperationException("No monitor work area is available.");

        double effectiveMinimumWidth = Math.Min(MinimumWidth, target.Width);
        double effectiveMinimumHeight = Math.Min(MinimumHeight, target.Height);
        double width = Math.Clamp(saved.Width, effectiveMinimumWidth, target.Width);
        double height = Math.Clamp(saved.Height, effectiveMinimumHeight, target.Height);
        double minimumX = target.Left - width + MinimumVisible;
        double maximumX = target.Right - MinimumVisible;
        double minimumY = target.Top;
        double maximumY = target.Bottom - MinimumVisible;
        double requestedX = savedMonitor is null
            ? target.Left + ((target.Width - width) / 2)
            : saved.X;
        double requestedY = savedMonitor is null
            ? target.Top + ((target.Height - height) / 2)
            : saved.Y;

        return new WindowPlacement(
            target.Id,
            Math.Clamp(requestedX, minimumX, maximumX),
            Math.Clamp(requestedY, minimumY, maximumY),
            width,
            height);
    }
}
