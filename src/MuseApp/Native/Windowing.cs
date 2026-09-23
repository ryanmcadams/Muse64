using System.Runtime.InteropServices;
using MuseApp.Core;

namespace MuseApp.Native;

/// <summary>Window geometry and activation in physical pixels.</summary>
internal static partial class Windowing
{
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const int AsfwAny = -1;

    public static PixelRect? GetBounds(nint hwnd) =>
        hwnd != 0 && GetWindowRect(hwnd, out var rect) != 0 ? rect.ToPixelRect() : null;

    public static void SetBounds(nint hwnd, PixelRect bounds) =>
        _ = SetWindowPos(hwnd, 0, bounds.Left, bounds.Top, bounds.Width, bounds.Height, SwpNoZOrder | SwpNoActivate);

    /// <summary>
    /// True if the window really is the user's foreground window. WPF's IsActive can be
    /// true for a window that was activated in its own thread but never got the foreground.
    /// </summary>
    public static bool IsForeground(nint hwnd) => hwnd != 0 && GetForegroundWindow() == hwnd;

    /// <summary>
    /// Called by a second instance (which owns the foreground) so the first instance is
    /// allowed to bring its window to the front instead of just flashing the taskbar.
    /// </summary>
    public static void AllowAnyProcessToSetForeground() => _ = AllowSetForegroundWindow(AsfwAny);

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int GetWindowRect(nint hwnd, out Rect rect);

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int cx, int cy, uint flags);

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int AllowSetForegroundWindow(int processId);

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint GetForegroundWindow();
}
