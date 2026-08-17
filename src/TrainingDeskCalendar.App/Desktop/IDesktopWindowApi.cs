namespace TrainingDeskCalendar.App.Desktop;

internal interface IDesktopWindowApi
{
    bool TryAttachToDesktop(nint windowHandle, out string? failureReason);

    void RestoreAsTopLevel(nint windowHandle);
}
