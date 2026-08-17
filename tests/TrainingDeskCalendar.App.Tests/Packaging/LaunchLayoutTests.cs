using TrainingDeskCalendar.Launcher;
using Xunit;

namespace TrainingDeskCalendar.App.Tests.Packaging;

public sealed class LaunchLayoutTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        "training-desk-calendar-launch-layout",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void FromBaseDirectory_ResolvesPrivateRuntimeAndApplicationPaths()
    {
        LaunchLayout layout = LaunchLayout.FromBaseDirectory(root);

        Assert.Equal(Path.Combine(root, "runtime"), layout.DotNetRoot);
        Assert.Equal(Path.Combine(root, "app"), layout.ApplicationDirectory);
        Assert.Equal(
            Path.Combine(root, "app", "TrainingDeskCalendar.App.exe"),
            layout.ApplicationPath);
    }

    [Fact]
    public void FromBaseDirectory_RejectsRelativePaths()
    {
        Assert.Throws<ArgumentException>(() =>
            LaunchLayout.FromBaseDirectory("payload"));
    }

    [Fact]
    public void Validate_RejectsMissingPrivateRuntime()
    {
        Directory.CreateDirectory(Path.Combine(root, "app"));
        File.WriteAllText(
            Path.Combine(root, "app", "TrainingDeskCalendar.App.exe"),
            string.Empty);
        LaunchLayout layout = LaunchLayout.FromBaseDirectory(root);

        Assert.Throws<DirectoryNotFoundException>(layout.Validate);
    }

    [Fact]
    public void Validate_RejectsMissingApplication()
    {
        Directory.CreateDirectory(Path.Combine(root, "runtime"));
        LaunchLayout layout = LaunchLayout.FromBaseDirectory(root);

        Assert.Throws<FileNotFoundException>(layout.Validate);
    }

    [Fact]
    public void Validate_AcceptsCompleteLayout()
    {
        Directory.CreateDirectory(Path.Combine(root, "runtime"));
        Directory.CreateDirectory(Path.Combine(root, "app"));
        File.WriteAllText(
            Path.Combine(root, "app", "TrainingDeskCalendar.App.exe"),
            string.Empty);
        LaunchLayout layout = LaunchLayout.FromBaseDirectory(root);

        layout.Validate();
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
