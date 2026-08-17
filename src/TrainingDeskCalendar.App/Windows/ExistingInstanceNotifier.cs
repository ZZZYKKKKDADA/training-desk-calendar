using System.Runtime.InteropServices;

namespace TrainingDeskCalendar.App.Windows;

internal static class ExistingInstanceNotifier
{
    private const int SwRestore = 9;

    public static void Show()
    {
        nint handle = FindWindow(null, "训练桌历");
        if (handle == nint.Zero)
        {
            return;
        }

        _ = ShowWindow(handle, SwRestore);
        _ = SetForegroundWindow(handle);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint FindWindow(string? className, string? windowName);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint windowHandle, int command);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint windowHandle);
}
