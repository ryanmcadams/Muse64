namespace MuseApp.Core;

/// <summary>
/// The per-user "Start with Windows" entry: a value under HKCU\...\Run, which Task
/// Manager's Startup page can disable through a matching StartupApproved value.
/// </summary>
public static class StartupEntry
{
    public const string ValueName = "Muse";
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string ApprovedKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    /// <summary>Run-key command: the quoted exe path plus <c>--minimized</c> so login starts in the tray.</summary>
    public static string BuildCommand(string exePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exePath);
        if (exePath.Contains('"', StringComparison.Ordinal))
            throw new ArgumentException("Executable path cannot contain quotes.", nameof(exePath));
        return $"\"{exePath}\" {CommandLineOptions.MinimizedSwitch}";
    }

    /// <summary>
    /// Whether Windows will actually run the entry. Task Manager "Disable" writes a
    /// StartupApproved value whose first byte is odd (0x03/0x07); absent or even means enabled.
    /// </summary>
    public static bool IsEnabled(string? runValue, byte[]? approvedValue) =>
        !string.IsNullOrWhiteSpace(runValue) &&
        (approvedValue is not { Length: > 0 } || (approvedValue[0] & 1) == 0);
}
