using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MuseApp.Core;

namespace MuseApp.Native;

/// <summary>Monitor enumeration in physical pixels (the process is per-monitor-v2 DPI aware).</summary>
internal static unsafe partial class Monitors
{
    /// <summary>Work areas (monitor bounds minus taskbars) of every attached monitor.</summary>
    public static IReadOnlyList<PixelRect> GetWorkAreas()
    {
        var areas = new List<PixelRect>();
        var handle = GCHandle.Alloc(areas);
        try
        {
            _ = EnumDisplayMonitors(0, null, &OnMonitor, GCHandle.ToIntPtr(handle));
        }
        finally
        {
            handle.Free();
        }
        return areas;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int OnMonitor(nint monitor, nint hdc, Rect* bounds, nint state)
    {
        var info = new MonitorInfo { Size = sizeof(MonitorInfo) };
        if (GetMonitorInfoW(monitor, ref info) != 0 &&
            GCHandle.FromIntPtr(state).Target is List<PixelRect> areas)
        {
            areas.Add(info.Work.ToPixelRect());
        }
        return 1; // continue enumeration
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
    }

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int EnumDisplayMonitors(
        nint hdc, Rect* clip, delegate* unmanaged[Stdcall]<nint, nint, Rect*, nint, int> callback, nint state);

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int GetMonitorInfoW(nint monitor, ref MonitorInfo info);
}

/// <summary>Win32 RECT.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct Rect
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;

    public readonly PixelRect ToPixelRect() => PixelRect.FromEdges(Left, Top, Right, Bottom);
}
