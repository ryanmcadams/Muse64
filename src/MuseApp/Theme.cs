using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using MuseApp.Native;

namespace MuseApp;

/// <summary>
/// Follows the Windows app theme (Settings → Personalization → Colors). Muse's web UI
/// follows prefers-color-scheme, so the native chrome, overlays and the WebView's
/// pre-paint background use matching colors to avoid a white flash in dark mode.
/// </summary>
internal static class Theme
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static bool IsDark { get; private set; } = true;

    public static event EventHandler? Changed;

    public static Color Background => IsDark ? Color.FromRgb(0x11, 0x11, 0x11) : Colors.White;

    public static System.Drawing.Color WebViewBackground =>
        System.Drawing.Color.FromArgb(Background.R, Background.G, Background.B);

    public static void Initialize()
    {
        IsDark = ReadIsDark();
        ApplyResources();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public static void Shutdown() => SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;

    /// <summary>Keeps a window's title bar in sync with the theme for its lifetime.</summary>
    public static void Track(Window window)
    {
        void Apply(object? sender, EventArgs e) =>
            Dwm.SetDarkTitleBar(new WindowInteropHelper(window).Handle, IsDark);

        window.SourceInitialized += Apply;
        Changed += Apply;
        window.Closed += (_, _) => Changed -= Apply;
    }

    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General)
            return;

        // SystemEvents may raise on its own thread; theme state is owned by the UI thread.
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            var dark = ReadIsDark();
            if (dark == IsDark)
                return;
            IsDark = dark;
            ApplyResources();
            Changed?.Invoke(null, EventArgs.Empty);
        });
    }

    private static bool ReadIsDark()
    {
        // A missing value (Windows builds before app dark mode existed) means light.
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
    }

    private static void ApplyResources()
    {
        var resources = Application.Current.Resources;
        resources["Muse.Background"] = Freeze(Background);
        resources["Muse.Foreground"] = Freeze(IsDark ? Color.FromRgb(0xF2, 0xF2, 0xF2) : Color.FromRgb(0x11, 0x11, 0x11));
        resources["Muse.Muted"] = Freeze(IsDark ? Color.FromRgb(0x9A, 0x9A, 0x9A) : Color.FromRgb(0x5F, 0x5F, 0x5F));
        resources["Muse.Bar"] = Freeze(IsDark ? Color.FromRgb(0x26, 0x26, 0x26) : Color.FromRgb(0xF0, 0xF0, 0xF0));
        resources["Muse.ButtonBackground"] = Freeze(IsDark ? Color.FromRgb(0x33, 0x33, 0x33) : Color.FromRgb(0xE6, 0xE6, 0xE6));
        resources["Muse.ButtonHover"] = Freeze(IsDark ? Color.FromRgb(0x44, 0x44, 0x44) : Color.FromRgb(0xD6, 0xD6, 0xD6));
    }

    private static SolidColorBrush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
