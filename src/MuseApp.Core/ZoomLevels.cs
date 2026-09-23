namespace MuseApp.Core;

/// <summary>Browser-style zoom steps (same ladder as Edge/Chrome) for the keyboard shortcuts.</summary>
public static class ZoomLevels
{
    public const double Min = 0.25;
    public const double Max = 5.0;

    private static readonly double[] Steps =
        [0.25, 0.33, 0.5, 0.67, 0.75, 0.8, 0.9, 1.0, 1.1, 1.25, 1.5, 1.75, 2.0, 2.5, 3.0, 4.0, 5.0];

    // Tolerance so a stored 1.1000000001 still counts as the 1.1 step.
    private const double Epsilon = 0.001;

    public static double ZoomIn(double current)
    {
        current = Normalize(current);
        foreach (var step in Steps)
        {
            if (step > current + Epsilon)
                return step;
        }
        return Max;
    }

    public static double ZoomOut(double current)
    {
        current = Normalize(current);
        for (var i = Steps.Length - 1; i >= 0; i--)
        {
            if (Steps[i] < current - Epsilon)
                return Steps[i];
        }
        return Min;
    }

    /// <summary>Clamps to the supported range; NaN/infinite becomes 100%.</summary>
    public static double Normalize(double zoom) =>
        double.IsFinite(zoom) ? Math.Clamp(zoom, Min, Max) : AppSettings.DefaultZoom;
}
