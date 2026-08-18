using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace TrainingDeskCalendar.App.Desktop;

internal sealed class Win32DesktopWindowApi : IDesktopWindowApi
{
    private const uint SpawnWorkerMessage = 0x052C;
    private const uint SmtoNormal = 0x0000;
    private const int GwlStyle = -16;
    private const long WsChild = 0x40000000L;
    private const long WsPopup = unchecked((long)0x80000000L);
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private static readonly nint HwndNoTopmost = (nint)(-2);

    private readonly ConcurrentDictionary<nint, nint> originalStyles = new();

    public bool TryAttachToDesktop(nint windowHandle, out string? failureReason)
    {
        nint workerWindow = FindDesktopWorkerWindow();
        if (workerWindow == nint.Zero)
        {
            failureReason = "WorkerW was not found.";
            return false;
        }

        if (GetParent(windowHandle) == workerWindow)
        {
            failureReason = null;
            return true;
        }

        nint originalStyle = originalStyles.GetOrAdd(
            windowHandle,
            handle => GetWindowLongPtr(handle, GwlStyle));
        nint childStyle = (nint)(((long)originalStyle & ~WsPopup) | WsChild);
        _ = SetWindowLongPtr(windowHandle, GwlStyle, childStyle);

        Marshal.SetLastPInvokeError(0);
        nint previousParent = SetParent(windowHandle, workerWindow);
        int error = Marshal.GetLastPInvokeError();
        if (previousParent == nint.Zero && error != 0)
        {
            _ = SetWindowLongPtr(windowHandle, GwlStyle, originalStyle);
            originalStyles.TryRemove(windowHandle, out _);
            failureReason = new Win32Exception(error).Message;
            return false;
        }

        _ = SetWindowPos(
            windowHandle,
            nint.Zero,
            0,
            0,
            0,
            0,
            SwpNoMove | SwpNoSize | SwpNoZOrder | SwpFrameChanged);

        failureReason = null;
        return true;
    }

    public void RestoreAsTopLevel(nint windowHandle)
    {
        _ = SetParent(windowHandle, nint.Zero);

        if (originalStyles.TryRemove(windowHandle, out nint originalStyle))
        {
            _ = SetWindowLongPtr(windowHandle, GwlStyle, originalStyle);
        }

        _ = SetWindowPos(
            windowHandle,
            HwndNoTopmost,
            0,
            0,
            0,
            0,
            SwpNoMove | SwpNoSize | SwpNoActivate | SwpFrameChanged);
    }

    private static nint FindDesktopWorkerWindow()
    {
        nint programManager = FindWindow("Progman", null);
        if (programManager == nint.Zero)
        {
            return nint.Zero;
        }

        _ = SendMessageTimeout(
            programManager,
            SpawnWorkerMessage,
            (nint)0xD,
            (nint)0x1,
            SmtoNormal,
            1_000,
            out _);

        nint workerWindow = nint.Zero;
        _ = EnumWindows((topLevelWindow, _) =>
        {
            nint shellView = FindWindowEx(topLevelWindow, nint.Zero, "SHELLDLL_DefView", null);
            if (shellView == nint.Zero)
            {
                return true;
            }

            workerWindow = FindWindowEx(nint.Zero, topLevelWindow, "WorkerW", null);
            return false;
        }, nint.Zero);

        return workerWindow;
    }

    private delegate bool EnumWindowsCallback(nint windowHandle, nint parameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint FindWindow(string? className, string? windowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint FindWindowEx(
        nint parentWindow,
        nint childAfter,
        string? className,
        string? windowName);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsCallback callback, nint parameter);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SendMessageTimeout(
        nint windowHandle,
        uint message,
        nint wParam,
        nint lParam,
        uint flags,
        uint timeoutMilliseconds,
        out nint result);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetParent(nint childWindow, nint newParentWindow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint GetParent(nint windowHandle);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr(nint windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint windowHandle, int index, nint newValue);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        nint windowHandle,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);
}
