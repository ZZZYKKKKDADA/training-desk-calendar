using System.IO;
using TrainingDeskCalendar.App.Persistence;

namespace TrainingDeskCalendar.App.Windowing;

internal sealed record AppearancePalette(
    string SurfaceHex,
    string ForegroundHex,
    string BorderHex,
    double Opacity)
{
    public static AppearancePalette Create(AppTheme theme, double opacity)
    {
        if (!Enum.IsDefined(theme) || !double.IsFinite(opacity) || opacity is < 0.4 or > 1.0)
        {
            throw new InvalidDataException("Window appearance settings are invalid.");
        }

        return theme == AppTheme.Dark
            ? new AppearancePalette("#20262B", "#F7F8FA", "#80FFFFFF", opacity)
            : new AppearancePalette("#F7F8FA", "#20262B", "#40000000", opacity);
    }
}
