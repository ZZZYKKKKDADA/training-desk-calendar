using System.IO;

namespace TrainingDeskCalendar.App.Diagnostics;

internal static class PrototypeReadySignal
{
    public static void Write(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, DateTimeOffset.UtcNow.ToString("O"));
    }
}
