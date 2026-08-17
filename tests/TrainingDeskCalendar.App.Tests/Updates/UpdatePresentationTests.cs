using System.Diagnostics;
using TrainingDeskCalendar.App.Services;
using TrainingDeskCalendar.App.Updates;
using Xunit;

namespace TrainingDeskCalendar.App.Tests.Updates;

public sealed class UpdatePresentationTests
{
    [Fact]
    public async Task AutomaticFailure_DoesNotNotifyUser()
    {
        var notifications = new RecordingNotifications();
        var coordinator = CreateCoordinator(
            new UpdateCheckResult(UpdateCheckStatus.Failed),
            notifications);

        await coordinator.CheckAsync(UpdateCheckMode.Automatic);

        Assert.Empty(notifications.Messages);
    }

    [Fact]
    public async Task ManualFailure_ShowsServiceError()
    {
        var notifications = new RecordingNotifications();
        var coordinator = CreateCoordinator(
            new UpdateCheckResult(UpdateCheckStatus.Failed, ErrorMessage: "无法检查更新。"),
            notifications);

        await coordinator.CheckAsync(UpdateCheckMode.Manual);

        Assert.Contains("无法检查更新。", notifications.Messages);
    }

    [Fact]
    public async Task ManualUnavailable_ExplainsMissingRepositoryMetadata()
    {
        var notifications = new RecordingNotifications();
        var coordinator = CreateCoordinator(UpdateCheckResult.Unavailable, notifications);

        await coordinator.CheckAsync(UpdateCheckMode.Manual);

        Assert.Contains(
            notifications.Messages,
            message => message.Contains("当前构建未配置 GitHub 仓库", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public async Task UpdateAvailable_LaunchesReleaseOnlyAfterConfirmation(
        bool confirmed,
        int expectedLaunches)
    {
        var notifications = new RecordingNotifications { ConfirmationResult = confirmed };
        var launcher = new RecordingLauncher();
        var releaseUri = new Uri("https://github.com/owner/repo/releases/tag/v1.3.0");
        var coordinator = CreateCoordinator(
            new UpdateCheckResult(
                UpdateCheckStatus.UpdateAvailable,
                new ReleaseVersion(1, 3, 0),
                releaseUri),
            notifications,
            launcher);

        await coordinator.CheckAsync(UpdateCheckMode.Manual);

        Assert.Contains("1.3.0", Assert.Single(notifications.Confirmations));
        Assert.Equal(expectedLaunches, launcher.Uris.Count);
        if (confirmed) Assert.Equal(releaseUri, Assert.Single(launcher.Uris));
    }

    [Fact]
    public async Task ManualUpToDate_ShowsCurrentStatus()
    {
        var notifications = new RecordingNotifications();
        var coordinator = CreateCoordinator(
            new UpdateCheckResult(UpdateCheckStatus.UpToDate),
            notifications);

        await coordinator.CheckAsync(UpdateCheckMode.Manual);

        Assert.Contains(
            notifications.Messages,
            message => message.Contains("当前已是最新版本", StringComparison.Ordinal));
    }

    [Fact]
    public void ExternalLauncher_OpensHttpsWithShellExecute()
    {
        ProcessStartInfo? captured = null;
        IExternalUriLauncher launcher = new ExternalUriLauncher(startInfo => captured = startInfo);
        var uri = new Uri("https://github.com/owner/repo");

        launcher.Open(uri);

        Assert.NotNull(captured);
        Assert.Equal(uri.AbsoluteUri, captured.FileName);
        Assert.True(captured.UseShellExecute);
    }

    [Fact]
    public void ExternalLauncher_RejectsNonHttpsUriWithoutStartingProcess()
    {
        bool started = false;
        var launcher = new ExternalUriLauncher(_ => started = true);

        Assert.Throws<ArgumentException>(() => launcher.Open(new Uri("http://example.com")));
        Assert.False(started);
    }

    private static UpdateCheckCoordinator CreateCoordinator(
        UpdateCheckResult result,
        RecordingNotifications notifications,
        RecordingLauncher? launcher = null) =>
        new(
            new StubUpdateCheckService(result),
            notifications,
            launcher ?? new RecordingLauncher());

    private sealed class StubUpdateCheckService(UpdateCheckResult result) : IUpdateCheckService
    {
        public Task<UpdateCheckResult> CheckAsync(
            UpdateCheckMode mode = UpdateCheckMode.Manual,
            CancellationToken cancellationToken = default) => Task.FromResult(result);
    }

    private sealed class RecordingNotifications : IUpdateNotifications
    {
        public bool ConfirmationResult { get; init; }
        public List<string> Messages { get; } = [];
        public List<string> Confirmations { get; } = [];

        public void ShowInformation(string message) => Messages.Add(message);
        public void ShowError(string message) => Messages.Add(message);
        public bool ConfirmOpenRelease(string message)
        {
            Confirmations.Add(message);
            return ConfirmationResult;
        }
    }

    private sealed class RecordingLauncher : IExternalUriLauncher
    {
        public List<Uri> Uris { get; } = [];
        public void Open(Uri uri) => Uris.Add(uri);
    }
}
