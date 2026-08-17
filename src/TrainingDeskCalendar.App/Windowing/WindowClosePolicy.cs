namespace TrainingDeskCalendar.App.Windowing;

internal sealed class WindowClosePolicy
{
    public bool ShouldHide { get; private set; } = true;

    public void RequestExit() => ShouldHide = false;
}
