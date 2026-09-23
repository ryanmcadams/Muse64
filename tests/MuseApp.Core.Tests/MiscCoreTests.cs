using MuseApp.Core;

namespace MuseApp.Core.Tests;

internal sealed class FakeTime(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = start;

    public override DateTimeOffset GetUtcNow() => Now;
}

public class ZoomLevelsTests
{
    [Theory]
    [InlineData(1.0, 1.1)]
    [InlineData(1.1000001, 1.25)]
    [InlineData(0.25, 0.33)]
    [InlineData(1.05, 1.1)]
    [InlineData(4.5, 5.0)]
    [InlineData(5.0, 5.0)]
    [InlineData(double.NaN, 1.1)]
    public void ZoomIn(double current, double expected) => Assert.Equal(expected, ZoomLevels.ZoomIn(current));

    [Theory]
    [InlineData(1.0, 0.9)]
    [InlineData(1.25, 1.1)]
    [InlineData(1.05, 1.0)]
    [InlineData(0.33, 0.25)]
    [InlineData(0.25, 0.25)]
    [InlineData(0.1, 0.25)]
    public void ZoomOut(double current, double expected) => Assert.Equal(expected, ZoomLevels.ZoomOut(current));

    [Theory]
    [InlineData(0.01, 0.25)]
    [InlineData(9.0, 5.0)]
    [InlineData(1.5, 1.5)]
    [InlineData(double.NaN, 1.0)]
    [InlineData(double.PositiveInfinity, 1.0)]
    public void Normalize(double value, double expected) => Assert.Equal(expected, ZoomLevels.Normalize(value));
}

public class RestartBudgetTests
{
    [Fact]
    public void AllowsUpToMaxWithinWindowThenRefuses()
    {
        var time = new FakeTime(DateTimeOffset.UnixEpoch);
        var budget = new RestartBudget(3, TimeSpan.FromMinutes(1), time);

        Assert.True(budget.TryConsume());
        time.Now += TimeSpan.FromSeconds(10);
        Assert.True(budget.TryConsume());
        time.Now += TimeSpan.FromSeconds(10);
        Assert.True(budget.TryConsume());
        Assert.False(budget.TryConsume());
    }

    [Fact]
    public void OldAttemptsExpire()
    {
        var time = new FakeTime(DateTimeOffset.UnixEpoch);
        var budget = new RestartBudget(3, TimeSpan.FromMinutes(1), time);
        for (var i = 0; i < 3; i++)
            budget.TryConsume();

        time.Now += TimeSpan.FromMinutes(1);
        Assert.True(budget.TryConsume());
    }

    [Fact]
    public void ResetClearsHistory()
    {
        var budget = new RestartBudget(1, TimeSpan.FromHours(1));
        Assert.True(budget.TryConsume());
        Assert.False(budget.TryConsume());
        budget.Reset();
        Assert.True(budget.TryConsume());
    }
}

public sealed class AppLogTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "MuseTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void WritesTimestampedLevelLines()
    {
        var log = new AppLog(_dir, time: new FakeTime(new DateTimeOffset(2026, 9, 23, 10, 0, 0, TimeSpan.Zero)));
        log.Info("hello");
        log.Warn("careful");
        log.Error("boom", new InvalidOperationException("detail"));

        var text = File.ReadAllText(log.FilePath);
        Assert.Contains("2026-09-23T10:00:00.000Z [INFO] hello", text, StringComparison.Ordinal);
        Assert.Contains("[WARN] careful", text, StringComparison.Ordinal);
        Assert.Contains("[ERROR] boom", text, StringComparison.Ordinal);
        Assert.Contains("InvalidOperationException: detail", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ErrorWithoutExceptionWritesMessageOnly()
    {
        var log = new AppLog(_dir);
        log.Error("just text");
        Assert.EndsWith("[ERROR] just text" + Environment.NewLine, File.ReadAllText(log.FilePath), StringComparison.Ordinal);
    }

    [Fact]
    public void RollsToSingleBackupWhenFull()
    {
        var log = new AppLog(_dir, maxBytes: 200);
        for (var i = 0; i < 20; i++)
            log.Info($"line {i:D2} padding padding padding");

        Assert.True(File.Exists(log.FilePath));
        Assert.True(File.Exists(log.PreviousFilePath));
        Assert.Equal(2, Directory.GetFiles(_dir).Length);
        Assert.True(new FileInfo(log.FilePath).Length <= 200);
        Assert.Contains("line 19", File.ReadAllText(log.FilePath), StringComparison.Ordinal);
    }

    [Fact]
    public void UnwritableDirectoryDoesNotThrow()
    {
        Directory.CreateDirectory(_dir);
        var blocker = Path.Combine(_dir, "file");
        File.WriteAllText(blocker, "x");

        // A file where the log directory should be: every write fails, silently.
        var log = new AppLog(blocker);
        log.Info("ignored");
        Assert.False(File.Exists(log.FilePath));
    }
}

public class WindowTitleTests
{
    [Theory]
    [InlineData(null, "Muse")]
    [InlineData("", "Muse")]
    [InlineData("   ", "Muse")]
    [InlineData("Muse", "Muse")]
    [InlineData("Muse — Your Personal AI Agent", "Muse — Your Personal AI Agent")]
    [InlineData("Log in", "Log in — Muse")]
    [InlineData("  Trip ideas ", "Trip ideas — Muse")]
    public void Compose(string? documentTitle, string expected) =>
        Assert.Equal(expected, WindowTitle.Compose(documentTitle));

    [Theory]
    [InlineData("Log in", "https://auth.muse.ai/aymh/?token=x", "auth.muse.ai — Log in")]
    [InlineData("", "https://accounts.google.com/o/oauth2", "accounts.google.com")]
    [InlineData("Sign in", "about:blank", "about:blank — Sign in")]
    [InlineData(null, "about:blank", "about:blank")]
    [InlineData("Muse", null, "Muse")]
    [InlineData(null, null, "Muse")]
    public void ComposePopup(string? documentTitle, string? source, string expected) =>
        Assert.Equal(expected, WindowTitle.ComposePopup(documentTitle, source));

    [Fact]
    public void ComposePopupHostCannotBePushedOutByPaddedTitle()
    {
        // A hostile page pads its title so an end-truncated title bar would show only the spoof.
        var spoof = "Log in — auth.muse.ai" + new string(' ', 300);
        var title = WindowTitle.ComposePopup(spoof, "https://evil.example/login?x=1");
        Assert.StartsWith("evil.example — ", title, StringComparison.Ordinal);
        Assert.DoesNotContain("?x=1", title, StringComparison.Ordinal);
    }

    [Fact]
    public void ComposePopupShowsLookAlikeHostsInPunycode()
    {
        // Cyrillic "ѕ" (U+0455) in place of the Latin s.
        var title = WindowTitle.ComposePopup("Muse", "https://muѕe.ai/");
        Assert.StartsWith("xn--", title, StringComparison.Ordinal);
    }
}

public class CommandLineOptionsTests
{
    [Theory]
    [InlineData(new string[0], false, false)]
    [InlineData(new[] { "--minimized" }, true, false)]
    [InlineData(new[] { "--tray" }, true, false)]
    [InlineData(new[] { "--MINIMIZED" }, true, false)]
    [InlineData(new[] { "--reset" }, false, true)]
    [InlineData(new[] { " --reset ", "--tray" }, true, true)]
    [InlineData(new[] { "--unknown", "minimized", "-reset" }, false, false)]
    public void Parse(string[] args, bool minimized, bool reset) =>
        Assert.Equal(new CommandLineOptions(minimized, reset), CommandLineOptions.Parse(args));

    [Fact]
    public void ParseRejectsNull() => Assert.Throws<ArgumentNullException>(() => CommandLineOptions.Parse(null!));
}

public class StartupEntryTests
{
    private const string ExePath = @"C:\Program Files\Muse\MuseApp.exe";
    private const string Quote = @""""; // verbatim: a single double-quote character
    private const string Command = Quote + ExePath + Quote + " --minimized";

    [Fact]
    public void BuildCommandQuotesPathAndStartsMinimized() =>
        Assert.Equal(Command, StartupEntry.BuildCommand(ExePath));

    [Fact]
    public void BuildCommandRoundTripsThroughParse()
    {
        var command = StartupEntry.BuildCommand(ExePath);
        var args = command[(command.LastIndexOf('"') + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(CommandLineOptions.Parse(args).StartMinimized);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(@"C:\a""b.exe")]
    public void BuildCommandRejectsBadPaths(string path) =>
        Assert.ThrowsAny<ArgumentException>(() => StartupEntry.BuildCommand(path));

    [Fact]
    public void BuildCommandRejectsNull() =>
        Assert.Throws<ArgumentNullException>(() => StartupEntry.BuildCommand(null!));

    [Theory]
    [InlineData(null, null, false)]
    [InlineData("", null, false)]
    [InlineData(Command, null, true)]
    [InlineData(Command, new byte[0], true)]
    [InlineData(Command, new byte[] { 2, 0, 0, 0 }, true)]
    [InlineData(Command, new byte[] { 6, 0, 0, 0 }, true)]
    [InlineData(Command, new byte[] { 3, 0, 0, 0 }, false)]
    [InlineData(Command, new byte[] { 7, 0, 0, 0 }, false)]
    [InlineData(null, new byte[] { 2, 0, 0, 0 }, false)]
    public void IsEnabled(string? runValue, byte[]? approved, bool expected) =>
        Assert.Equal(expected, StartupEntry.IsEnabled(runValue, approved));
}

public class TraySettingsTests
{
    [Fact]
    public void TrayDefaults()
    {
        var settings = new AppSettings();
        Assert.True(settings.CloseToTray);
        Assert.False(settings.MinimizeToTray);
        Assert.False(settings.ShownTrayHint);
    }
}
