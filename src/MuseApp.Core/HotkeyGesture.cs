namespace MuseApp.Core;

/// <summary>Modifier keys of a global hotkey.</summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Control = 1,
    Alt = 2,
    Shift = 4,
    Win = 8,
}

/// <summary>Outcome of parsing the <c>Hotkey</c> setting.</summary>
public enum HotkeyParseStatus
{
    Valid,

    /// <summary>Null, empty or whitespace: the user turned the hotkey off.</summary>
    Disabled,

    Invalid,
}

/// <summary>
/// A global show/hide hotkey such as "Ctrl+Alt+M". <see cref="Key"/> is normalized to
/// upper case: "A"–"Z", "0"–"9" or "F1"–"F24".
/// </summary>
public sealed record HotkeyGesture(HotkeyModifiers Modifiers, string Key)
{
    public const string DefaultText = "Ctrl+Alt+M";

    public static HotkeyGesture Default { get; } = new(HotkeyModifiers.Control | HotkeyModifiers.Alt, "M");

    /// <summary>
    /// Parses "Ctrl+Alt+M", "win shift f9", ... Modifiers are Ctrl/Control, Alt, Shift and Win,
    /// case-insensitive, separated by '+' or spaces, each at most once, plus exactly one key.
    /// At least one of Ctrl, Alt or Win is required: a bare key or Shift+key would be taken
    /// away from every other app for ordinary typing.
    /// </summary>
    public static HotkeyParseStatus TryParse(string? text, out HotkeyGesture? gesture)
    {
        gesture = null;
        if (string.IsNullOrWhiteSpace(text))
            return HotkeyParseStatus.Disabled;

        var modifiers = HotkeyModifiers.None;
        string? key = null;
        foreach (var token in text.Split(['+', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var modifier = ParseModifier(token);
            if (modifier != HotkeyModifiers.None)
            {
                if (modifiers.HasFlag(modifier))
                    return HotkeyParseStatus.Invalid; // "Ctrl+Ctrl+M"
                modifiers |= modifier;
            }
            else if (key is null && NormalizeKey(token) is { } normalized)
            {
                key = normalized;
            }
            else
            {
                return HotkeyParseStatus.Invalid; // unknown token or a second key
            }
        }

        if (key is null || (modifiers & (HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Win)) == 0)
            return HotkeyParseStatus.Invalid;

        gesture = new HotkeyGesture(modifiers, key);
        return HotkeyParseStatus.Valid;
    }

    /// <summary>Canonical text, e.g. "Ctrl+Shift+F9".</summary>
    public override string ToString()
    {
        var parts = new List<string>(5);
        if (Modifiers.HasFlag(HotkeyModifiers.Control))
            parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt))
            parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift))
            parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Win))
            parts.Add("Win");
        parts.Add(Key);
        return string.Join('+', parts);
    }

    private static HotkeyModifiers ParseModifier(string token) => token.ToUpperInvariant() switch
    {
        "CTRL" or "CONTROL" => HotkeyModifiers.Control,
        "ALT" => HotkeyModifiers.Alt,
        "SHIFT" => HotkeyModifiers.Shift,
        "WIN" => HotkeyModifiers.Win,
        _ => HotkeyModifiers.None,
    };

    private static string? NormalizeKey(string token)
    {
        var upper = token.ToUpperInvariant();
        if (upper.Length == 1 && (upper[0] is >= 'A' and <= 'Z' || upper[0] is >= '0' and <= '9'))
            return upper;
        // F1–F24, no leading zeros or signs ("F01", "F+1" are rejected).
        if (upper.Length is 2 or 3 && upper[0] == 'F' && upper[1] is >= '1' and <= '9' &&
            int.TryParse(upper.AsSpan(1), System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var n) && n is >= 1 and <= 24)
            return upper;
        return null;
    }
}
