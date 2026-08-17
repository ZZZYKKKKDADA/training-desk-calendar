using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TrainingDeskCalendar.App.Domain;
using TrainingDeskCalendar.App.Services;

namespace TrainingDeskCalendar.App.Calendar;

internal sealed class CalendarViewModel : INotifyPropertyChanged
{
    private readonly TrainingPlanService planService;
    private readonly PlanAutosaveCoordinator autosave;
    private readonly CalendarRangeService rangeService;
    private readonly DateOnly today;
    private readonly TimeProvider timeProvider;
    private DayCardViewModel? editingCard;
    private TwoWeekRange range;

    public CalendarViewModel(
        TrainingPlanService planService,
        PlanAutosaveCoordinator autosave,
        CalendarRangeService rangeService,
        DateOnly today,
        TimeProvider? timeProvider = null)
    {
        this.planService = planService ?? throw new ArgumentNullException(nameof(planService));
        this.autosave = autosave ?? throw new ArgumentNullException(nameof(autosave));
        this.rangeService = rangeService ?? throw new ArgumentNullException(nameof(rangeService));
        this.today = today;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        range = rangeService.Containing(today);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public TwoWeekRange Range
    {
        get => range;
        private set
        {
            range = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RangeLabel));
        }
    }

    public string RangeLabel =>
        $"{Range.Start:yyyy年M月d日} - {Range.End:yyyy年M月d日}";

    public ObservableCollection<DayCardViewModel> Days { get; } = [];

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<TrainingPlan> plans = await planService.GetRangeAsync(
            Range.Start,
            Range.End,
            cancellationToken);
        Dictionary<DateOnly, TrainingPlan> byDate = plans.ToDictionary(plan => plan.Date);
        Days.Clear();
        foreach (DateOnly date in Range.Days)
        {
            Days.Add(new DayCardViewModel(
                date,
                byDate.GetValueOrDefault(date),
                planService,
                autosave,
                timeProvider,
                date == today));
        }

        editingCard = null;
        OnPropertyChanged(nameof(Days));
    }

    public Task PreviousAsync(CancellationToken cancellationToken = default) => MoveAsync(-1, cancellationToken);
    public Task NextAsync(CancellationToken cancellationToken = default) => MoveAsync(1, cancellationToken);

    public async Task GoToTodayAsync(CancellationToken cancellationToken = default)
    {
        await FlushAsync(cancellationToken);
        Range = rangeService.Containing(today);
        await LoadAsync(cancellationToken);
    }

    public async Task MoveAsync(int pages, CancellationToken cancellationToken = default)
    {
        await FlushAsync(cancellationToken);
        Range = rangeService.Move(Range, pages);
        await LoadAsync(cancellationToken);
    }

    public void BeginEdit(DayCardViewModel card)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (!Days.Contains(card))
        {
            throw new ArgumentException("The card is not in the current range.", nameof(card));
        }

        if (editingCard is not null && !ReferenceEquals(editingCard, card))
        {
            editingCard.CollapseAndSave();
        }

        editingCard = card;
        card.BeginEdit();
    }

    public async Task SaveEditAsync(
        DayCardViewModel card,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(card);
        await card.SaveEditAsync();
        editingCard = null;
        await autosave.FlushAsync(cancellationToken);
    }

    public void CancelEdit(DayCardViewModel card)
    {
        ArgumentNullException.ThrowIfNull(card);
        card.CancelEdit();
        if (ReferenceEquals(editingCard, card))
        {
            editingCard = null;
        }
    }

    public Task SetCompletedAsync(
        DayCardViewModel card,
        bool completed,
        CancellationToken cancellationToken = default) =>
        card.SetCompletedAsync(completed);

    public Task FlushAsync(CancellationToken cancellationToken = default) =>
        autosave.FlushAsync(cancellationToken);

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
