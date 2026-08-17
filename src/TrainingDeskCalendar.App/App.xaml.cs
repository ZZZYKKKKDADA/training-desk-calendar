using System.Windows;
using TrainingDeskCalendar.App.Persistence;
using TrainingDeskCalendar.App.Services;

namespace TrainingDeskCalendar.App;

public partial class App : Application
{
    internal static string? ReadyFilePath { get; private set; }
    internal static TimeSpan? ExitAfter { get; private set; }
    private AppComposition? composition;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        for (int index = 0; index < e.Args.Length; index++)
        {
            if (e.Args[index] == "--ready-file" && index + 1 < e.Args.Length)
            {
                ReadyFilePath = e.Args[++index];
            }
            else if (e.Args[index] == "--exit-after-seconds" &&
                     index + 1 < e.Args.Length &&
                     int.TryParse(e.Args[++index], out int seconds))
            {
                ExitAfter = TimeSpan.FromSeconds(seconds);
            }
        }

        try
        {
            composition = await AppComposition.CreateAsync(
                AppDataPaths.ForCurrentUser(),
                DateOnly.FromDateTime(DateTime.Today));
            var window = new MainWindow(composition);
            MainWindow = window;
            window.Show();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"训练桌历无法启动：{exception.Message}",
                "训练桌历",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (composition is not null)
        {
            await composition.DisposeAsync();
        }

        base.OnExit(e);
    }
}
