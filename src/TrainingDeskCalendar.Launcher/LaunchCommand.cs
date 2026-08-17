using System.Diagnostics;

namespace TrainingDeskCalendar.Launcher;

internal sealed record LaunchCommand(
    string FileName,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string> Environment,
    IReadOnlyList<string> Arguments)
{
    internal const string LauncherEnvironmentName = "TRAINING_DESK_CALENDAR_LAUNCHER";

    public static LaunchCommand Create(
        LaunchLayout layout,
        string launcherPath,
        IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentException.ThrowIfNullOrWhiteSpace(launcherPath);
        ArgumentNullException.ThrowIfNull(arguments);
        if (!Path.IsPathFullyQualified(launcherPath))
        {
            throw new ArgumentException(
                "The launcher path must be absolute.",
                nameof(launcherPath));
        }

        return new LaunchCommand(
            layout.ApplicationPath,
            layout.ApplicationDirectory,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["DOTNET_ROOT_X64"] = layout.DotNetRoot,
                [LauncherEnvironmentName] = Path.GetFullPath(launcherPath)
            },
            arguments.ToArray());
    }

    public ProcessStartInfo ToProcessStartInfo()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = FileName,
            WorkingDirectory = WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (string argument in Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach ((string name, string value) in Environment)
        {
            startInfo.Environment[name] = value;
        }

        return startInfo;
    }
}
