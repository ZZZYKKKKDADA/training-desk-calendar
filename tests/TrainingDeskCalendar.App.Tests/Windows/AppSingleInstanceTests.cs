using TrainingDeskCalendar.App.Windows;
using Xunit;

namespace TrainingDeskCalendar.App.Tests.Windows;

public sealed class AppSingleInstanceTests
{
    [Fact]
    public void SecondInstance_NotifiesTheFirstAndDoesNotAcquireTheLock()
    {
        string key = $"TrainingDeskCalendar.Tests.{Guid.NewGuid():N}";
        using var first = new AppSingleInstance();
        using var second = new AppSingleInstance();
        bool notified = false;

        Assert.True(first.TryAcquire(key, () => notified = true));
        bool acquired = true;
        var thread = new Thread(() =>
        {
            acquired = second.TryAcquire(key, () => notified = true);
        });
        thread.Start();
        thread.Join();

        Assert.False(acquired);
        Assert.True(notified);
    }

    [Fact]
    public void DisposedInstance_AllowsALaterInstanceToAcquireTheLock()
    {
        string key = $"TrainingDeskCalendar.Tests.{Guid.NewGuid():N}";
        using (var first = new AppSingleInstance())
        {
            Assert.True(first.TryAcquire(key, static () => { }));
        }

        using var second = new AppSingleInstance();
        Assert.True(second.TryAcquire(key, static () => { }));
    }
}
