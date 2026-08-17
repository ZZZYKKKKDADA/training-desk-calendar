using System.Windows;
using Microsoft.Win32;
using TrainingDeskCalendar.App.Persistence;

namespace TrainingDeskCalendar.App.Settings;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel viewModel;

    internal SettingsWindow(SettingsViewModel viewModel)
    {
        this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = viewModel;
        ThemeCombo.SelectedIndex = (int)viewModel.Theme;
    }

    private void OnThemeChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ThemeCombo.SelectedIndex is 0 or 1)
        {
            viewModel.Theme = (AppTheme)ThemeCombo.SelectedIndex;
        }
    }

    private async void OnStartupClick(object sender, RoutedEventArgs e)
    {
        var checkBox = (System.Windows.Controls.CheckBox)sender;
        try
        {
            await viewModel.SetStartWithWindowsAsync(checkBox.IsChecked == true);
        }
        catch (Exception exception)
        {
            checkBox.IsChecked = viewModel.StartWithWindows;
            System.Windows.MessageBox.Show(this, exception.Message, "训练桌历", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnResetWindowClick(object sender, RoutedEventArgs e) => viewModel.ResetWindow();

    private async void OnExportClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "训练桌历数据 (*.json)|*.json|所有文件 (*.*)|*.*",
            FileName = "training-desk-calendar.json"
        };
        if (dialog.ShowDialog(this) != true) return;
        await InvokeDataActionAsync(() => viewModel.ExportAsync(dialog.FileName));
    }

    private async void OnImportClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "训练桌历数据 (*.json)|*.json|所有文件 (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != true) return;
        await InvokeDataActionAsync(() => viewModel.ImportAsync(dialog.FileName));
    }

    private async void OnUpdateClick(object sender, RoutedEventArgs e) =>
        await InvokeDataActionAsync(viewModel.CheckUpdatesAsync);

    private async void OnApplyClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await viewModel.ApplyAsync();
            DialogResult = true;
            Close();
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show(this, exception.Message, "训练桌历", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();

    private async Task InvokeDataActionAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show(this, exception.Message, "训练桌历", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
