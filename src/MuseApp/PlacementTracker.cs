using System.Windows;
using System.Windows.Interop;
using MuseApp.Core;
using MuseApp.Native;

namespace MuseApp;

/// <summary>
/// Restores and tracks a window's normal-state bounds in physical pixels. Bounds are
/// clamped onto a current monitor on restore (see <see cref="WindowPlacement"/>), so a
/// window saved on a since-removed monitor never opens off-screen.
/// </summary>
internal sealed class PlacementTracker
{
    private readonly Window _window;
    private nint _hwnd;

    public PlacementTracker(Window window, AppSettings saved)
    {
        _window = window;
        NormalBounds = saved.WindowRect;
        Maximized = saved.Maximized;
        if (NormalBounds is not null)
            window.WindowStartupLocation = WindowStartupLocation.Manual;

        window.SourceInitialized += OnSourceInitialized;
        window.LocationChanged += (_, _) => Capture();
        window.SizeChanged += (_, _) => Capture();
        window.StateChanged += (_, _) =>
        {
            if (!Suspended && window.WindowState != WindowState.Minimized)
                Maximized = window.WindowState == WindowState.Maximized;
        };
    }

    /// <summary>Last bounds while in the normal (not maximized/minimized) state.</summary>
    public PixelRect? NormalBounds { get; private set; }

    /// <summary>Whether the window was maximized last time it was not minimized.</summary>
    public bool Maximized { get; private set; }

    /// <summary>Stops tracking while the window is in a transient state such as fullscreen.</summary>
    public bool Suspended { get; set; }

    /// <summary>Moves a window fully onto the monitor it overlaps most (used for popups).</summary>
    public static void EnsureOnScreen(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (Windowing.GetBounds(hwnd) is not { } bounds)
            return;
        var clamped = WindowPlacement.Clamp(bounds, Monitors.GetWorkAreas());
        if (clamped != bounds)
            Windowing.SetBounds(hwnd, clamped);
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(_window).Handle;
        if (NormalBounds is { } saved)
        {
            var bounds = WindowPlacement.Clamp(saved, Monitors.GetWorkAreas());
            // Twice on purpose: if the first move crosses onto a monitor with a different
            // DPI, Windows rescales the window (WM_DPICHANGED); the second call restores the exact size.
            Windowing.SetBounds(_hwnd, bounds);
            Windowing.SetBounds(_hwnd, bounds);
            NormalBounds = bounds;
        }
        if (Maximized)
            _window.WindowState = WindowState.Maximized;
    }

    /// <summary>Records the current bounds if the window is in the normal state.</summary>
    public void CaptureNow() => Capture();

    private void Capture()
    {
        if (!Suspended && _hwnd != 0 && _window.WindowState == WindowState.Normal &&
            Windowing.GetBounds(_hwnd) is { } bounds)
            NormalBounds = bounds;
    }
}
