using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using MuseApp.Core;

namespace MuseApp;

/// <summary>
/// The Muse window: browser lifecycle, loading/offline/crash panels and window chrome.
/// Tray, hotkey and hide/show behavior live in MainWindow.Tray.cs.
/// </summary>
public partial class MainWindow : Window
{
    private const string RuntimeBootstrapperUrl = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";

    private readonly AppSettings _settings = App.Settings.Load();
    private readonly PlacementTracker _placement;

    // At most 3 automatic recoveries per minute; a crash loop ends in the error panel.
    private readonly RestartBudget _restartBudget = new(3, TimeSpan.FromMinutes(1));

    private CoreWebView2Environment? _environment;
    private WebViewHost? _host;
    private Uri? _lastRequestedUri;
    private Uri? _lastLoadedUri;
    private Action? _messageAction;
    private bool _retryWhenOnline;
    private WindowState? _stateBeforeFullScreen;
    private WindowState _previousState;

    public MainWindow()
    {
        InitializeComponent();
        Theme.Track(this);
        _placement = new PlacementTracker(this, _settings);
        _previousState = WindowState;

        _tray = CreateTray();

        // Started from SourceInitialized (the first moment an HWND exists) rather than Loaded,
        // so it runs the same way for a normal launch and for the tray start (StartHidden).
        SourceInitialized += (_, _) =>
        {
            RegisterHotkey();
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, async () => await StartBrowserAsync(UrlPolicy.Home));
        };
        Closing += OnClosing;
        Closed += OnClosed;
        StateChanged += OnStateChanged;
        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
    }

    private async Task StartBrowserAsync(Uri target)
    {
        ShowLoading("Loading…");

        string runtimeVersion;
        try
        {
            runtimeVersion = CoreWebView2Environment.GetAvailableBrowserVersionString();
        }
        catch (WebView2RuntimeNotFoundException ex)
        {
            App.Log.Error("WebView2 runtime not found", ex);
            ShowMessage(
                "Couldn't start the embedded browser.",
                "Muse needs the free Microsoft Edge WebView2 Runtime. Install it (a ~2 MB download), then relaunch this app.",
                ex.Message,
                "Get WebView2 Runtime",
                () => WebViewHost.OpenInBrowser(new Uri(RuntimeBootstrapperUrl)));
            return;
        }
        catch (Exception ex)
        {
            ShowStartFailure("WebView2 runtime check failed", ex);
            return;
        }

        App.Log.Info($"WebView2 runtime {runtimeVersion}, SDK {typeof(CoreWebView2).Assembly.GetName().Version}");

        try
        {
            _environment = await WebViewHost.CreateEnvironmentAsync();
            var host = new WebViewHost();
            _host = host;
            WebViewSlot.Content = host.View;
            await host.InitializeAsync(_environment);
            AttachMainHandlers(host);
            host.View.ZoomFactor = ZoomLevels.Normalize(_settings.ZoomFactor);
            UpdateMemoryTarget();
            host.Core.Navigate(target.AbsoluteUri);
        }
        catch (Exception ex)
        {
            // Catch-all on purpose: anything escaping here would leave "Loading…" up forever.
            ShowStartFailure("WebView2 initialization failed", ex);
        }
    }

    /// <summary>Logs a startup/recovery failure and offers Retry (which rebuilds the browser from scratch).</summary>
    private void ShowStartFailure(string logMessage, Exception ex)
    {
        App.Log.Error(logMessage, ex);
        ShowMessage(
            "Muse couldn't start the embedded browser",
            $"Something went wrong while starting WebView2. Details were saved to:\n{App.Log.FilePath}",
            ex.Message,
            "Retry",
            () => _ = RecreateBrowserAsync());
    }

    private void AttachMainHandlers(WebViewHost host)
    {
        var core = host.Core;
        core.NavigationStarting += (_, e) =>
        {
            if (!e.Cancel)
                _lastRequestedUri = UrlPolicy.TryParse(e.Uri);
        };
        core.NavigationCompleted += OnNavigationCompleted;
        core.SourceChanged += (_, _) => UpdateInfoBar();
        core.DocumentTitleChanged += (_, _) => UpdateTitle();
        core.ContainsFullScreenElementChanged += (_, _) => SetFullScreen(core.ContainsFullScreenElement);
        core.WindowCloseRequested += (_, _) => App.Log.Info("Ignored window.close() from the main page");
        core.ProcessFailed += OnProcessFailed;
        core.NotificationReceived += OnNotificationReceived;
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            _lastLoadedUri = UrlPolicy.TryParse(_host?.Core.Source);
            ShowBrowser();
            return;
        }

        // Superseded or policy-cancelled navigations are not failures the user needs to see.
        if (e.WebErrorStatus == CoreWebView2WebErrorStatus.OperationCanceled)
            return;

        App.Log.Warn($"Navigation to {UrlPolicy.Describe(_lastRequestedUri)} failed: {e.WebErrorStatus}");
        var offline = e.WebErrorStatus is
            CoreWebView2WebErrorStatus.ConnectionAborted or CoreWebView2WebErrorStatus.ConnectionReset or
            CoreWebView2WebErrorStatus.Disconnected or CoreWebView2WebErrorStatus.CannotConnect or
            CoreWebView2WebErrorStatus.HostNameNotResolved or CoreWebView2WebErrorStatus.Timeout or
            CoreWebView2WebErrorStatus.ServerUnreachable;

        ShowMessage(
            offline ? "Can't reach Muse" : "Couldn't load this page",
            offline
                ? "Check your internet connection. Muse will try again automatically when you're back online."
                : "The page failed to load.",
            $"{UrlPolicy.Describe(_lastRequestedUri)} — {e.WebErrorStatus}",
            "Retry",
            RetryNavigation);
        _retryWhenOnline = offline;
    }

    private void RetryNavigation()
    {
        if (_host is null)
        {
            _ = RecreateBrowserAsync();
            return;
        }
        ShowLoading("Loading…");
        _host.Core.Navigate((_lastRequestedUri ?? UrlPolicy.Home).AbsoluteUri);
    }

    private async void OnProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        App.Log.Warn($"WebView2 process failed: {e.ProcessFailedKind} (reason {e.Reason}, exit code {e.ExitCode})");
        switch (e.ProcessFailedKind)
        {
            case CoreWebView2ProcessFailedKind.RenderProcessExited:
            case CoreWebView2ProcessFailedKind.RenderProcessUnresponsive:
                if (_restartBudget.TryConsume())
                    _host?.Core.Reload();
                else
                    ShowCrashLoop(() => { _restartBudget.Reset(); _host?.Core.Reload(); ShowLoading("Loading…"); });
                break;

            // Includes the runtime auto-updating under us: the whole browser is gone.
            case CoreWebView2ProcessFailedKind.BrowserProcessExited:
                if (_restartBudget.TryConsume())
                    await RecreateBrowserAsync();
                else
                    ShowCrashLoop(() => { _restartBudget.Reset(); _ = RecreateBrowserAsync(); });
                break;

            // GPU, utility and subframe processes are restarted by WebView2 itself.
            default:
                break;
        }
    }

    private void ShowCrashLoop(Action retry) =>
        ShowMessage(
            "Muse stopped responding",
            "The embedded browser keeps crashing. You can try again, or restart the app.",
            null,
            "Retry",
            retry);

    /// <summary>Tears down the dead WebView2 (and its environment) and starts a fresh one.</summary>
    private async Task RecreateBrowserAsync()
    {
        // Callers fire and forget (Retry buttons, tray Reload), so nothing may escape.
        try
        {
            ShowLoading("Reconnecting…");
            foreach (var popup in OwnedWindows.OfType<PopupWindow>().ToList())
                popup.Close();

            var oldEnvironment = _environment;
            var oldHost = _host;
            _environment = null;
            _host = null;
            if (oldHost is not null)
            {
                // The new environment reuses the same user data folder, which is only released
                // once the old browser process group reports that it has fully exited.
                var exited = new TaskCompletionSource();
                if (oldEnvironment is not null)
                    oldEnvironment.BrowserProcessExited += (_, _) => exited.TrySetResult();
                WebViewSlot.Content = null;
                oldHost.Dispose();
                await Task.WhenAny(exited.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            }

            // Resume where the user was if it was a Muse page; never replay off-site/OAuth URLs.
            var target = UrlPolicy.ClassifyTopLevel(_lastLoadedUri) == TopLevelDecision.Allow &&
                         _lastLoadedUri?.Scheme == Uri.UriSchemeHttps
                ? _lastLoadedUri
                : UrlPolicy.Home;
            await StartBrowserAsync(target);
        }
        catch (Exception ex)
        {
            ShowStartFailure("WebView2 recovery failed", ex);
        }
    }

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        if (!e.IsAvailable)
            return;
        Dispatcher.BeginInvoke(() =>
        {
            if (!_retryWhenOnline)
                return;
            App.Log.Info("Network available again; retrying");
            RetryNavigation();
        });
    }

    private void SetFullScreen(bool fullScreen)
    {
        if (fullScreen && _stateBeforeFullScreen is null)
        {
            _stateBeforeFullScreen = WindowState;
            _placement.Suspended = true;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            // Re-maximize so the borderless window covers the taskbar too.
            WindowState = WindowState.Normal;
            WindowState = WindowState.Maximized;
        }
        else if (!fullScreen && _stateBeforeFullScreen is { } previous)
        {
            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode = ResizeMode.CanResize;
            WindowState = previous;
            _stateBeforeFullScreen = null;
            _placement.Suspended = false;
        }
        UpdateInfoBar();
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        // Returning from the taskbar should put keyboard focus back into the page.
        if (_previousState == WindowState.Minimized && WindowState != WindowState.Minimized)
            _host?.View.Focus();
        _previousState = WindowState;

        if (WindowState == WindowState.Minimized && _settings.MinimizeToTray && !_quitting)
            HideToTray();
    }

    private void UpdateTitle() => Title = WindowTitle.Compose(_host?.Core.DocumentTitle);

    private void UpdateInfoBar()
    {
        var source = UrlPolicy.TryParse(_host?.TryGetCore()?.Source);
        var offSite = UrlPolicy.ClassifyTopLevel(source) == TopLevelDecision.AllowOffSite;
        var visible = offSite && _stateBeforeFullScreen is null && WebViewSlot.Visibility == Visibility.Visible;
        if (visible)
            InfoBarText.Text = $"You've left Muse — {source!.Host}";
        InfoBar.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowLoading(string text)
    {
        _retryWhenOnline = false;
        LoadingText.Text = text;
        LoadingPanel.Visibility = Visibility.Visible;
        MessagePanel.Visibility = Visibility.Collapsed;
        WebViewSlot.Visibility = Visibility.Hidden;
        UpdateInfoBar();
    }

    private void ShowBrowser()
    {
        _retryWhenOnline = false;
        var wasHidden = WebViewSlot.Visibility != Visibility.Visible;
        LoadingPanel.Visibility = Visibility.Collapsed;
        MessagePanel.Visibility = Visibility.Collapsed;
        WebViewSlot.Visibility = Visibility.Visible;
        UpdateInfoBar();
        if (wasHidden && IsActive)
            _host?.View.Focus();
    }

    private void ShowMessage(string title, string body, string? details, string buttonText, Action action)
    {
        _retryWhenOnline = false;
        MessageTitle.Text = title;
        MessageBody.Text = body;
        MessageDetails.Text = details ?? string.Empty;
        MessageDetails.Visibility = string.IsNullOrEmpty(details) ? Visibility.Collapsed : Visibility.Visible;
        MessageButton.Content = buttonText;
        _messageAction = action;
        LoadingPanel.Visibility = Visibility.Collapsed;
        MessagePanel.Visibility = Visibility.Visible;
        WebViewSlot.Visibility = Visibility.Hidden;
        UpdateInfoBar();
    }

    private void MessageButton_Click(object sender, RoutedEventArgs e) => _messageAction?.Invoke();

    private void BackToMuse_Click(object sender, RoutedEventArgs e) =>
        _host?.Core.Navigate(UrlPolicy.Home.AbsoluteUri);
}
