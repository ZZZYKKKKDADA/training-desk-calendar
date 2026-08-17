using System.Net;
using System.Text;
using TrainingDeskCalendar.App.Services;
using TrainingDeskCalendar.App.Updates;
using Xunit;

namespace TrainingDeskCalendar.App.Tests.Updates;

public sealed class GitHubReleaseUpdateCheckServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 18, 1, 0, 0, TimeSpan.Zero);
    private static readonly RepositoryMetadata Repository = RepositoryMetadata.Parse(
        "https://github.com/owner/repo");
    private static readonly ReleaseVersion CurrentVersion = new(1, 2, 3);

    [Fact]
    public async Task ManualCheck_RequestsLatestReleaseWithRequiredHeadersAndReturnsUpdate()
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(JsonResponse(
            """
            {"tag_name":"v1.3.0","html_url":"https://github.com/owner/repo/releases/tag/v1.3.0","ignored":"value"}
            """)));
        DateTimeOffset? saved = null;
        var service = CreateService(handler, save: (value, _) =>
        {
            saved = value;
            return Task.CompletedTask;
        });

        UpdateCheckResult result = await service.CheckAsync(UpdateCheckMode.Manual);

        Assert.Equal(UpdateCheckStatus.UpdateAvailable, result.Status);
        Assert.Equal(new ReleaseVersion(1, 3, 0), result.LatestVersion);
        Assert.Equal(
            new Uri("https://github.com/owner/repo/releases/tag/v1.3.0"),
            result.ReleaseUri);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(Now, saved);
        HttpRequestMessage request = Assert.Single(handler.Requests);
        Assert.Equal(
            new Uri("https://api.github.com/repos/owner/repo/releases/latest"),
            request.RequestUri);
        Assert.NotEmpty(request.Headers.UserAgent);
        Assert.Contains(
            request.Headers.Accept,
            value => value.MediaType == "application/vnd.github+json");
    }

    [Theory]
    [InlineData("v1.2.3")]
    [InlineData("v1.2.2")]
    public async Task ManualCheck_ReturnsUpToDateForSameOrOlderRelease(string tag)
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(JsonResponse(
            $$"""{"tag_name":"{{tag}}","html_url":"https://github.com/owner/repo/releases/tag/{{tag}}"}""")));
        var service = CreateService(handler);

        UpdateCheckResult result = await service.CheckAsync(UpdateCheckMode.Manual);

        Assert.Equal(UpdateCheckStatus.UpToDate, result.Status);
        Assert.Null(result.LatestVersion);
        Assert.Null(result.ReleaseUri);
    }

    [Fact]
    public async Task Check_ReturnsUnavailableWithoutRepositoryMetadataAndDoesNotSend()
    {
        var handler = new RecordingHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(new InvalidOperationException()));
        var service = new GitHubReleaseUpdateCheckService(
            new HttpClient(handler),
            repository: null,
            CurrentVersion,
            new FixedTimeProvider(Now),
            lastUpdateCheckUtc: null,
            (_, _) => Task.CompletedTask);

        UpdateCheckResult result = await service.CheckAsync(UpdateCheckMode.Manual);

        Assert.Equal(UpdateCheckStatus.Unavailable, result.Status);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task AutomaticCheck_SkipsWhenLastSuccessfulCheckWasLessThanOneDayAgo()
    {
        var handler = new RecordingHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(new InvalidOperationException()));
        var service = CreateService(handler, lastCheck: Now.AddHours(-23));

        UpdateCheckResult result = await service.CheckAsync(UpdateCheckMode.Automatic);

        Assert.Equal(UpdateCheckStatus.Unavailable, result.Status);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task AutomaticNetworkFailure_IsSilentWhileManualFailureHasMessage()
    {
        var automaticHandler = new RecordingHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("offline")));
        var manualHandler = new RecordingHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("offline")));

        UpdateCheckResult automatic = await CreateService(automaticHandler)
            .CheckAsync(UpdateCheckMode.Automatic);
        UpdateCheckResult manual = await CreateService(manualHandler)
            .CheckAsync(UpdateCheckMode.Manual);

        Assert.Equal(UpdateCheckStatus.Failed, automatic.Status);
        Assert.Null(automatic.ErrorMessage);
        Assert.Equal(UpdateCheckStatus.Failed, manual.Status);
        Assert.False(string.IsNullOrWhiteSpace(manual.ErrorMessage));
        Assert.DoesNotContain("offline", manual.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Check_PropagatesCallerCancellation()
    {
        var handler = new RecordingHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException();
        });
        var service = CreateService(handler);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.CheckAsync(UpdateCheckMode.Manual, cancellation.Token));
    }

    [Theory]
    [InlineData("v1.2.3-beta", "https://github.com/owner/repo/releases/tag/v1.2.3-beta")]
    [InlineData("v1.3.0", "http://github.com/owner/repo/releases/tag/v1.3.0")]
    [InlineData("v1.3.0", "https://example.com/owner/repo/releases/tag/v1.3.0")]
    public async Task ManualCheck_RejectsInvalidReleaseMetadata(string tag, string url)
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(JsonResponse(
            $$"""{"tag_name":"{{tag}}","html_url":"{{url}}"}""")));

        UpdateCheckResult result = await CreateService(handler)
            .CheckAsync(UpdateCheckMode.Manual);

        Assert.Equal(UpdateCheckStatus.Failed, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    private static GitHubReleaseUpdateCheckService CreateService(
        HttpMessageHandler handler,
        DateTimeOffset? lastCheck = null,
        Func<DateTimeOffset, CancellationToken, Task>? save = null) =>
        new(
            new HttpClient(handler),
            Repository,
            CurrentVersion,
            new FixedTimeProvider(Now),
            lastCheck,
            save ?? ((_, _) => Task.CompletedTask));

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory;

        public RecordingHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        {
            this.responseFactory = responseFactory ?? throw new ArgumentNullException(nameof(responseFactory));
        }

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return await responseFactory(request, cancellationToken);
        }
    }
}
