namespace MuseApp.Core;

/// <summary>
/// Everything the app writes lives under %LOCALAPPDATA%\Muse (settings, logs, browser
/// profile). v2.0 kept settings in roaming %APPDATA%\Muse; that file is migrated once.
/// </summary>
public static class AppPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Muse");

    public static string SettingsFile { get; } = Path.Combine(Root, "settings.json");

    public static string LogDirectory { get; } = Path.Combine(Root, "logs");

    public static string WebViewUserData { get; } = Path.Combine(Root, "WebView2");

    public static string LegacySettingsFile { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Muse", "settings.json");
}
