namespace TrainingDeskCalendar.App.Windowing;

internal sealed record MonitorWorkArea(
    string Id,
    double Left,
    double Top,
    double Width,
    double Height,
    bool IsPrimary)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;
}
