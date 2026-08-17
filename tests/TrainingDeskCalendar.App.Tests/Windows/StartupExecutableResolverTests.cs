using TrainingDeskCalendar.App.Windows;
using Xunit;

namespace TrainingDeskCalendar.App.Tests.Windows;

public sealed class StartupExecutableResolverTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        "training-desk-calendar-startup-resolver",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Resolve_PrefersExistingAbsoluteLauncherPath()
    {
        string launcher = CreateFile("TrainingDeskCalendar.Launcher.exe");
        string application = CreateFile("app", "TrainingDeskCalendar.App.exe");

        string? result = StartupExecutableResolver.Resolve(launcher, application);

        Assert.Equal(launcher, result);
    }

    [Fact]
    public void Resolve_FallsBackToCurrentProcessForDevelopmentRuns()
    {
        string application = CreateFile("TrainingDeskCalendar.App.exe");

        string? result = StartupExecutableResolver.Resolve(
            Path.Combine(root, "missing-launcher.exe"),
            application);

        Assert.Equal(application, result);
    }

    [Fact]
    public void Resolve_RejectsRelativeAndMissingCandidates()
    {
        Assert.Null(StartupExecutableResolver.Resolve("launcher.exe", null));
        Assert.Null(StartupExecutableResolver.Resolve(null, null));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private string CreateFile(params string[] segments)
    {
        string path = segments.Aggregate(root, Path.Combine);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Empty);
        return path;
    }
}
