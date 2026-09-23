using System.Windows;
using Microsoft.Web.WebView2.Core;
using MuseApp.Core;

namespace MuseApp;

/// <summary>
/// In-app popup for OAuth/login windows that must keep <c>window.opener</c>. It shares
/// the main environment (cookies, session) and the same URL/permission policy, and has
/// no address bar, so the title always names the host it is showing.
/// </summary>
public partial class PopupWindow : Window
{
    internal PopupWindow(Window owner, CoreWebView2WindowFeatures? features)
    {
        InitializeComponent();
        Owner = owner;
        Theme.Track(this);
        ApplyFeatures(features);

        Host = new WebViewHost();
        WebViewSlot.Content = Host.View;

        SourceInitialized += (_, _) => PlacementTracker.EnsureOnScreen(this);
        Closed += (_, _) => Host.Dispose();
    }

    internal WebViewHost Host { get; }

    internal async Task InitializeAsync(CoreWebView2Environment environment)
    {
        await Host.InitializeAsync(environment);
        var core = Host.Core;
        core.WindowCloseRequested += (_, _) => Close();
        core.DocumentTitleChanged += (_, _) => UpdateTitle();
        core.SourceChanged += (_, _) => UpdateTitle();
        core.ProcessFailed += (_, e) =>
        {
            // A popup is short-lived; rather than recover it, close it and let the user retry.
            App.Log.Warn($"Popup WebView2 process failed: {e.ProcessFailedKind}");
            if (e.ProcessFailedKind is CoreWebView2ProcessFailedKind.BrowserProcessExited or
                CoreWebView2ProcessFailedKind.RenderProcessExited or
                CoreWebView2ProcessFailedKind.RenderProcessUnresponsive)
                Close();
        };
    }

    private void ApplyFeatures(CoreWebView2WindowFeatures? features)
    {
        if (features is null)
            return;
        // window.open sizes are CSS pixels, which match WPF DIPs at 100% page zoom.
        if (features.HasSize)
        {
            Width = Math.Clamp(features.Width, MinWidth, 1600);
            Height = Math.Clamp(features.Height, MinHeight, 1200);
        }
        if (features.HasPosition)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = features.Left;
            Top = features.Top;
        }
    }

    private void UpdateTitle() => Title = WindowTitle.ComposePopup(Host.Core.DocumentTitle, Host.Core.Source);
}
