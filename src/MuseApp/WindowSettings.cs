using System.IO;
using System.Text.Json;
using System.Windows;

namespace MuseApp;

/// <summary>
/// Persists window placement between sessions in %APPDATA%\Muse\settings.json.
/// </summary>
internal sealed class WindowSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Muse",
        "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public double Left { get; set; } = double.NaN;
    public double Top { get; set; } = double.NaN;
    public double Width { get; set; } = 1280;
    public double Height { get; set; } = 860;
    public bool Maximized { get; set; }

    public static WindowSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                if (JsonSerializer.Deserialize<WindowSettings>(json) is { } settings)
                    return settings;
            }
        }
        catch
        {
            // Corrupt settings file: fall back to defaults.
        }
        return new WindowSettings();
    }

    public void ApplyTo(Window window)
    {
        window.Width = Width;
        window.Height = Height;

        if (!double.IsNaN(Left) && !double.IsNaN(Top))
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = Left;
            window.Top = Top;
        }

        if (Maximized)
            window.WindowState = WindowState.Maximized;
    }

    public void CaptureFrom(Window window)
    {
        // When maximized, RestoreBounds holds the pre-maximized placement.
        var bounds = window.WindowState == WindowState.Maximized ? window.RestoreBounds
            : new Rect(window.Left, window.Top, window.Width, window.Height);

        Left = bounds.Left;
        Top = bounds.Top;
        Width = bounds.Width;
        Height = bounds.Height;
        Maximized = window.WindowState == WindowState.Maximized;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch
        {
            // Settings are best-effort; never crash the app over them.
        }
    }
}
