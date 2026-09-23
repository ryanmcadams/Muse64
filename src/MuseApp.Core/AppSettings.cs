using System.Text.Json;
using System.Text.Json.Serialization;

namespace MuseApp.Core;

/// <summary>
/// Persisted user state. The flat Left/Top/Width/Height/Maximized shape is kept from v2.0
/// so the old %APPDATA% file deserializes as-is during migration.
/// Window bounds are physical pixels from v2.1 on (v2.0 stored DIPs; clamping absorbs the difference).
/// </summary>
public sealed class AppSettings
{
    public const double DefaultZoom = 1.0;

    public double? Left { get; set; }
    public double? Top { get; set; }
    public double? Width { get; set; }
    public double? Height { get; set; }
    public bool Maximized { get; set; }
    public double ZoomFactor { get; set; } = DefaultZoom;

    /// <summary>Closing the window (X, Alt+F4) hides it to the tray instead of quitting.</summary>
    public bool CloseToTray { get; set; } = true;

    /// <summary>Minimizing hides the window to the tray instead of the taskbar.</summary>
    public bool MinimizeToTray { get; set; }

    /// <summary>
    /// Global show/hide hotkey, e.g. "Ctrl+Alt+M" (see <see cref="HotkeyGesture.TryParse"/>).
    /// Null or empty turns it off; an invalid value falls back to the default.
    /// </summary>
    public string? Hotkey { get; set; } = HotkeyGesture.DefaultText;

    /// <summary>The one-time "still running in the tray" balloon has been shown.</summary>
    public bool ShownTrayHint { get; set; }

    /// <summary>Saved normal-state window rect, or null if none/invalid.</summary>
    [JsonIgnore]
    public PixelRect? WindowRect
    {
        get
        {
            if (Left is not { } l || Top is not { } t || Width is not { } w || Height is not { } h)
                return null;
            if (!double.IsFinite(l) || !double.IsFinite(t) || !double.IsFinite(w) || !double.IsFinite(h) ||
                w < 1 || h < 1 || Math.Abs(l) > int.MaxValue / 2 || Math.Abs(t) > int.MaxValue / 2 ||
                w > int.MaxValue / 2 || h > int.MaxValue / 2)
                return null;
            return new PixelRect((int)Math.Round(l), (int)Math.Round(t), (int)Math.Round(w), (int)Math.Round(h));
        }
        set
        {
            Left = value?.Left;
            Top = value?.Top;
            Width = value?.Width;
            Height = value?.Height;
        }
    }

    /// <summary>Repairs values a hand-edited or damaged file could contain.</summary>
    internal void Sanitize()
    {
        ZoomFactor = ZoomLevels.Normalize(ZoomFactor);
        if (WindowRect is null)
            Left = Top = Width = Height = null; // partial or out-of-range bounds are useless
    }
}

/// <summary>
/// Loads and saves <see cref="AppSettings"/>. Writes are atomic (temp file + move) so a
/// crash mid-write cannot leave a truncated file; unreadable files fall back to defaults.
/// </summary>
public sealed class SettingsStore(string path, string? legacyPath = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    public string Path { get; } = path;

    public AppSettings Load()
    {
        if (!File.Exists(Path) && legacyPath is not null && File.Exists(legacyPath))
            return MigrateLegacy(legacyPath);

        // A leftover legacy file (earlier delete failed) is stale once the new file exists.
        if (legacyPath is not null)
            TryDelete(legacyPath);

        return TryRead(Path) ?? new AppSettings();
    }

    /// <summary>Best-effort atomic save; returns false instead of throwing.</summary>
    public bool Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var temp = Path + ".tmp";
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path))!);
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, JsonOptions));
            File.Move(temp, Path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDelete(temp);
            return false;
        }
    }

    /// <summary>Deletes the settings file (used by --reset).</summary>
    public void Delete() => TryDelete(Path);

    private AppSettings MigrateLegacy(string legacy)
    {
        var settings = TryRead(legacy) ?? new AppSettings();
        // Only drop the old file once its contents are safely in the new location.
        if (Save(settings))
        {
            TryDelete(legacy);
            TryDeleteEmptyDirectory(System.IO.Path.GetDirectoryName(legacy));
        }
        return settings;
    }

    private static AppSettings? TryRead(string file)
    {
        try
        {
            if (!File.Exists(file))
                return null;
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(file), JsonOptions);
            settings?.Sanitize();
            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort cleanup.
        }
    }

    private static void TryDeleteEmptyDirectory(string? directory)
    {
        try
        {
            if (directory is not null && Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                Directory.Delete(directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort cleanup.
        }
    }
}
