using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using MuseApp.Core;

namespace MuseApp;

public partial class App : Application
{
    private SingleInstance? _instance;
    private int _reportedFatal;

    internal static AppLog Log { get; } = new(AppPaths.LogDirectory);

    internal static SettingsStore Settings { get; } = new(AppPaths.SettingsFile, AppPaths.LegacySettingsFile);

    internal static string Version { get; } =
        typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "?";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        RegisterCrashHandlers();

        _instance = SingleInstance.TryAcquire();
        if (_instance is null)
        {
            SingleInstance.SignalExisting();
            Shutdown();
            return;
        }

        Log.Info($"Muse {Version} starting ({RuntimeInformation.FrameworkDescription}, " +
                 $"{RuntimeInformation.OSDescription}, {RuntimeInformation.ProcessArchitecture})");

        var options = CommandLineOptions.Parse(e.Args);
        if (options.Reset)
            ResetUserData();

        Theme.Initialize();

        var window = new MainWindow();
        MainWindow = window;
        // A second launch (e.g. clicking the Start menu entry while Muse is in the tray) shows the window.
        _instance.ListenForActivation(() => Dispatcher.BeginInvoke(window.ShowFromTray));
        SessionEnding += (_, _) => window.PrepareForExit();

        if (options.StartMinimized)
            window.StartHidden();
        else
            window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_instance is not null)
        {
            Theme.Shutdown();
            _instance.Dispose();
            Log.Info($"Muse exiting (code {e.ApplicationExitCode})");
        }
        base.OnExit(e);
    }

    /// <summary>--reset: wipe the browser profile (cookies, login) and settings, keep logs.</summary>
    private static void ResetUserData()
    {
        Log.Info("--reset: deleting WebView2 profile and settings");
        Settings.Delete();
        try
        {
            if (Directory.Exists(AppPaths.WebViewUserData))
                Directory.Delete(AppPaths.WebViewUserData, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warn($"--reset: could not delete WebView2 profile: {ex.Message}");
        }
    }

    private void RegisterCrashHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            ReportFatal("AppDomain", args.ExceptionObject as Exception);

        // An unobserved task fault is by definition something nobody awaited: record it,
        // but do not take the whole app down at an arbitrary GC moment.
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error("Unobserved task exception", args.Exception);
            args.SetObserved();
        };
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        // Report first: saving state touches the (possibly broken) window and must not
        // turn a logged error into a silent process kill.
        ReportFatal("Dispatcher", e.Exception);
        try
        {
            (MainWindow as MainWindow)?.PrepareForExit();
        }
        catch (Exception ex)
        {
            Log.Error("Could not save state while exiting", ex);
        }
        Shutdown(1);
    }

    private void ReportFatal(string source, Exception? exception)
    {
        Log.Error($"Unhandled exception ({source})", exception);
        if (Interlocked.Exchange(ref _reportedFatal, 1) != 0)
            return;

        MessageBox.Show(
            $"Muse hit a problem and needs to close.\n\nDetails were saved to:\n{Log.FilePath}",
            "Muse", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
