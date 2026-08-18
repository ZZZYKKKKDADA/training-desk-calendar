using System.Windows;

namespace TrainingDeskCalendar.App.Windowing;

internal sealed class WindowDragService
{
    private Point pointerOrigin;
    private Point windowOrigin;
    private bool isDragging;

    public void Begin(Point pointerPosition, Point windowPosition)
    {
        pointerOrigin = pointerPosition;
        windowOrigin = windowPosition;
        isDragging = true;
    }

    public bool TryGetPosition(Point pointerPosition, out Point windowPosition)
    {
        if (!isDragging)
        {
            windowPosition = default;
            return false;
        }

        windowPosition = new Point(
            windowOrigin.X + pointerPosition.X - pointerOrigin.X,
            windowOrigin.Y + pointerPosition.Y - pointerOrigin.Y);
        return true;
    }

    public void End() => isDragging = false;
}
