using System.Threading;

namespace TrainingDeskCalendar.App.Windows;

internal sealed class AppSingleInstance : IAppSingleInstance
{
    private Mutex? mutex;
    private bool ownsMutex;

    public bool TryAcquire(string instanceKey, Action showExistingInstance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceKey);
        ArgumentNullException.ThrowIfNull(showExistingInstance);
        if (mutex is not null)
        {
            return ownsMutex;
        }

        mutex = new Mutex(false, $"Local\\{instanceKey}");
        try
        {
            ownsMutex = mutex.WaitOne(TimeSpan.Zero);
        }
        catch (AbandonedMutexException)
        {
            ownsMutex = true;
        }

        if (ownsMutex)
        {
            return true;
        }

        mutex.Dispose();
        mutex = null;
        showExistingInstance();
        return false;
    }

    public void Dispose()
    {
        if (mutex is null)
        {
            return;
        }

        if (ownsMutex)
        {
            mutex.ReleaseMutex();
        }

        mutex.Dispose();
        mutex = null;
        ownsMutex = false;
    }
}
