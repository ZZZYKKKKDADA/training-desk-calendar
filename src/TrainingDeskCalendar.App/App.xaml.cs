using System.Windows;

namespace TrainingDeskCalendar.App;

public partial class App : Application
{
    internal static string? ReadyFilePath { get; private set; }
    internal static TimeSpan? ExitAfter { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
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

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }
}
