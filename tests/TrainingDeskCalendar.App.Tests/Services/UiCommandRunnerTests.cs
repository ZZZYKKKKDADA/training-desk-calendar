using TrainingDeskCalendar.App.Services;
using Xunit;

namespace TrainingDeskCalendar.App.Tests.Services;

public sealed class UiCommandRunnerTests
{
    [Fact]
    public async Task RunAsync_ReportsFailureWithoutRethrowingIntoTheUiEventLoop()
    {
        Exception? reported = null;

        await UiCommandRunner.RunAsync(
            () => Task.FromException(new IOException("settings unavailable")),
            exception => reported = exception);

        Assert.IsType<IOException>(reported);
        Assert.Equal("settings unavailable", reported.Message);
    }
}
