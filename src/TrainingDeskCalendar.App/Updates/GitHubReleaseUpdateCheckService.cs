using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using TrainingDeskCalendar.App.Services;

namespace TrainingDeskCalendar.App.Updates;

internal sealed class GitHubReleaseUpdateCheckService : IUpdateCheckService
{
    private static readonly TimeSpan AutomaticCheckInterval = TimeSpan.FromHours(24);
    private const string ManualFailureMessage = "无法检查更新，请确认网络连接后重试。";

    private readonly HttpClient httpClient;
    private readonly RepositoryMetadata? repository;
    private readonly ReleaseVersion currentVersion;
    private readonly TimeProvider timeProvider;
    private readonly Func<DateTimeOffset, CancellationToken, Task> saveLastCheck;
    private DateTimeOffset? lastUpdateCheckUtc;

    public GitHubReleaseUpdateCheckService(
        HttpClient httpClient,
        RepositoryMetadata? repository,
        ReleaseVersion currentVersion,
        TimeProvider timeProvider,
        DateTimeOffset? lastUpdateCheckUtc,
        Func<DateTimeOffset, CancellationToken, Task> saveLastCheck)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.repository = repository;
        this.currentVersion = currentVersion;
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.lastUpdateCheckUtc = lastUpdateCheckUtc;
        this.saveLastCheck = saveLastCheck ?? throw new ArgumentNullException(nameof(saveLastCheck));
    }

    public async Task<UpdateCheckResult> CheckAsync(
        UpdateCheckMode mode = UpdateCheckMode.Manual,
        CancellationToken cancellationToken = default)
    {
        if (repository is null) return UpdateCheckResult.Unavailable;

        DateTimeOffset now = timeProvider.GetUtcNow();
        if (mode == UpdateCheckMode.Automatic &&
            lastUpdateCheckUtc is DateTimeOffset lastCheck &&
            now - lastCheck < AutomaticCheckInterval)
        {
            return UpdateCheckResult.Unavailable;
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://api.github.com/repos/{repository.Slug}/releases/latest");
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue(
                "TrainingDeskCalendar",
                currentVersion.ToString()));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(
                "application/vnd.github+json"));
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

            using HttpResponseMessage response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();
            await using Stream content = await response.Content.ReadAsStreamAsync(cancellationToken);
            LatestReleaseResponse? release = await JsonSerializer.DeserializeAsync<LatestReleaseResponse>(
                content,
                cancellationToken: cancellationToken);
            if (release is null)
            {
                throw new JsonException("GitHub returned an empty release response.");
            }

            ReleaseVersion latestVersion = ReleaseVersion.Parse(release.TagName);
            Uri releaseUri = ValidateReleaseUri(release.HtmlUrl, repository);
            await saveLastCheck(now, cancellationToken);
            lastUpdateCheckUtc = now;

            return latestVersion > currentVersion
                ? new UpdateCheckResult(
                    UpdateCheckStatus.UpdateAvailable,
                    latestVersion,
                    releaseUri)
                : new UpdateCheckResult(UpdateCheckStatus.UpToDate);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is HttpRequestException or JsonException or FormatException or IOException or
                OperationCanceledException)
        {
            return new UpdateCheckResult(
                UpdateCheckStatus.Failed,
                ErrorMessage: mode == UpdateCheckMode.Manual ? ManualFailureMessage : null);
        }
    }

    private static Uri ValidateReleaseUri(string value, RepositoryMetadata repository)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
            !uri.IsDefaultPort ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            !uri.AbsolutePath.StartsWith(
                $"/{repository.Slug}/releases/",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException("GitHub release URL is invalid.");
        }

        return uri;
    }

    private sealed record LatestReleaseResponse(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("html_url")] string HtmlUrl);
}
