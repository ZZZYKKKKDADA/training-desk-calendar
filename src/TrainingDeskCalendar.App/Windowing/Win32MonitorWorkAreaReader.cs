using System.ComponentModel;
using System.Runtime.InteropServices;

namespace TrainingDeskCalendar.App.Windowing;

internal sealed class Win32MonitorWorkAreaReader : IMonitorWorkAreaReader
{
    private const uint MonitorDefaultToNearest = 0x00000002;
    private const uint MonitorInfoPrimary = 0x00000001;
    private const int EffectiveDpi = 0;

    public IReadOnlyList<MonitorWorkArea> GetAll()
    {
        var monitors = new List<MonitorWorkArea>();
        bool succeeded = EnumDisplayMonitors(
            nint.Zero,
            nint.Zero,
            (monitorHandle, _, _, _) =>
            {
                monitors.Add(ReadMonitor(monitorHandle));
                return true;
            },
            nint.Zero);

        if (!succeeded)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return monitors;
    }

    public string GetMonitorIdForWindow(nint windowHandle)
    {
        nint monitorHandle = MonitorFromWindow(windowHandle, MonitorDefaultToNearest);
        return ReadMonitor(monitorHandle).Id;
    }

    private static MonitorWorkArea ReadMonitor(nint monitorHandle)
    {
        var info = new MonitorInfoEx
        {
            Size = Marshal.SizeOf<MonitorInfoEx>(),
            DeviceName = string.Empty
        };

        if (!GetMonitorInfo(monitorHandle, ref info))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        uint dpiX = 96;
        uint dpiY = 96;
        _ = GetDpiForMonitor(monitorHandle, EffectiveDpi, out dpiX, out dpiY);
        double scale = 96d / Math.Max(dpiX, 1);

        return new MonitorWorkArea(
            info.DeviceName,
            info.WorkArea.Left * scale,
            info.WorkArea.Top * scale,
            info.WorkArea.Right * scale - info.WorkArea.Left * scale,
            info.WorkArea.Bottom * scale - info.WorkArea.Top * scale,
            (info.Flags & MonitorInfoPrimary) != 0);
    }

    private delegate bool MonitorEnumCallback(
        nint monitorHandle,
        nint deviceContext,
        nint monitorRectangle,
        nint data);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        public int Size;
        public NativeRectangle MonitorArea;
        public NativeRectangle WorkArea;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(
        nint deviceContext,
        nint clipRectangle,
        MonitorEnumCallback callback,
        nint data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitorHandle, ref MonitorInfoEx monitorInfo);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint windowHandle, uint flags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(
        nint monitorHandle,
        int dpiType,
        out uint dpiX,
        out uint dpiY);
}
