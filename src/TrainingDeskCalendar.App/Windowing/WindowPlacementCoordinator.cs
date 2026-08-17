using System.Windows;

namespace TrainingDeskCalendar.App.Windowing;

internal sealed class WindowPlacementCoordinator(
    Window window,
    nint windowHandle,
    IMonitorWorkAreaReader monitorReader,
    WindowPlacementService placementService)
{
    private string lastKnownMonitorId = monitorReader.GetMonitorIdForWindow(windowHandle);
    private bool applyingPlacement;

    public void TrackCurrentMonitor()
    {
        if (applyingPlacement)
        {
            return;
        }

        lastKnownMonitorId = monitorReader.GetMonitorIdForWindow(windowHandle);
    }

    public void EnsureVisible()
    {
        IReadOnlyList<MonitorWorkArea> monitors = monitorReader.GetAll();
        var current = new WindowPlacement(
            lastKnownMonitorId,
            window.Left,
            window.Top,
            window.ActualWidth,
            window.ActualHeight);
        WindowPlacement normalized = placementService.Normalize(current, monitors);

        if (ApproximatelyEqual(current, normalized))
        {
            lastKnownMonitorId = normalized.MonitorId;
            return;
        }

        applyingPlacement = true;
        try
        {
            window.Left = normalized.X;
            window.Top = normalized.Y;
            window.Width = normalized.Width;
            window.Height = normalized.Height;
            lastKnownMonitorId = normalized.MonitorId;
        }
        finally
        {
            applyingPlacement = false;
        }
    }

    private static bool ApproximatelyEqual(WindowPlacement left, WindowPlacement right)
    {
        return left.MonitorId == right.MonitorId &&
               Math.Abs(left.X - right.X) < 0.5 &&
               Math.Abs(left.Y - right.Y) < 0.5 &&
               Math.Abs(left.Width - right.Width) < 0.5 &&
               Math.Abs(left.Height - right.Height) < 0.5;
    }
}
