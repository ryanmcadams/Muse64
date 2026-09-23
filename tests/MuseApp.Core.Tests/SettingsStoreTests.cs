using MuseApp.Core;

namespace MuseApp.Core.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MuseTests", Guid.NewGuid().ToString("N"));

    private string NewPath => Path.Combine(_root, "Local", "Muse", "settings.json");
    private string LegacyPath => Path.Combine(_root, "Roaming", "Muse", "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void MissingFileGivesDefaults()
    {
        var settings = new SettingsStore(NewPath, LegacyPath).Load();
        Assert.Null(settings.WindowRect);
        Assert.False(settings.Maximized);
        Assert.Equal(1.0, settings.ZoomFactor);
    }

    [Fact]
    public void RoundTripPreservesAllValues()
    {
        var store = new SettingsStore(NewPath);
        var saved = new AppSettings
        {
            WindowRect = new PixelRect(-1800, 40, 1600, 1000),
            Maximized = true,
            ZoomFactor = 1.25,
            CloseToTray = false,
            MinimizeToTray = true,
            ShownTrayHint = true,
        };

        Assert.True(store.Save(saved));
        var loaded = store.Load();

        Assert.Equal(new PixelRect(-1800, 40, 1600, 1000), loaded.WindowRect);
        Assert.True(loaded.Maximized);
        Assert.Equal(1.25, loaded.ZoomFactor);
        Assert.False(loaded.CloseToTray);
        Assert.True(loaded.MinimizeToTray);
        Assert.True(loaded.ShownTrayHint);
        Assert.False(File.Exists(NewPath + ".tmp"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Win+F9")]
    public void HotkeyRoundTrips(string? hotkey)
    {
        var store = new SettingsStore(NewPath);
        Assert.True(store.Save(new AppSettings { Hotkey = hotkey }));
        Assert.Equal(hotkey, store.Load().Hotkey);
    }

    [Fact]
    public void MissingHotkeyKeyGetsDefault()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(NewPath)!);
        File.WriteAllText(NewPath, """{ "ZoomFactor": 1.1 }""");
        Assert.Equal(HotkeyGesture.DefaultText, new SettingsStore(NewPath).Load().Hotkey);
    }

    [Fact]
    public void SaveOverwritesExistingFileAtomically()
    {
        var store = new SettingsStore(NewPath);
        store.Save(new AppSettings { ZoomFactor = 2 });
        store.Save(new AppSettings { ZoomFactor = 3 });
        Assert.Equal(3, store.Load().ZoomFactor);
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(NewPath)!));
    }

    [Fact]
    public void SaveFailureReturnsFalse()
    {
        // The target path is an existing directory, so the final move cannot succeed.
        Directory.CreateDirectory(NewPath);
        Assert.False(new SettingsStore(NewPath).Save(new AppSettings()));
        Assert.False(File.Exists(NewPath + ".tmp"));
    }

    [Fact]
    public void SaveRejectsNull() =>
        Assert.Throws<ArgumentNullException>(() => new SettingsStore(NewPath).Save(null!));

    [Theory]
    [InlineData("{ this is not json")]
    [InlineData("")]
    [InlineData("[1,2,3]")]
    [InlineData("{\"ZoomFactor\": \"big\"}")]
    public void CorruptJsonFallsBackToDefaults(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(NewPath)!);
        File.WriteAllText(NewPath, content);

        var settings = new SettingsStore(NewPath).Load();

        Assert.Null(settings.WindowRect);
        Assert.Equal(1.0, settings.ZoomFactor);
    }

    [Fact]
    public void JsonNullFallsBackToDefaults()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(NewPath)!);
        File.WriteAllText(NewPath, "null");
        Assert.Equal(1.0, new SettingsStore(NewPath).Load().ZoomFactor);
    }

    [Fact]
    public void OutOfRangeValuesAreSanitized()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(NewPath)!);
        File.WriteAllText(NewPath, """{ "Left": 10, "Top": "NaN", "Width": 800, "Height": 600, "ZoomFactor": 99 }""");

        var settings = new SettingsStore(NewPath).Load();

        Assert.Null(settings.WindowRect);
        Assert.Null(settings.Left);
        Assert.Equal(ZoomLevels.Max, settings.ZoomFactor);
    }

    [Fact]
    public void MigratesLegacyAppDataFileOnceAndDeletesIt()
    {
        // Exact shape written by v2.0 (WindowSettings in %APPDATA%\Muse).
        Directory.CreateDirectory(Path.GetDirectoryName(LegacyPath)!);
        File.WriteAllText(LegacyPath, """
            {
              "Left": 200,
              "Top": 120,
              "Width": 1280,
              "Height": 860,
              "Maximized": true
            }
            """);

        var store = new SettingsStore(NewPath, LegacyPath);
        var settings = store.Load();

        Assert.Equal(new PixelRect(200, 120, 1280, 860), settings.WindowRect);
        Assert.True(settings.Maximized);
        Assert.Equal(1.0, settings.ZoomFactor);
        Assert.True(settings.CloseToTray); // v2.0 file has no tray keys: defaults apply
        Assert.True(File.Exists(NewPath));
        Assert.False(File.Exists(LegacyPath));
        Assert.False(Directory.Exists(Path.GetDirectoryName(LegacyPath)));

        // Second load reads the new file.
        Assert.Equal(new PixelRect(200, 120, 1280, 860), new SettingsStore(NewPath, LegacyPath).Load().WindowRect);
    }

    [Fact]
    public void MigrationKeepsLegacyDirectoryWhenItHasOtherFiles()
    {
        var legacyDir = Path.GetDirectoryName(LegacyPath)!;
        Directory.CreateDirectory(legacyDir);
        File.WriteAllText(LegacyPath, """{ "Width": 1000, "Height": 700 }""");
        File.WriteAllText(Path.Combine(legacyDir, "other.txt"), "keep me");

        new SettingsStore(NewPath, LegacyPath).Load();

        Assert.False(File.Exists(LegacyPath));
        Assert.True(File.Exists(Path.Combine(legacyDir, "other.txt")));
    }

    [Fact]
    public void CorruptLegacyFileMigratesAsDefaults()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LegacyPath)!);
        File.WriteAllText(LegacyPath, "garbage");

        var settings = new SettingsStore(NewPath, LegacyPath).Load();

        Assert.Null(settings.WindowRect);
        Assert.True(File.Exists(NewPath));
        Assert.False(File.Exists(LegacyPath));
    }

    [Fact]
    public void LegacyFileIsKeptWhenNewFileCannotBeWritten()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LegacyPath)!);
        File.WriteAllText(LegacyPath, """{ "Left": 1, "Top": 2, "Width": 800, "Height": 600 }""");
        Directory.CreateDirectory(NewPath); // blocks the save

        var settings = new SettingsStore(NewPath, LegacyPath).Load();

        Assert.Equal(new PixelRect(1, 2, 800, 600), settings.WindowRect);
        Assert.True(File.Exists(LegacyPath));
    }

    [Fact]
    public void StaleLegacyFileIsDeletedWhenNewFileExists()
    {
        var store = new SettingsStore(NewPath, LegacyPath);
        store.Save(new AppSettings { ZoomFactor = 1.5 });
        Directory.CreateDirectory(Path.GetDirectoryName(LegacyPath)!);
        File.WriteAllText(LegacyPath, """{ "Left": 5, "Top": 5, "Width": 800, "Height": 600 }""");

        var settings = store.Load();

        Assert.Equal(1.5, settings.ZoomFactor);
        Assert.Null(settings.WindowRect);
        Assert.False(File.Exists(LegacyPath));
    }

    [Fact]
    public void DeleteRemovesSettingsFile()
    {
        var store = new SettingsStore(NewPath);
        store.Save(new AppSettings());
        store.Delete();
        Assert.False(File.Exists(NewPath));
        store.Delete(); // idempotent
    }

    [Theory]
    [InlineData(10.0, 10.0, 0.0, 600.0)]
    [InlineData(10.0, 10.0, 800.0, double.PositiveInfinity)]
    [InlineData(3e9, 10.0, 800.0, 600.0)]
    [InlineData(10.0, 10.0, 3e9, 600.0)]
    public void InvalidBoundsGiveNoWindowRect(double left, double top, double width, double height)
    {
        var settings = new AppSettings { Left = left, Top = top, Width = width, Height = height };
        Assert.Null(settings.WindowRect);
    }

    [Fact]
    public void FractionalBoundsAreRounded()
    {
        var settings = new AppSettings { Left = -10.6, Top = 0.4, Width = 800.5, Height = 600.49 };
        Assert.Equal(new PixelRect(-11, 0, 800, 600), settings.WindowRect);
    }

    [Fact]
    public void ClearingWindowRectClearsAllBounds()
    {
        var settings = new AppSettings { WindowRect = new PixelRect(1, 2, 3, 4) };
        settings.WindowRect = null;
        Assert.Null(settings.Left);
        Assert.Null(settings.Height);
    }
}
