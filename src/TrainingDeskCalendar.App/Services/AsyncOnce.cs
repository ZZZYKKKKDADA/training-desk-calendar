namespace TrainingDeskCalendar.App.Services;

internal sealed class AsyncOnce
{
    private readonly object sync = new();
    private Task? task;

    public Task Run(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        lock (sync)
        {
            if (task is not null)
            {
                return task;
            }

            Task created = action();
            task = created;
            _ = created.ContinueWith(
                completed =>
                {
                    if (!completed.IsFaulted && !completed.IsCanceled)
                    {
                        return;
                    }

                    lock (sync)
                    {
                        if (ReferenceEquals(task, completed))
                        {
                            task = null;
                        }
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return created;
        }
    }
}
