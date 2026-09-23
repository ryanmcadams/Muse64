using System.Drawing;
using System.Windows.Forms;
using MuseApp.Core;

namespace MuseApp;

/// <summary>
/// Notification-area icon and menu. Muse keeps running here while its window is hidden
/// so the signed-in session and live connections survive closing the window.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly AppSettings _settings;
    private readonly ToolStripMenuItem _startWithWindows = new("Start with Windows");
    private readonly ToolStripMenuItem _closeToTray = new("Close to tray");
    private readonly ToolStripMenuItem _minimizeToTray = new("Minimize to tray");
    private Balloon? _balloon;

    public TrayIcon(AppSettings settings)
    {
        _settings = settings;

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open Muse", null, (_, _) => Guarded(() => OpenRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add("Reload", null, (_, _) => Guarded(() => ReloadRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_startWithWindows);
        menu.Items.Add(_closeToTray);
        menu.Items.Add(_minimizeToTray);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit Muse", null, (_, _) => Guarded(() => QuitRequested?.Invoke(this, EventArgs.Empty)));
        menu.Opening += (_, _) => Guarded(RefreshChecks);

        _startWithWindows.Click += (_, _) =>
            Guarded(() => StartupRegistration.SetEnabled(!StartupRegistration.IsEnabled()));
        _closeToTray.Click += (_, _) => Guarded(() =>
        {
            _settings.CloseToTray = !_settings.CloseToTray;
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        });
        _minimizeToTray.Click += (_, _) => Guarded(() =>
        {
            _settings.MinimizeToTray = !_settings.MinimizeToTray;
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        });

        _icon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "Muse",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.MouseClick += (_, e) => Guarded(() =>
        {
            if (e.Button == MouseButtons.Left)
                ToggleRequested?.Invoke(this, EventArgs.Empty);
        });
        _icon.BalloonTipClicked += (_, _) => Guarded(() =>
        {
            // Guarded on its own: a stale notification must not stop the window from opening.
            Guarded(() => TakeBalloon()?.OnClicked?.Invoke());
            OpenRequested?.Invoke(this, EventArgs.Empty);
        });
        _icon.BalloonTipClosed += (_, _) => Guarded(() => TakeBalloon()?.OnClosed?.Invoke());
    }

    public event EventHandler? OpenRequested;
    public event EventHandler? ToggleRequested;
    public event EventHandler? ReloadRequested;
    public event EventHandler? QuitRequested;
    public event EventHandler? SettingsChanged;

    /// <summary>
    /// Shows a balloon (a toast on Windows 10/11). Only one is tracked at a time: a newer
    /// balloon replaces the older one, which is reported as closed.
    /// </summary>
    public void ShowBalloon(string? title, string? text, Action? onClicked = null, Action? onClosed = null)
    {
        TakeBalloon()?.OnClosed?.Invoke();
        _balloon = new Balloon(onClicked, onClosed);

        var heading = string.IsNullOrWhiteSpace(title) ? "Muse" : title;
        // NotifyIcon rejects an empty body.
        var body = string.IsNullOrWhiteSpace(text) ? heading : text;
        _icon.ShowBalloonTip(10_000, heading, body, ToolTipIcon.None);
    }

    public void Dispose()
    {
        TakeBalloon()?.OnClosed?.Invoke();
        // Hide before disposing so no ghost icon lingers in the notification area.
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Icon?.Dispose();
        _icon.Dispose();
        _startWithWindows.Dispose();
        _closeToTray.Dispose();
        _minimizeToTray.Dispose();
    }

    private void RefreshChecks()
    {
        _startWithWindows.Checked = StartupRegistration.IsEnabled();
        _closeToTray.Checked = _settings.CloseToTray;
        _minimizeToTray.Checked = _settings.MinimizeToTray;
    }

    /// <summary>
    /// Runs a tray event handler. WinForms turns an exception escaping a NotifyIcon or menu
    /// handler into its own "Continue/Quit" dialog, bypassing the app's crash handling, so
    /// failures here are logged and the tray stays usable.
    /// </summary>
    private static void Guarded(Action handler)
    {
        try
        {
            handler();
        }
        catch (Exception ex)
        {
            App.Log.Error("Tray action failed", ex);
        }
    }

    private Balloon? TakeBalloon()
    {
        var balloon = _balloon;
        _balloon = null;
        return balloon;
    }

    private static Icon LoadIcon()
    {
        using var stream = typeof(TrayIcon).Assembly.GetManifestResourceStream("MuseApp.muse.ico");
        return stream is not null
            ? new Icon(stream, SystemInformation.SmallIconSize)
            : Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? SystemIcons.Application;
    }

    private sealed record Balloon(Action? OnClicked, Action? OnClosed);
}
