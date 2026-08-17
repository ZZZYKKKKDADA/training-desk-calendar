using TrainingDeskCalendar.Launcher;
using Xunit;

namespace TrainingDeskCalendar.App.Tests.Packaging;

public sealed class LauncherCommandTests
{
    [Fact]
    public void Create_UsesPrivateRuntimeLauncherPathAndForwardedArguments()
    {
        string root = Path.Combine(Path.GetTempPath(), "TrainingDeskCalendar");
        string launcherPath = Path.Combine(root, "TrainingDeskCalendar.Launcher.exe");
        LaunchLayout layout = LaunchLayout.FromBaseDirectory(root);

        LaunchCommand command = LaunchCommand.Create(
            layout,
            launcherPath,
            ["--ready-file", @"C:\Temp Folder\ready.txt"]);

        Assert.Equal(layout.ApplicationPath, command.FileName);
        Assert.Equal(layout.ApplicationDirectory, command.WorkingDirectory);
        Assert.Equal(layout.DotNetRoot, command.Environment["DOTNET_ROOT_X64"]);
        Assert.Equal(launcherPath, command.Environment["TRAINING_DESK_CALENDAR_LAUNCHER"]);
        Assert.Equal(
            ["--ready-file", @"C:\Temp Folder\ready.txt"],
            command.Arguments);
    }

    [Fact]
    public void Create_RejectsLauncherOutsideAnAbsolutePath()
    {
        LaunchLayout layout = LaunchLayout.FromBaseDirectory(
            Path.Combine(Path.GetTempPath(), "TrainingDeskCalendar"));

        Assert.Throws<ArgumentException>(() =>
            LaunchCommand.Create(layout, "launcher.exe", []));
    }
}
