using System.IO;

namespace TrainingDeskCalendar.App.Persistence;

internal sealed record AppDataPaths(
    string Root,
    string DatabasePath,
    string SettingsPath,
    string BackupDirectory)
{
    public static AppDataPaths ForCurrentUser()
    {
        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TrainingDeskCalendar");
        return ForRoot(root);
    }

    internal static AppDataPaths ForRoot(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        string fullRoot = Path.GetFullPath(root);
        return new AppDataPaths(
            fullRoot,
            Path.Combine(fullRoot, "training-desk-calendar.db"),
            Path.Combine(fullRoot, "settings.json"),
            Path.Combine(fullRoot, "backups"));
    }
}
