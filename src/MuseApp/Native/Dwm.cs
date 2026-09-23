using System.Runtime.InteropServices;

namespace MuseApp.Native;

/// <summary>Desktop Window Manager interop: dark title bar so the chrome matches the app theme.</summary>
internal static partial class Dwm
{
    // Windows 10 20H1+ / Windows 11. Builds 17763-18985 used the undocumented value 19.
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeLegacy = 19;

    public static void SetDarkTitleBar(nint hwnd, bool dark)
    {
        if (hwnd == 0)
            return;
        var value = dark ? 1 : 0;
        if (DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref value, sizeof(int)) != 0)
            _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeLegacy, ref value, sizeof(int));
    }

    [LibraryImport("dwmapi.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
}
