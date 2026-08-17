using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using TrainingDeskCalendar.App.Domain;
using TrainingDeskCalendar.App.Services;

namespace TrainingDeskCalendar.App.Calendar;

internal sealed class DayCardViewModel : INotifyPropertyChanged
{
    private readonly TrainingPlanService planService;
    private readonly PlanAutosaveCoordinator autosave;
    private readonly TimeProvider timeProvider;
    private string text;
    private TaskColorId selectedColor;
    private bool isCompleted;
    private bool isEditing;
    private bool isDirty;
    private bool hasPlan;
    private string? saveError;
    private string editText = string.Empty;
    private TaskColorId editColor;
    private bool editCompleted;
    private Task? lastSaveTask;

    public DayCardViewModel(
        DateOnly date,
        TrainingPlan? plan,
        TrainingPlanService planService,
        PlanAutosaveCoordinator autosave,
        TimeProvider? timeProvider = null,
        bool isToday = false)
    {
        Date = date;
        this.planService = planService ?? throw new ArgumentNullException(nameof(planService));
        this.autosave = autosave ?? throw new ArgumentNullException(nameof(autosave));
        this.timeProvider = timeProvider ?? TimeProvider.System;
        text = plan?.Text ?? string.Empty;
        selectedColor = plan?.Color ?? TaskColorId.Gray;
        isCompleted = plan?.IsCompleted ?? false;
        hasPlan = plan is not null;
        IsToday = isToday;
        editColor = selectedColor;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public DateOnly Date { get; }
    public string Weekday => Date.DayOfWeek switch
    {
        DayOfWeek.Monday => "周一",
        DayOfWeek.Tuesday => "周二",
        DayOfWeek.Wednesday => "周三",
        DayOfWeek.Thursday => "周四",
        DayOfWeek.Friday => "周五",
        DayOfWeek.Saturday => "周六",
        _ => "周日"
    };
    public int DayNumber => Date.Day;
    public bool IsToday { get; }
    public bool HasPlan => hasPlan;
    public string DisplayText => string.IsNullOrWhiteSpace(Text) ? "暂无计划" : Text;

    public string Text
    {
        get => text;
        set
        {
            value ??= string.Empty;
            if (text == value)
            {
                return;
            }

            text = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayText));
            if (IsEditing)
            {
                QueueDraft();
            }
        }
    }

    public TaskColorId SelectedColor
    {
        get => selectedColor;
        private set
        {
            if (selectedColor == value)
            {
                return;
            }

            selectedColor = value;
            OnPropertyChanged();
        }
    }

    public bool IsCompleted
    {
        get => isCompleted;
        private set
        {
            if (isCompleted == value)
            {
                return;
            }

            isCompleted = value;
            OnPropertyChanged();
        }
    }

    public bool IsEditing
    {
        get => isEditing;
        private set
        {
            if (isEditing == value)
            {
                return;
            }

            isEditing = value;
            OnPropertyChanged();
        }
    }

    public bool IsDirty
    {
        get => isDirty;
        private set
        {
            if (isDirty == value)
            {
                return;
            }

            isDirty = value;
            OnPropertyChanged();
        }
    }

    public string? SaveError
    {
        get => saveError;
        private set
        {
            if (saveError == value)
            {
                return;
            }

            saveError = value;
            OnPropertyChanged();
        }
    }

    public void BeginEdit()
    {
        if (IsEditing)
        {
            return;
        }

        editText = Text;
        editColor = SelectedColor;
        editCompleted = IsCompleted;
        IsEditing = true;
        SaveError = null;
    }

    public void SelectColor(TaskColorId color)
    {
        if (!Enum.IsDefined(color))
        {
            throw new ArgumentOutOfRangeException(nameof(color));
        }

        SelectedColor = color;
        if (IsEditing)
        {
            QueueDraft();
        }
    }

    public async Task SetCompletedAsync(bool completed)
    {
        if (IsEditing)
        {
            IsCompleted = completed;
            QueueDraft();
            await autosave.FlushAsync();
            IsDirty = false;
            hasPlan = !CreatePlan().IsDefaultEmpty;
            OnPropertyChanged(nameof(HasPlan));
            return;
        }

        await planService.SetCompletedAsync(Date, completed);
        IsCompleted = completed;
        hasPlan = completed || !string.IsNullOrWhiteSpace(Text);
        OnPropertyChanged(nameof(HasPlan));
    }

    public async Task SaveEditAsync()
    {
        if (!IsEditing)
        {
            return;
        }

        if (!IsDirty)
        {
            QueueDraft();
        }

        await autosave.FlushAsync();
        IsDirty = false;
        hasPlan = !CreatePlan().IsDefaultEmpty;
        OnPropertyChanged(nameof(HasPlan));
        IsEditing = false;
        SaveError = null;
    }

    public void CancelEdit()
    {
        if (!IsEditing)
        {
            return;
        }

        text = editText;
        selectedColor = editColor;
        isCompleted = editCompleted;
        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(DisplayText));
        OnPropertyChanged(nameof(SelectedColor));
        OnPropertyChanged(nameof(IsCompleted));
        QueueDraft();
        IsDirty = false;
        IsEditing = false;
        SaveError = null;
    }

    internal void CollapseAndSave()
    {
        if (!IsEditing)
        {
            return;
        }

        if (!IsDirty)
        {
            QueueDraft();
        }

        IsEditing = false;
        _ = ObserveSaveAsync(lastSaveTask);
    }

    private void QueueDraft()
    {
        IsDirty = true;
        SaveError = null;
        lastSaveTask = autosave.QueueAsync(CreatePlan());
        _ = ObserveSaveAsync(lastSaveTask);
    }

    private async Task ObserveSaveAsync(Task? saveTask)
    {
        if (saveTask is null)
        {
            return;
        }

        try
        {
            await saveTask;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            SaveError = exception.Message;
        }
    }

    private TrainingPlan CreatePlan() => TrainingPlan.Create(
        Date,
        Text,
        SelectedColor,
        IsCompleted,
        timeProvider.GetUtcNow());

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
