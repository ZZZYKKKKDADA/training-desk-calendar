using System.IO;

namespace TrainingDeskCalendar.App.Persistence;

internal enum AppTheme
{
    Light,
    Dark
}

internal sealed record AppSettings(
    int Version,
    double WindowX,
    double WindowY,
    double WindowWidth,
    double WindowHeight,
    string MonitorId,
    bool IsLocked,
    AppTheme Theme,
    double Opacity,
    bool StartWithWindows,
    DateTimeOffset? LastUpdateCheckUtc)
{
    public static AppSettings Defaults => new(
        1,
        100,
        100,
        1120,
        470,
        string.Empty,
        false,
        AppTheme.Light,
        1.0,
        true,
        null);

    public AppSettings Validate()
    {
        if (Version != 1 ||
            !double.IsFinite(WindowX) ||
            !double.IsFinite(WindowY) ||
            !double.IsFinite(WindowWidth) ||
            !double.IsFinite(WindowHeight) ||
            WindowWidth < 840 ||
            WindowHeight < 360 ||
            !double.IsFinite(Opacity) ||
            Opacity is < 0.4 or > 1.0 ||
            MonitorId is null ||
            !Enum.IsDefined(Theme) ||
            LastUpdateCheckUtc is { Offset: not { Ticks: 0 } })
        {
            throw new InvalidDataException("Settings are invalid.");
        }

        return this;
    }
}
