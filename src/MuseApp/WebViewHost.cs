using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using MuseApp.Core;

namespace MuseApp;

/// <summary>
/// Owns one WebView2 control and applies the policy every Muse web surface shares
/// (main window and in-app popups): settings, navigation/popup/scheme policy,
/// permissions, context menu, downloads and keyboard shortcuts.
/// Window-specific behavior (overlays, title, crash recovery) stays in the windows.
/// </summary>
internal sealed class WebViewHost : IDisposable
{
    // Context-menu entries that expose developer or "escape the app" affordances.
    private static readonly HashSet<string> HiddenMenuItems = new(StringComparer.OrdinalIgnoreCase)
    {
        "inspectElement", "inspect", "viewPageSource", "viewSource", "openLinkInNewWindow",
    };

    private static readonly bool DevToolsEnabled =
        Debugger.IsAttached || Environment.GetEnvironmentVariable("MUSE_DEVTOOLS") == "1";

    public WebViewHost()
    {
        // Set before the core exists so the first frame is the theme color, not white.
        View = new WebView2 { DefaultBackgroundColor = Theme.WebViewBackground };
        View.KeyDown += OnKeyDown;
        Theme.Changed += OnThemeChanged;
    }

    public WebView2 View { get; }

    /// <summary>Available after <see cref="InitializeAsync"/>.</summary>
    public CoreWebView2 Core => View.CoreWebView2 ?? throw new InvalidOperationException("WebView2 is not initialized.");

    /// <summary>
    /// The CoreWebView2, or null before initialization or after its browser process died.
    /// (The WPF control's CoreWebView2 getter throws once the browser process has crashed,
    /// and state/UI code must keep working until the control is re-created.)
    /// </summary>
    public CoreWebView2? TryGetCore()
    {
        try
        {
            return View.CoreWebView2;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public static Task<CoreWebView2Environment> CreateEnvironmentAsync()
    {
        var options = new CoreWebView2EnvironmentOptions
        {
            AreBrowserExtensionsEnabled = false,
            // Never silently sign in to sites with the Windows account.
            AllowSingleSignOnUsingOSPrimaryAccount = false,
        };
        return CoreWebView2Environment.CreateAsync(null, AppPaths.WebViewUserData, options);
    }

    /// <summary>Creates the CoreWebView2 (the control must already be in a shown window).</summary>
    public async Task InitializeAsync(CoreWebView2Environment environment)
    {
        await View.EnsureCoreWebView2Async(environment);
        var core = Core;

        var settings = core.Settings;
        settings.AreDevToolsEnabled = DevToolsEnabled;
        settings.AreDefaultContextMenusEnabled = true;
        settings.IsStatusBarEnabled = true;
        settings.IsSwipeNavigationEnabled = false;
        settings.IsBuiltInErrorPageEnabled = false; // the app shows its own offline panel
        settings.IsPasswordAutosaveEnabled = false;
        settings.IsGeneralAutofillEnabled = true;
        settings.IsReputationCheckingRequired = true;
        core.Profile.PreferredColorScheme = CoreWebView2PreferredColorScheme.Auto;

        core.NavigationStarting += OnNavigationStarting;
        core.NewWindowRequested += OnNewWindowRequested;
        core.PermissionRequested += OnPermissionRequested;
        core.ScreenCaptureStarting += OnScreenCaptureStarting;
        core.LaunchingExternalUriScheme += OnLaunchingExternalUriScheme;
        core.ServerCertificateErrorDetected += OnServerCertificateErrorDetected;
        core.BasicAuthenticationRequested += OnBasicAuthenticationRequested;
        core.ContextMenuRequested += OnContextMenuRequested;
        core.DownloadStarting += OnDownloadStarting;
    }

    /// <summary>Opens a URL in the user's default browser, only if it is a plain web URL.</summary>
    public static void OpenInBrowser(Uri uri)
    {
        if (!UrlPolicy.IsSafeToShellExecute(uri))
        {
            App.Log.Warn($"Refused to open {UrlPolicy.Describe(uri)} externally");
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Win32Exception ex)
        {
            App.Log.Warn($"Could not open {UrlPolicy.Describe(uri)} in the browser: {ex.Message}");
        }
    }

    public void Dispose()
    {
        Theme.Changed -= OnThemeChanged;
        View.KeyDown -= OnKeyDown;
        View.Dispose();
    }

    private void OnThemeChanged(object? sender, EventArgs e) => View.DefaultBackgroundColor = Theme.WebViewBackground;

    private static void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (UrlPolicy.ClassifyTopLevel(e.Uri) != TopLevelDecision.Block)
            return;
        e.Cancel = true;
        App.Log.Warn($"Blocked top-level navigation to {UrlPolicy.Describe(e.Uri)}");
    }

    private async void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        // Always handled: WebView2's default would open an unmanaged browser window.
        e.Handled = true;
        var decision = UrlPolicy.ClassifyPopup(e.Uri, e.IsUserInitiated);
        switch (decision)
        {
            case PopupDecision.ExternalBrowser:
                App.Log.Info($"Popup to {UrlPolicy.Describe(e.Uri)} sent to default browser");
                OpenInBrowser(UrlPolicy.TryParse(e.Uri)!);
                return;
            case PopupDecision.Drop:
                App.Log.Warn($"Dropped popup to {UrlPolicy.Describe(e.Uri)} (user initiated: {e.IsUserInitiated})");
                return;
        }

        // In-app popup (OAuth, login): same environment so cookies and window.opener work.
        var deferral = e.GetDeferral();
        PopupWindow? popup = null;
        try
        {
            popup = new PopupWindow(Application.Current.MainWindow, e.WindowFeatures);
            popup.Show();
            await popup.InitializeAsync(Core.Environment);
            e.NewWindow = popup.Host.Core;
            App.Log.Info($"Popup to {UrlPolicy.Describe(e.Uri)} opened in app");
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            App.Log.Error($"Could not open in-app popup for {UrlPolicy.Describe(e.Uri)}", ex);
            popup?.Close();
        }
        finally
        {
            deferral.Complete();
        }
    }

    private static void OnPermissionRequested(object? sender, CoreWebView2PermissionRequestedEventArgs e)
    {
        var kind = e.PermissionKind switch
        {
            CoreWebView2PermissionKind.Microphone => PermissionKind.Microphone,
            CoreWebView2PermissionKind.Camera => PermissionKind.Camera,
            CoreWebView2PermissionKind.Geolocation => PermissionKind.Geolocation,
            CoreWebView2PermissionKind.Notifications => PermissionKind.Notifications,
            CoreWebView2PermissionKind.ClipboardRead => PermissionKind.ClipboardRead,
            CoreWebView2PermissionKind.Autoplay => PermissionKind.Autoplay,
            _ => PermissionKind.Other,
        };
        var decision = PermissionPolicy.Decide(e.Uri, kind);
        switch (decision)
        {
            case PermissionDecision.Allow:
                e.State = CoreWebView2PermissionState.Allow;
                break;
            case PermissionDecision.Deny:
                e.State = CoreWebView2PermissionState.Deny;
                App.Log.Info($"Denied {e.PermissionKind} for {UrlPolicy.Describe(e.Uri)}");
                break;
            default:
                e.State = CoreWebView2PermissionState.Default;
                e.SavesInProfile = true;
                break;
        }
    }

    private void OnScreenCaptureStarting(object? sender, CoreWebView2ScreenCaptureStartingEventArgs e)
    {
        var origin = e.OriginalSourceFrameInfo?.Source ?? Core.Source;
        if (PermissionPolicy.Decide(origin, PermissionKind.ScreenCapture) != PermissionDecision.Deny)
            return;
        e.Cancel = true;
        App.Log.Info($"Denied screen capture for {UrlPolicy.Describe(origin)}");
    }

    private static void OnLaunchingExternalUriScheme(object? sender, CoreWebView2LaunchingExternalUriSchemeEventArgs e)
    {
        // mailto:/tel: fall through to WebView2's own "open this app?" confirmation.
        if (UrlPolicy.IsAllowedExternalScheme(e.Uri))
            return;
        e.Cancel = true;
        App.Log.Warn($"Blocked external scheme {UrlPolicy.Describe(e.Uri)} from {UrlPolicy.Describe(e.InitiatingOrigin)}");
    }

    private static void OnServerCertificateErrorDetected(object? sender, CoreWebView2ServerCertificateErrorDetectedEventArgs e) =>
        // Action is left at Default (the request fails); certificate errors are never bypassed.
        App.Log.Warn($"Certificate error {e.ErrorStatus} for {UrlPolicy.Describe(e.RequestUri)}");

    private static void OnBasicAuthenticationRequested(object? sender, CoreWebView2BasicAuthenticationRequestedEventArgs e)
    {
        // Muse never uses HTTP auth; a prompt here could only be a phishing attempt.
        e.Cancel = true;
        App.Log.Warn($"Cancelled basic auth request from {UrlPolicy.Describe(e.Uri)}");
    }

    private static void OnContextMenuRequested(object? sender, CoreWebView2ContextMenuRequestedEventArgs e)
    {
        if (DevToolsEnabled)
            return;
        var items = e.MenuItems;
        for (var i = items.Count - 1; i >= 0; i--)
        {
            if (HiddenMenuItems.Contains(items[i].Name))
                items.RemoveAt(i);
        }
    }

    private static void OnDownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e) =>
        // WebView2's default UI and the user's real Downloads known folder are kept.
        App.Log.Info($"Download from {UrlPolicy.Describe(e.DownloadOperation.Uri)}: {Path.GetFileName(e.ResultFilePath)}");

    /// <summary>
    /// The single place for app shortcuts. The WPF WebView2 control re-raises accelerator
    /// keys pressed inside the page as KeyDown; marking them handled stops Chromium's own
    /// handling so zoom steps and reloads are not applied twice.
    /// </summary>
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (TryGetCore() is not { } core)
            return;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;
        // Ctrl without Alt: AltGr (= Ctrl+Alt) produces characters on many layouts.
        var ctrl = modifiers.HasFlag(ModifierKeys.Control) && !modifiers.HasFlag(ModifierKeys.Alt);

        if ((key == Key.F5 && modifiers == ModifierKeys.None) || (ctrl && key == Key.R))
        {
            core.Reload();
        }
        else if (ctrl && key is Key.OemPlus or Key.Add)
        {
            View.ZoomFactor = ZoomLevels.ZoomIn(View.ZoomFactor);
        }
        else if (ctrl && key is Key.OemMinus or Key.Subtract)
        {
            View.ZoomFactor = ZoomLevels.ZoomOut(View.ZoomFactor);
        }
        else if (ctrl && key is Key.D0 or Key.NumPad0)
        {
            View.ZoomFactor = AppSettings.DefaultZoom;
        }
        else
        {
            return;
        }
        e.Handled = true;
    }
}
