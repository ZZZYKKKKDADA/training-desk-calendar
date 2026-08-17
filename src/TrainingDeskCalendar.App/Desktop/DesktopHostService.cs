namespace TrainingDeskCalendar.App.Desktop;

internal sealed class DesktopHostService(IDesktopWindowApi desktopWindowApi)
{
    private const string UnknownFailureReason =
        "Desktop attachment failed without a native error message.";

    public DesktopAttachResult Attach(nint windowHandle)
    {
        if (windowHandle == nint.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(windowHandle));
        }

        if (desktopWindowApi.TryAttachToDesktop(windowHandle, out string? failureReason))
        {
            return new DesktopAttachResult(DesktopAttachStatus.Attached, null);
        }

        desktopWindowApi.RestoreAsTopLevel(windowHandle);
        return new DesktopAttachResult(
            DesktopAttachStatus.Fallback,
            failureReason ?? UnknownFailureReason);
    }
}
