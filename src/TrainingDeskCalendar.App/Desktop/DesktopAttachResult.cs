namespace TrainingDeskCalendar.App.Desktop;

internal enum DesktopAttachStatus
{
    Attached,
    Fallback
}

internal sealed record DesktopAttachResult(
    DesktopAttachStatus Status,
    string? FailureReason);
