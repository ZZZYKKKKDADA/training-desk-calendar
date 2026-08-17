using System.IO;

namespace TrainingDeskCalendar.App.Windows;

internal static class StartupExecutableResolver
{
    internal const string LauncherEnvironmentName = "TRAINING_DESK_CALENDAR_LAUNCHER";

    public static string? Resolve(
        string? launcherPath,
        string? currentProcessPath)
    {
        return ResolveCandidate(launcherPath) ?? ResolveCandidate(currentProcessPath);
    }

    private static string? ResolveCandidate(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            !Path.IsPathFullyQualified(path) ||
            !File.Exists(path))
        {
            return null;
        }

        return Path.GetFullPath(path);
    }
}
