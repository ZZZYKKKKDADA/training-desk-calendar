using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TrainingDeskCalendar.Launcher;

internal static partial class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            string launcherPath = Environment.ProcessPath ??
                throw new InvalidOperationException("The launcher path is unavailable.");
            LaunchLayout layout = LaunchLayout.FromBaseDirectory(AppContext.BaseDirectory);
            layout.Validate();
            LaunchCommand command = LaunchCommand.Create(layout, launcherPath, args);
            return Process.Start(command.ToProcessStartInfo()) is null ? 2 : 0;
        }
        catch
        {
            _ = MessageBox(
                nint.Zero,
                "训练桌历安装文件不完整，请重新安装。",
                "训练桌历",
                0x00000010);
            return 1;
        }
    }

    [LibraryImport("user32.dll", EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MessageBox(
        nint window,
        string text,
        string caption,
        uint type);
}
