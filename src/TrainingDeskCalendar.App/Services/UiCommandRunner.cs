namespace TrainingDeskCalendar.App.Services;

internal static class UiCommandRunner
{
    public static async Task RunAsync(
        Func<Task> action,
        Action<Exception> reportFailure)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(reportFailure);

        try
        {
            await action();
        }
        catch (Exception exception)
        {
            reportFailure(exception);
        }
    }
}
