namespace TrainingDeskCalendar.App.Services;

internal interface IUpdateCheckService
{
    Task CheckAsync(CancellationToken cancellationToken = default);
}

internal sealed class DeferredUpdateCheckService : IUpdateCheckService
{
    public Task CheckAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
