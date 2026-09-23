namespace MuseApp.Core;

/// <summary>A rectangle in physical screen pixels (virtual-screen coordinates, may be negative).</summary>
public readonly record struct PixelRect(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;
    public int Bottom => Top + Height;

    public static PixelRect FromEdges(int left, int top, int right, int bottom) =>
        new(left, top, right - left, bottom - top);

    /// <summary>Area of overlap with <paramref name="other"/> (0 when disjoint).</summary>
    public long IntersectionArea(PixelRect other)
    {
        long w = Math.Min(Right, other.Right) - Math.Max(Left, other.Left);
        long h = Math.Min(Bottom, other.Bottom) - Math.Max(Top, other.Top);
        return w > 0 && h > 0 ? w * h : 0;
    }

    /// <summary>Squared distance from this rect's center to the nearest point of <paramref name="other"/>.</summary>
    public long DistanceSquaredTo(PixelRect other)
    {
        long cx = Left + (Width / 2);
        long cy = Top + (Height / 2);
        long dx = cx < other.Left ? other.Left - cx : cx > other.Right ? cx - other.Right : 0;
        long dy = cy < other.Top ? other.Top - cy : cy > other.Bottom ? cy - other.Bottom : 0;
        return (dx * dx) + (dy * dy);
    }
}

/// <summary>
/// Restoring a saved window rect safely. Monitors get unplugged, rearranged and
/// rescaled between sessions, so a saved rect is only a hint: it is moved onto the
/// monitor it overlaps most (or the nearest one) and shrunk to fit that work area.
/// Everything is in physical pixels so per-monitor DPI does not distort the math.
/// </summary>
public static class WindowPlacement
{
    public static PixelRect Clamp(PixelRect window, IReadOnlyList<PixelRect> workAreas)
    {
        ArgumentNullException.ThrowIfNull(workAreas);
        if (workAreas.Count == 0)
            return window;

        var area = PickWorkArea(window, workAreas);
        var width = Math.Clamp(window.Width, 1, area.Width);
        var height = Math.Clamp(window.Height, 1, area.Height);
        var left = Math.Clamp(window.Left, area.Left, area.Right - width);
        var top = Math.Clamp(window.Top, area.Top, area.Bottom - height);
        return new PixelRect(left, top, width, height);
    }

    /// <summary>The work area overlapping the window most; if none overlap, the nearest one.</summary>
    public static PixelRect PickWorkArea(PixelRect window, IReadOnlyList<PixelRect> workAreas)
    {
        ArgumentNullException.ThrowIfNull(workAreas);
        if (workAreas.Count == 0)
            throw new ArgumentException("At least one work area is required.", nameof(workAreas));

        var best = workAreas[0];
        var bestOverlap = window.IntersectionArea(best);
        foreach (var area in workAreas.Skip(1))
        {
            var overlap = window.IntersectionArea(area);
            if (overlap > bestOverlap)
            {
                best = area;
                bestOverlap = overlap;
            }
        }
        if (bestOverlap > 0)
            return best;

        return workAreas.MinBy(window.DistanceSquaredTo);
    }
}
