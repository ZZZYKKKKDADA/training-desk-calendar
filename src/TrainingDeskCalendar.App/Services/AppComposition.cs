using System.Net.Http;
using TrainingDeskCalendar.App.Calendar;
using TrainingDeskCalendar.App.Domain;
using TrainingDeskCalendar.App.Persistence;
using TrainingDeskCalendar.App.Updates;
using TrainingDeskCalendar.App.Windows;

namespace TrainingDeskCalendar.App.Services;

internal sealed class AppComposition : IAsyncDisposable
{
    private readonly AsyncOnce disposeOnce = new();
    private readonly HttpClient? ownedUpdateHttpClient;

    private AppComposition(
        AppDataPaths paths,
        SqlitePlanStore planStore,
        SettingsStore settingsStore,
        AppSettings settings,
        TrainingPlanService planService,
        PlanAutosaveCoordinator autosave,
        CalendarViewModel calendar,
        DataTransferService transferService,
        IStartupRegistration startupRegistration,
        IUpdateCheckService updateCheckService,
        UpdateCheckCoordinator updateCheckCoordinator,
        ReleaseBuildMetadata buildMetadata,
        IExternalUriLauncher uriLauncher,
        HttpClient? ownedUpdateHttpClient)
    {
        Paths = paths;
        PlanStore = planStore;
        SettingsStore = settingsStore;
        Settings = settings;
        PlanService = planService;
        Autosave = autosave;
        Calendar = calendar;
        TransferService = transferService;
        StartupRegistration = startupRegistration;
        UpdateCheckService = updateCheckService;
        UpdateCheckCoordinator = updateCheckCoordinator;
        BuildMetadata = buildMetadata;
        UriLauncher = uriLauncher;
        this.ownedUpdateHttpClient = ownedUpdateHttpClient;
    }

    public AppDataPaths Paths { get; }
    public SqlitePlanStore PlanStore { get; }
    public SettingsStore SettingsStore { get; }
    public AppSettings Settings { get; private set; }
    public TrainingPlanService PlanService { get; }
    public PlanAutosaveCoordinator Autosave { get; }
    public CalendarViewModel Calendar { get; }
    public DataTransferService TransferService { get; }
    public IStartupRegistration StartupRegistration { get; }
    public IUpdateCheckService UpdateCheckService { get; }
    public UpdateCheckCoordinator UpdateCheckCoordinator { get; }
    public ReleaseBuildMetadata BuildMetadata { get; }
    public IExternalUriLauncher UriLauncher { get; }

    public async Task SaveSettingsAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        settings.Validate();
        await SettingsStore.SaveAsync(settings, cancellationToken);
        Settings = settings;
    }

    public async Task SetStartWithWindowsAsync(bool enabled)
    {
        var coordinator = new StartupSettingsCoordinator(StartupRegistration);
        Settings = await coordinator.SetEnabledAsync(
            Settings,
            enabled,
            settings => SettingsStore.SaveAsync(settings));
    }

    public async Task ImportAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        await Calendar.FlushAsync(cancellationToken);
        await TransferService.ImportAsync(path, cancellationToken);
        Settings = await SettingsStore.LoadAsync(cancellationToken);

        Exception? startupError = null;
        try
        {
            StartupRegistration.SetEnabled(Settings.StartWithWindows);
        }
        catch (Exception exception)
        {
            startupError = exception;
        }

        await Calendar.LoadAsync(cancellationToken);
        if (startupError is not null)
        {
            throw new InvalidOperationException(
                "数据已导入，但开机自启动状态未能同步。",
                startupError);
        }
    }

    public static async Task<AppComposition> CreateAsync(
        AppDataPaths paths,
        DateOnly today,
        TimeProvider? timeProvider = null,
        IStartupRegistration? startupRegistration = null,
        ReleaseBuildMetadata? buildMetadata = null,
        HttpClient? updateHttpClient = null,
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
        var transferService = new DataTransferService(planStore, settingsStore, paths, clock);
        IStartupRegistration resolvedStartupRegistration = startupRegistration ??
            (Environment.ProcessPath is string processPath
                ? new StartupRegistration(processPath)
                : new DisabledStartupRegistration());
        ReleaseBuildMetadata resolvedBuildMetadata = buildMetadata ??
            ReleaseBuildMetadata.FromAssembly(typeof(AppComposition).Assembly);
        HttpClient? ownedUpdateHttpClient = updateHttpClient is null
            ? new HttpClient { Timeout = TimeSpan.FromSeconds(10) }
            : null;
        HttpClient resolvedUpdateHttpClient = updateHttpClient ?? ownedUpdateHttpClient!;
        AppComposition? composition = null;
        var updateCheckService = new GitHubReleaseUpdateCheckService(
            resolvedUpdateHttpClient,
            resolvedBuildMetadata.Repository,
            resolvedBuildMetadata.Version,
            clock,
            settings.LastUpdateCheckUtc,
            async (checkedAt, token) =>
            {
                if (composition is null)
                {
                    throw new InvalidOperationException("Application composition is not initialized.");
                }
                await composition.SaveSettingsAsync(
                    composition.Settings with { LastUpdateCheckUtc = checkedAt },
                    token);
            });
        IExternalUriLauncher uriLauncher = new ExternalUriLauncher();
        var updateCheckCoordinator = new UpdateCheckCoordinator(
            updateCheckService,
            new WpfUpdateNotifications(),
            uriLauncher);
        var calendar = new CalendarViewModel(
            planService,
            autosave,
            new CalendarRangeService(),
            today,
            clock);

        composition = new AppComposition(
            paths,
            planStore,
            settingsStore,
            settings,
            planService,
            autosave,
            calendar,
            transferService,
            resolvedStartupRegistration,
            updateCheckService,
            updateCheckCoordinator,
            resolvedBuildMetadata,
            uriLauncher,
            ownedUpdateHttpClient);
        return composition;
    }

    public ValueTask DisposeAsync() => new(disposeOnce.Run(DisposeCoreAsync));

    private async Task DisposeCoreAsync()
    {
        await Calendar.FlushAsync();
        await Autosave.DisposeAsync();
        ownedUpdateHttpClient?.Dispose();
    }
}

internal sealed class DisabledStartupRegistration : IStartupRegistration
{
    public bool IsEnabled => false;
    public void SetEnabled(bool enabled) { }
}
