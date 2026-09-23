using System.Globalization;
using System.Runtime.InteropServices;
using MuseApp.Core;

namespace MuseApp.Native;

/// <summary>System-wide hotkeys delivered to a window as WM_HOTKEY.</summary>
internal static partial class Hotkeys
{
    public const int WmHotkey = 0x0312;
    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModShift = 0x0004;
    public const uint ModWin = 0x0008;
    public const uint ModNoRepeat = 0x4000;

    private const uint VirtualKeyF1 = 0x70;

    /// <summary>RegisterHotKey modifier flags for a parsed gesture (always with MOD_NOREPEAT).</summary>
    public static uint ToModifiers(HotkeyModifiers modifiers) =>
        ModNoRepeat |
        (modifiers.HasFlag(HotkeyModifiers.Alt) ? ModAlt : 0) |
        (modifiers.HasFlag(HotkeyModifiers.Control) ? ModControl : 0) |
        (modifiers.HasFlag(HotkeyModifiers.Shift) ? ModShift : 0) |
        (modifiers.HasFlag(HotkeyModifiers.Win) ? ModWin : 0);

    /// <summary>
    /// Virtual-key code for a parsed key: VK_A–VK_Z and VK_0–VK_9 equal their ASCII
    /// codes; F1–F24 are VK_F1 (0x70) onwards.
    /// </summary>
    public static uint ToVirtualKey(string key) =>
        key.Length == 1
            ? key[0]
            : VirtualKeyF1 + uint.Parse(key.AsSpan(1), provider: CultureInfo.InvariantCulture) - 1;

    /// <summary>Registers the hotkey; returns the Win32 error code (0 on success).</summary>
    public static int Register(nint hwnd, int id, uint modifiers, uint virtualKey) =>
        RegisterHotKey(hwnd, id, modifiers, virtualKey) != 0 ? 0 : Marshal.GetLastPInvokeError();

    public static void Unregister(nint hwnd, int id) => _ = UnregisterHotKey(hwnd, id);

    [LibraryImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int RegisterHotKey(nint hwnd, int id, uint modifiers, uint virtualKey);

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int UnregisterHotKey(nint hwnd, int id);
}
