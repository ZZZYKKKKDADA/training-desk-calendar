using TrainingDeskCalendar.App.Services;

namespace TrainingDeskCalendar.App.Updates;

internal interface IUpdateNotifications
{
    void ShowInformation(string message);
    void ShowError(string message);
    bool ConfirmOpenRelease(string message);
}

internal interface IExternalUriLauncher
{
    void Open(Uri uri);
}

internal sealed class UpdateCheckCoordinator(
    IUpdateCheckService updateCheckService,
    IUpdateNotifications notifications,
    IExternalUriLauncher uriLauncher)
{
    private readonly IUpdateCheckService updateCheckService =
        updateCheckService ?? throw new ArgumentNullException(nameof(updateCheckService));
    private readonly IUpdateNotifications notifications =
        notifications ?? throw new ArgumentNullException(nameof(notifications));
    private readonly IExternalUriLauncher uriLauncher =
        uriLauncher ?? throw new ArgumentNullException(nameof(uriLauncher));

    public async Task CheckAsync(
        UpdateCheckMode mode,
        CancellationToken cancellationToken = default)
    {
        UpdateCheckResult result = await updateCheckService.CheckAsync(mode, cancellationToken);
        switch (result.Status)
        {
            case UpdateCheckStatus.Unavailable when mode == UpdateCheckMode.Manual:
                notifications.ShowInformation("当前构建未配置 GitHub 仓库，无法检查更新。");
                break;
            case UpdateCheckStatus.UpToDate when mode == UpdateCheckMode.Manual:
                notifications.ShowInformation("当前已是最新版本。");
                break;
            case UpdateCheckStatus.UpdateAvailable:
                PresentAvailableUpdate(result, mode);
                break;
            case UpdateCheckStatus.Failed when
                mode == UpdateCheckMode.Manual && result.ErrorMessage is not null:
                notifications.ShowError(result.ErrorMessage);
                break;
        }
    }

    private void PresentAvailableUpdate(UpdateCheckResult result, UpdateCheckMode mode)
    {
        if (result.LatestVersion is not ReleaseVersion version || result.ReleaseUri is not Uri releaseUri)
        {
            if (mode == UpdateCheckMode.Manual)
            {
                notifications.ShowError("更新信息不完整，请稍后重试。");
            }
            return;
        }

        if (notifications.ConfirmOpenRelease(
            $"发现新版本 {version}。是否打开 GitHub Release 下载页面？"))
        {
            uriLauncher.Open(releaseUri);
        }
    }
}
