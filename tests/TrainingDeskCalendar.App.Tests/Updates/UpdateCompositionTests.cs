using System.Net;
using System.Text;
using TrainingDeskCalendar.App.Persistence;
using TrainingDeskCalendar.App.Services;
using TrainingDeskCalendar.App.Updates;
using Xunit;

namespace TrainingDeskCalendar.App.Tests.Updates;

public sealed class UpdateCompositionTests : IDisposable
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 18, 2, 0, 0, TimeSpan.Zero);
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        "training-desk-calendar-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LocalBuild_UsesRealUnavailableServiceWithoutNetwork()
    {
        await using AppComposition composition = await AppComposition.CreateAsync(
            AppDataPaths.ForRoot(root),
            new DateOnly(2026, 8, 18),
            new FixedTimeProvider(Now),
            buildMetadata: new ReleaseBuildMetadata(new ReleaseVersion(0, 1, 0), null));

        UpdateCheckResult result = await composition.UpdateCheckService.CheckAsync(
            UpdateCheckMode.Manual);

        Assert.IsType<GitHubReleaseUpdateCheckService>(composition.UpdateCheckService);
        Assert.Equal(UpdateCheckStatus.Unavailable, result.Status);
        Assert.NotNull(composition.UpdateCheckCoordinator);
    }

    [Fact]
    public async Task SuccessfulCheck_PersistsLastCheckTimeInCompositionAndSettingsFile()
    {
        var handler = new StaticResponseHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {"tag_name":"v0.1.0","html_url":"https://github.com/owner/repo/releases/tag/v0.1.0"}
                """,
                Encoding.UTF8,
                "application/json")
        });
        var metadata = new ReleaseBuildMetadata(
            new ReleaseVersion(0, 1, 0),
            RepositoryMetadata.Parse("https://github.com/owner/repo"));
        await using AppComposition composition = await AppComposition.CreateAsync(
            AppDataPaths.ForRoot(root),
            new DateOnly(2026, 8, 18),
            new FixedTimeProvider(Now),
            buildMetadata: metadata,
            updateHttpClient: new HttpClient(handler));

        UpdateCheckResult result = await composition.UpdateCheckService.CheckAsync(
            UpdateCheckMode.Automatic);
        AppSettings persisted = await composition.SettingsStore.LoadAsync();

        Assert.Equal(UpdateCheckStatus.UpToDate, result.Status);
        Assert.Equal(Now, composition.Settings.LastUpdateCheckUtc);
        Assert.Equal(Now, persisted.LastUpdateCheckUtc);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public void ApplicationEntryPoints_UseAutomaticAndManualModesExplicitly()
    {
        string root = FindSolutionRoot();
        string application = File.ReadAllText(Path.Combine(
            root,
            "src",
            "TrainingDeskCalendar.App",
            "App.xaml.cs"));
        string mainWindow = File.ReadAllText(Path.Combine(
            root,
            "src",
            "TrainingDeskCalendar.App",
            "MainWindow.xaml.cs"));

        Assert.Contains("DispatcherPriority.ApplicationIdle", application, StringComparison.Ordinal);
        Assert.Contains("UpdateCheckMode.Automatic", application, StringComparison.Ordinal);
        Assert.Contains("UpdateCheckMode.Manual", application, StringComparison.Ordinal);
        Assert.Contains("UpdateCheckMode.Manual", mainWindow, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    private static string FindSolutionRoot()
    {
        string? directory = AppContext.BaseDirectory;
        while (directory is not null &&
               !File.Exists(Path.Combine(directory, "TrainingDeskCalendar.sln")))
        {
            directory = Directory.GetParent(directory)?.FullName;
        }

        return directory ?? throw new DirectoryNotFoundException();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class StaticResponseHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(response);
        }
    }
}
