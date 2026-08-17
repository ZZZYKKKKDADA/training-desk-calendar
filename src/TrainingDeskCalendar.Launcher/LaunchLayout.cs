namespace TrainingDeskCalendar.Launcher;

internal sealed record LaunchLayout(
    string BaseDirectory,
    string DotNetRoot,
    string ApplicationDirectory,
    string ApplicationPath)
{
    public static LaunchLayout FromBaseDirectory(string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        if (!Path.IsPathFullyQualified(baseDirectory))
        {
            throw new ArgumentException(
                "The launcher base directory must be absolute.",
                nameof(baseDirectory));
        }

        string root = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(baseDirectory));
        string applicationDirectory = Path.Combine(root, "app");
        return new LaunchLayout(
            root,
            Path.Combine(root, "runtime"),
            applicationDirectory,
            Path.Combine(applicationDirectory, "TrainingDeskCalendar.App.exe"));
    }

    public void Validate()
    {
        if (!Directory.Exists(DotNetRoot))
        {
            throw new DirectoryNotFoundException(
                "The private Windows Desktop Runtime directory is missing.");
        }

        if (!File.Exists(ApplicationPath))
        {
            throw new FileNotFoundException(
                "The Training Desk Calendar application is missing.",
                ApplicationPath);
        }
    }
}
