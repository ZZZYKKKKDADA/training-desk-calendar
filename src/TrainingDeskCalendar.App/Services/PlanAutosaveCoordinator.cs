using TrainingDeskCalendar.App.Domain;

namespace TrainingDeskCalendar.App.Services;

internal delegate Task AutosaveDelay(
    TimeSpan delay,
    CancellationToken cancellationToken);

internal sealed class PlanAutosaveCoordinator : IAsyncDisposable
{
    private static readonly TimeSpan DefaultDebounce = TimeSpan.FromMilliseconds(250);

    private readonly TrainingPlanService planService;
    private readonly AutosaveDelay delay;
    private readonly TimeSpan debounce;
    private readonly Lock syncRoot = new();
    private readonly SemaphoreSlim saveGate = new(1, 1);
    private readonly Dictionary<DateOnly, PendingSave> pendingSaves = [];
    private bool isDisposing;
    private bool isDisposed;

    public PlanAutosaveCoordinator(
        TrainingPlanService planService,
        AutosaveDelay? delay = null,
        TimeSpan? debounce = null)
    {
        this.planService = planService ?? throw new ArgumentNullException(nameof(planService));
        this.delay = delay ?? Task.Delay;
        this.debounce = debounce ?? DefaultDebounce;

        if (this.debounce < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(debounce));
        }
    }

    public Task QueueAsync(TrainingPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        PendingSave pendingSave;
        lock (syncRoot)
        {
            ThrowIfUnavailable();

            pendingSaves.Remove(plan.Date, out PendingSave? replaced);
            replaced?.CancelDelay();

            pendingSave = new PendingSave(plan, replaced);
            pendingSaves.Add(plan.Date, pendingSave);
        }

        _ = RunDebounceAsync(pendingSave);
        return pendingSave.Completion.Task;
    }

    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        lock (syncRoot)
        {
            ThrowIfUnavailable();
        }

        return FlushCoreAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        lock (syncRoot)
        {
            if (isDisposed)
            {
                return;
            }

            if (isDisposing)
            {
                throw new InvalidOperationException("The autosave coordinator is already being disposed.");
            }

            isDisposing = true;
        }

        try
        {
            await FlushCoreAsync(CancellationToken.None);

            lock (syncRoot)
            {
                isDisposed = true;
            }
        }
        finally
        {
            lock (syncRoot)
            {
                isDisposing = false;
            }
        }
    }

    private async Task RunDebounceAsync(PendingSave pendingSave)
    {
        try
        {
            await delay(debounce, pendingSave.Cancellation.Token);
            await SaveIfCurrentAsync(pendingSave, CancellationToken.None);
        }
        catch (OperationCanceledException) when (pendingSave.Cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            lock (syncRoot)
            {
                if (IsCurrent(pendingSave))
                {
                    pendingSave.Completion.TrySetException(exception);
                }
            }
        }
    }

    private async Task FlushCoreAsync(CancellationToken cancellationToken)
    {
        await saveGate.WaitAsync(cancellationToken);
        try
        {
            PendingSave[] saves;
            lock (syncRoot)
            {
                saves = pendingSaves.Values
                    .OrderBy(save => save.Plan.Date)
                    .ToArray();

                foreach (PendingSave save in saves)
                {
                    save.CancelDelay();
                }
            }

            foreach (PendingSave save in saves)
            {
                cancellationToken.ThrowIfCancellationRequested();

                lock (syncRoot)
                {
                    if (!IsCurrent(save))
                    {
                        continue;
                    }
                }

                try
                {
                    await planService.SaveAsync(save.Plan, cancellationToken);
                }
                catch (Exception exception)
                {
                    save.Completion.TrySetException(exception);
                    throw;
                }

                CompleteIfCurrent(save);
            }
        }
        finally
        {
            saveGate.Release();
        }
    }

    private async Task SaveIfCurrentAsync(
        PendingSave pendingSave,
        CancellationToken cancellationToken)
    {
        await saveGate.WaitAsync(cancellationToken);
        try
        {
            lock (syncRoot)
            {
                if (!IsCurrent(pendingSave))
                {
                    return;
                }
            }

            await planService.SaveAsync(pendingSave.Plan, cancellationToken);
            CompleteIfCurrent(pendingSave);
        }
        finally
        {
            saveGate.Release();
        }
    }

    private void CompleteIfCurrent(PendingSave pendingSave)
    {
        lock (syncRoot)
        {
            if (!IsCurrent(pendingSave))
            {
                return;
            }

            pendingSaves.Remove(pendingSave.Plan.Date);
            pendingSave.Completion.TrySetResult();
            pendingSave.CancelSupersededCompletions();
        }
    }

    private bool IsCurrent(PendingSave pendingSave) =>
        pendingSaves.TryGetValue(pendingSave.Plan.Date, out PendingSave? current) &&
        ReferenceEquals(current, pendingSave);

    private void ThrowIfUnavailable()
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        if (isDisposing)
        {
            throw new InvalidOperationException("The autosave coordinator is being disposed.");
        }
    }

    private sealed class PendingSave(TrainingPlan plan, PendingSave? superseded)
    {
        public TrainingPlan Plan { get; } = plan;
        public CancellationTokenSource Cancellation { get; } = new();
        public TaskCompletionSource Completion { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public void CancelDelay() => Cancellation.Cancel();

        public void CancelSupersededCompletions()
        {
            if (superseded is null)
            {
                return;
            }

            superseded.Cancellation.Cancel();
            superseded.Completion.TrySetCanceled(superseded.Cancellation.Token);
            superseded.CancelSupersededCompletions();
        }
    }
}
