using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;

namespace MuseApp;

public partial class MainWindow : Window
{
    private const string MuseUrl = "https://muse.ai";
    private const string WebView2DownloadUrl = "https://developer.microsoft.com/microsoft-edge/webview2/";

    private readonly WindowSettings _settings = WindowSettings.Load();
    private bool _initialized;

    public MainWindow()
    {
        InitializeComponent();
        _settings.ApplyTo(this);

        Loaded += OnLoaded;
        Closing += (_, _) => _settings.CaptureFrom(this);
        Closed += (_, _) => _settings.Save();

        // Ctrl+R / F5 reload, Ctrl+Plus/Minus/0 zoom.
        CommandBindings.Add(new CommandBinding(NavigationCommands.Refresh,
            (_, _) => WebView.Reload()));
        InputBindings.Add(new KeyBinding(NavigationCommands.Refresh,
            new KeyGesture(Key.R, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(NavigationCommands.Refresh, new KeyGesture(Key.F5)));
        InputBindings.Add(new KeyBinding(new ZoomCommand(zoomIn: true),
            new KeyGesture(Key.OemPlus, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(new ZoomCommand(zoomIn: false),
            new KeyGesture(Key.OemMinus, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(new ResetZoomCommand(),
            new KeyGesture(Key.D0, ModifierKeys.Control)));
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized)
            return;
        _initialized = true;

        try
        {
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Muse", "WebView2");

            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
            await WebView.EnsureCoreWebView2Async(environment);

            var core = WebView.CoreWebView2;

            // Popups and target=_blank links (OAuth, help articles, shared links)
            // open in the default browser instead of a blank window.
            core.NewWindowRequested += (_, args) =>
            {
                args.Handled = true;
                Process.Start(new ProcessStartInfo(args.Uri) { UseShellExecute = true });
            };

            // Keep downloads (exports, images) working via the default handler,
            // landing in the user's Downloads folder.
            core.DownloadStarting += (_, args) =>
            {
                args.ResultFilePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads",
                    Path.GetFileName(args.ResultFilePath));
            };

            core.Navigate(MuseUrl);

            LoadingPanel.Visibility = Visibility.Collapsed;
            WebView.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            ErrorDetails.Text = ex.Message;
            ErrorPanel.Visibility = Visibility.Visible;
        }
    }

    private void DownloadRuntimeButton_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(WebView2DownloadUrl) { UseShellExecute = true });

    private sealed class ZoomCommand(bool zoomIn) : ICommand
    {
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
            if (Application.Current.MainWindow is MainWindow window)
                window.WebView.ZoomFactor = Math.Clamp(window.WebView.ZoomFactor + (zoomIn ? 0.1 : -0.1), 0.25, 5.0);
        }
    }

    private sealed class ResetZoomCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
            if (Application.Current.MainWindow is MainWindow window)
                window.WebView.ZoomFactor = 1.0;
        }
    }
}
