namespace TrainingDeskCalendar.App.Windows;

internal interface IAppSingleInstance : IDisposable
{
    bool TryAcquire(string instanceKey, Action showExistingInstance);
}
