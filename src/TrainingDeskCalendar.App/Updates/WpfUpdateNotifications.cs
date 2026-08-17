using System.Windows;

namespace TrainingDeskCalendar.App.Updates;

internal sealed class WpfUpdateNotifications : IUpdateNotifications
{
    public void ShowInformation(string message) => MessageBox.Show(
        Application.Current?.MainWindow,
        message,
        "训练桌历",
        MessageBoxButton.OK,
        MessageBoxImage.Information);

    public void ShowError(string message) => MessageBox.Show(
        Application.Current?.MainWindow,
        message,
        "训练桌历",
        MessageBoxButton.OK,
        MessageBoxImage.Warning);

    public bool ConfirmOpenRelease(string message) => MessageBox.Show(
        Application.Current?.MainWindow,
        message,
        "训练桌历",
        MessageBoxButton.YesNo,
        MessageBoxImage.Information) == MessageBoxResult.Yes;
}
