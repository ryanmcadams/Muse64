using System.ComponentModel;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;
using MuseApp.Core;
using MuseApp.Native;

namespace MuseApp;

/// <summary>
/// Tray behavior: Muse keeps running when its window is closed or minimized to the tray,
/// so the signed-in session, websockets and notifications stay alive. Only "Quit Muse"
/// and Windows session end really exit.
/// </summary>
public partial class MainWindow
{
    private const int ToggleHotkeyId = 1;
    private const string TrayHint = "Muse is still running in the tray. Right-click the icon to quit.";

    private readonly TrayIcon _tray;
    private bool _quitting;
    private bool _hotkeyRegistered;

    /// <summary>Starts in the tray: the browser loads, but the window is not shown.</summary>
    internal void StartHidden()
    {
        // WPF connects a window's visual tree (and so the WebView2 child HWND) only when it
        // is first shown; EnsureHandle alone never starts the browser. So the window is shown
        // once far off-screen without activation, hidden again at once, then put back.
        _placement.Suspended = true;
        var restoreMaximized = _placement.Maximized;

        var hwnd = new WindowInteropHelper(this).EnsureHandle(); // restores saved bounds
        WindowStartupLocation = WindowStartupLocation.Manual;
        WindowState = WindowState.Normal;
        ShowActivated = false;
        var bounds = Windowing.GetBounds(hwnd);
        if (bounds is { } b)
            Windowing.SetBounds(hwnd, b with { Left = -32000, Top = -32000 });

        Show();
        Hide();

        if (bounds is { } original)
            Windowing.SetBounds(hwnd, original);
        if (restoreMaximized)
            WindowState = WindowState.Maximized; // applied by WPF on the next Show
        ShowActivated = true;
        _placement.Suspended = false;
        UpdateMemoryTarget();
        App.Log.Info("Started hidden in the tray");
    }

    /// <summary>Shows, restores and focuses the window (tray, hotkey, second launch, notification click).</summary>
    internal void ShowFromTray()
    {
        if (!IsVisible)
            Show();
        if (WindowState == WindowState.Minimized)
            WindowState = _placement.Maximized ? WindowState.Maximized : WindowState.Normal;

        Activate();
        // Foreground-lock workaround: a Topmost pulse raises the window even when Windows
        // declines the activation (e.g. the request came from a background launch).
        Topmost = true;
        Topmost = false;

        UpdateMemoryTarget();
        _host?.View.Focus();
    }

    /// <summary>Real exit: saves state, closes the window and shuts the app down.</summary>
    internal void Quit()
    {
        _quitting = true;
        Close();
    }

    /// <summary>The app is ending (Windows session end or a fatal error): let the window close for real.</summary>
    internal void PrepareForExit()
    {
        _quitting = true;
        SaveSettings();
    }

    private bool IsInFront =>
        IsVisible && WindowState != WindowState.Minimized &&
        Windowing.IsForeground(new WindowInteropHelper(this).Handle);

    private TrayIcon CreateTray()
    {
        var tray = new TrayIcon(_settings);
        tray.OpenRequested += (_, _) => ShowFromTray();
        tray.ToggleRequested += (_, _) =>
        {
            // The tray click itself deactivates the window, so "shown" is judged by visibility.
            if (IsVisible && WindowState != WindowState.Minimized)
                HideToTray();
            else
                ShowFromTray();
        };
        tray.ReloadRequested += (_, _) =>
        {
            if (WebViewSlot.Visibility == Visibility.Visible)
                _host?.Core.Reload();
            else
                _messageAction?.Invoke();
        };
        // Deferred: quitting disposes the tray menu, which must not happen inside its own click handler.
        tray.QuitRequested += (_, _) => Dispatcher.BeginInvoke(Quit);
        tray.SettingsChanged += (_, _) => SaveSettings();
        return tray;
    }

    private void HideToTray()
    {
        _placement.CaptureNow();
        // Otherwise WPF re-applies CenterScreen on the next Show and the window jumps.
        WindowStartupLocation = WindowStartupLocation.Manual;
        Hide();
        UpdateMemoryTarget();
        if (!_settings.ShownTrayHint)
        {
            _settings.ShownTrayHint = true;
            _tray.ShowBalloon("Muse", TrayHint);
        }
        SaveSettings();
    }

    /// <summary>Hidden Muse asks WebView2 to trim memory; it is never suspended, so live connections persist.</summary>
    private void UpdateMemoryTarget()
    {
        if (_host?.TryGetCore() is { } core)
        {
            core.MemoryUsageTargetLevel = IsVisible && WindowState != WindowState.Minimized
                ? CoreWebView2MemoryUsageTargetLevel.Normal
                : CoreWebView2MemoryUsageTargetLevel.Low;
        }
    }

    private void RegisterHotkey()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
        HotkeyGesture gesture;
        switch (HotkeyGesture.TryParse(_settings.Hotkey, out var parsed))
        {
            case HotkeyParseStatus.Disabled:
                App.Log.Info("Global hotkey disabled in settings");
                return;
            case HotkeyParseStatus.Valid:
                gesture = parsed!;
                break;
            default:
                // Only the length is logged: the setting is free text typed by the user.
                App.Log.Warn($"Invalid Hotkey setting ({_settings.Hotkey?.Length ?? 0} chars); using {HotkeyGesture.DefaultText}");
                gesture = HotkeyGesture.Default;
                break;
        }

        var error = Hotkeys.Register(hwnd, ToggleHotkeyId,
            Hotkeys.ToModifiers(gesture.Modifiers), Hotkeys.ToVirtualKey(gesture.Key));
        _hotkeyRegistered = error == 0;
        // Another app may own the combination; the hotkey is a convenience, so just note it.
        if (_hotkeyRegistered)
            App.Log.Info($"Global hotkey {gesture} registered");
        else
            App.Log.Warn($"Global hotkey {gesture} unavailable (Win32 error {error})");
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == Hotkeys.WmHotkey && wParam == ToggleHotkeyId)
        {
            handled = true;
            // Unlike a tray click, the hotkey leaves focus alone: a visible but buried
            // window is brought forward, and only the foreground window is hidden.
            if (IsInFront)
                HideToTray();
            else
                ShowFromTray();
        }
        return 0;
    }

    private void OnNotificationReceived(object? sender, CoreWebView2NotificationReceivedEventArgs e)
    {
        // Handled: WebView2's own toast would be attributed to "msedgewebview2" and bypass the tray.
        e.Handled = true;
        var notification = e.Notification;

        if (IsInFront)
        {
            // The user is looking at Muse, which shows its own in-page UI.
            notification.ReportShown();
            notification.ReportClosed();
            return;
        }

        App.Log.Info($"Notification from {UrlPolicy.Describe(e.SenderOrigin)} shown from the tray");
        _tray.ShowBalloon(notification.Title, notification.Body,
            onClicked: notification.ReportClicked,
            onClosed: notification.ReportClosed);
        notification.ReportShown();
    }

    private void SaveSettings()
    {
        _settings.WindowRect = _placement.NormalBounds;
        _settings.Maximized = _placement.Maximized;
        // Read at save time: WebView2 does not raise ZoomFactorChanged for programmatic changes.
        if (_host?.TryGetCore() is not null)
            _settings.ZoomFactor = _host.View.ZoomFactor;
        if (!App.Settings.Save(_settings))
            App.Log.Warn("Could not save settings");
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_quitting && _settings.CloseToTray)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }
        _quitting = true;
        _placement.CaptureNow();
        SaveSettings();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
        var hwnd = new WindowInteropHelper(this).Handle;
        if (_hotkeyRegistered)
            Hotkeys.Unregister(hwnd, ToggleHotkeyId);
        _tray.Dispose();
        _host?.Dispose();
        // ShutdownMode is OnExplicitShutdown (hiding the window must not exit), so exit here.
        Application.Current.Shutdown();
    }
}
