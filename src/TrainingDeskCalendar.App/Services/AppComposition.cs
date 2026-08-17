using TrainingDeskCalendar.App.Calendar;
using TrainingDeskCalendar.App.Domain;
using TrainingDeskCalendar.App.Persistence;

namespace TrainingDeskCalendar.App.Services;

internal sealed class AppComposition : IAsyncDisposable
{
    private bool disposed;

    private AppComposition(
        AppDataPaths paths,
        SqlitePlanStore planStore,
        SettingsStore settingsStore,
        AppSettings settings,
        TrainingPlanService planService,
        PlanAutosaveCoordinator autosave,
        CalendarViewModel calendar)
    {
        Paths = paths;
        PlanStore = planStore;
        SettingsStore = settingsStore;
        Settings = settings;
        PlanService = planService;
        Autosave = autosave;
        Calendar = calendar;
    }

    public AppDataPaths Paths { get; }
    public SqlitePlanStore PlanStore { get; }
    public SettingsStore SettingsStore { get; }
    public AppSettings Settings { get; }
    public TrainingPlanService PlanService { get; }
    public PlanAutosaveCoordinator Autosave { get; }
    public CalendarViewModel Calendar { get; }

    public static async Task<AppComposition> CreateAsync(
        AppDataPaths paths,
        DateOnly today,
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        TimeProvider clock = timeProvider ?? TimeProvider.System;
        var planStore = new SqlitePlanStore(paths.DatabasePath);
        await planStore.InitializeAsync(cancellationToken);
        var settingsStore = new SettingsStore(paths.SettingsPath, timeProvider: clock);
        AppSettings settings = await settingsStore.LoadAsync(cancellationToken);
        var planService = new TrainingPlanService(planStore, clock);
        var autosave = new PlanAutosaveCoordinator(planService);
        var calendar = new CalendarViewModel(
            planService,
            autosave,
            new CalendarRangeService(),
            today,
            clock);

        return new AppComposition(
            paths,
            planStore,
            settingsStore,
            settings,
            planService,
            autosave,
            calendar);
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await Calendar.FlushAsync();
        await Autosave.DisposeAsync();
    }
}
