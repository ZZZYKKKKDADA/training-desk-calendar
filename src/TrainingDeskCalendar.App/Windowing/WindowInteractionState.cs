namespace TrainingDeskCalendar.App.Windowing;

internal sealed class WindowInteractionState
{
    public bool IsLocked { get; private set; }
    public bool CanMove => !IsLocked;
    public bool CanResize => !IsLocked;

    public void SetLocked(bool locked) => IsLocked = locked;
}
