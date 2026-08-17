using TrainingDeskCalendar.App.Services;
using Xunit;

namespace TrainingDeskCalendar.App.Tests.Services;

public sealed class AsyncOnceTests
{
    [Fact]
    public async Task Run_ReturnsOneSharedTaskAndWaitsForCompletion()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var once = new AsyncOnce();
        int calls = 0;

        Task first = once.Run(async () =>
        {
            calls++;
            await gate.Task;
        });
        Task second = once.Run(() => throw new InvalidOperationException("must not run"));

        Assert.Same(first, second);
        Assert.False(second.IsCompleted);
        Assert.Equal(1, calls);

        gate.SetResult();
        await Task.WhenAll(first, second);
    }

    [Fact]
    public async Task Run_AllowsRetryAfterTheSharedTaskFails()
    {
        var once = new AsyncOnce();
        int calls = 0;

        await Assert.ThrowsAsync<IOException>(() => once.Run(() =>
        {
            calls++;
            return Task.FromException(new IOException("transient failure"));
        }));
        await once.Run(() =>
        {
            calls++;
            return Task.CompletedTask;
        });

        Assert.Equal(2, calls);
    }
}
