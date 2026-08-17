using TrainingDeskCalendar.App.Updates;

namespace TrainingDeskCalendar.App.Services;

internal enum UpdateCheckMode
{
    Automatic,
    Manual
}

internal enum UpdateCheckStatus
{
    Unavailable,
    UpToDate,
    UpdateAvailable,
    Failed
}

internal sealed record UpdateCheckResult(
    UpdateCheckStatus Status,
    ReleaseVersion? LatestVersion = null,
    Uri? ReleaseUri = null,
    string? ErrorMessage = null)
{
    public static UpdateCheckResult Unavailable { get; } = new(UpdateCheckStatus.Unavailable);
}

internal interface IUpdateCheckService
{
    Task<UpdateCheckResult> CheckAsync(
        UpdateCheckMode mode = UpdateCheckMode.Manual,
        CancellationToken cancellationToken = default);
}

internal sealed class DeferredUpdateCheckService : IUpdateCheckService
{
    public Task<UpdateCheckResult> CheckAsync(
        UpdateCheckMode mode = UpdateCheckMode.Manual,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(UpdateCheckResult.Unavailable);
}
