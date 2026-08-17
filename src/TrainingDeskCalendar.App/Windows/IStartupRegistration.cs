namespace TrainingDeskCalendar.App.Windows;

internal interface IStartupRegistration
{
    bool IsEnabled { get; }
    void SetEnabled(bool enabled);
}

internal interface IUserStartupStore
{
    string? GetValue(string path, string name);
    void SetValue(string path, string name, string value);
    void DeleteValue(string path, string name);
}
