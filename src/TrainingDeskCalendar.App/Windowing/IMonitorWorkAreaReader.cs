namespace TrainingDeskCalendar.App.Windowing;

internal interface IMonitorWorkAreaReader
{
    IReadOnlyList<MonitorWorkArea> GetAll();
    string GetMonitorIdForWindow(nint windowHandle);
}
