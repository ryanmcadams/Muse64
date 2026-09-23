namespace MuseApp.Core;

/// <summary>
/// Command-line switches: <c>--minimized</c> / <c>--tray</c> start hidden in the tray
/// (used by the Start-with-Windows entry); <c>--reset</c> wipes the browser profile and
/// settings before starting. Unknown arguments are ignored.
/// </summary>
public sealed record CommandLineOptions(bool StartMinimized, bool Reset)
{
    public const string MinimizedSwitch = "--minimized";
    public const string TraySwitch = "--tray";
    public const string ResetSwitch = "--reset";

    public static CommandLineOptions Parse(IEnumerable<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        bool minimized = false, reset = false;
        foreach (var arg in args)
        {
            var value = arg.Trim();
            if (value.Equals(MinimizedSwitch, StringComparison.OrdinalIgnoreCase) ||
                value.Equals(TraySwitch, StringComparison.OrdinalIgnoreCase))
                minimized = true;
            else if (value.Equals(ResetSwitch, StringComparison.OrdinalIgnoreCase))
                reset = true;
        }
        return new CommandLineOptions(minimized, reset);
    }
}
