using MuseApp.Core;

namespace MuseApp.Core.Tests;

public class WindowPlacementTests
{
    // Primary 1920x1080 with a 48px taskbar at the bottom.
    private static readonly PixelRect Primary = PixelRect.FromEdges(0, 0, 1920, 1032);

    // A 4K monitor (e.g. 150% scaling) to the LEFT of the primary: negative coordinates.
    private static readonly PixelRect LeftFourK = PixelRect.FromEdges(-3840, -500, 0, 1612);

    // A small portrait monitor to the right.
    private static readonly PixelRect RightPortrait = PixelRect.FromEdges(1920, 0, 2944, 1240);

    private static readonly PixelRect[] ThreeMonitors = [Primary, LeftFourK, RightPortrait];

    [Fact]
    public void RectInsideWorkAreaIsUnchanged()
    {
        var rect = new PixelRect(100, 100, 1280, 860);
        Assert.Equal(rect, WindowPlacement.Clamp(rect, [Primary]));
    }

    [Fact]
    public void RectOnNegativeCoordinateMonitorIsKept()
    {
        var rect = new PixelRect(-3000, -200, 1920, 1290);
        Assert.Equal(rect, WindowPlacement.Clamp(rect, ThreeMonitors));
    }

    [Fact]
    public void FullyOffScreenRectMovesToNearestMonitor()
    {
        // Saved when the left monitor extended further; now far outside everything on the left.
        var result = WindowPlacement.Clamp(new PixelRect(-5000, 100, 1280, 860), [Primary]);
        Assert.Equal(new PixelRect(0, 100, 1280, 860), result);
    }

    [Fact]
    public void FullyOffScreenBelowGoesToNearestOfSeveral()
    {
        var result = WindowPlacement.Clamp(new PixelRect(2000, 5000, 800, 600), ThreeMonitors);
        Assert.Equal(new PixelRect(2000, 640, 800, 600), result);
    }

    [Fact]
    public void MonitorRemovedFallsBackToRemainingMonitor()
    {
        // Window was on the left 4K monitor which is now unplugged.
        var result = WindowPlacement.Clamp(new PixelRect(-3000, -200, 1920, 1290), [Primary]);
        Assert.Equal(new PixelRect(0, 0, 1920, 1032), result);
    }

    [Fact]
    public void PartiallyOffScreenIsPulledInside()
    {
        var result = WindowPlacement.Clamp(new PixelRect(1500, 900, 1280, 860), [Primary]);
        Assert.Equal(new PixelRect(640, 172, 1280, 860), result);
    }

    [Fact]
    public void OversizedRectShrinksToWorkArea()
    {
        var result = WindowPlacement.Clamp(new PixelRect(-100, -100, 4000, 3000), [Primary]);
        Assert.Equal(Primary, result);
    }

    [Fact]
    public void SpanningRectSnapsToMonitorWithMostOverlap()
    {
        // Mostly on the right portrait monitor, a bit over the primary.
        var result = WindowPlacement.Clamp(new PixelRect(1800, 100, 900, 700), ThreeMonitors);
        Assert.Equal(new PixelRect(1920, 100, 900, 700), result);
    }

    [Fact]
    public void DpiScaledRectFromHighDpiMonitorFitsSmallerMonitor()
    {
        // 1280x860 DIPs at 150% = 1920x1290 physical px saved on the 4K; now only a 1366x728 laptop.
        var laptop = PixelRect.FromEdges(0, 0, 1366, 728);
        var result = WindowPlacement.Clamp(new PixelRect(-2500, 200, 1920, 1290), [laptop]);
        Assert.Equal(new PixelRect(0, 0, 1366, 728), result);
    }

    [Fact]
    public void ZeroOrNegativeSizeBecomesAtLeastOnePixel()
    {
        var result = WindowPlacement.Clamp(new PixelRect(10, 10, 0, -5), [Primary]);
        Assert.Equal(new PixelRect(10, 10, 1, 1), result);
    }

    [Fact]
    public void NoWorkAreasReturnsInput()
    {
        var rect = new PixelRect(-5000, -5000, 10, 10);
        Assert.Equal(rect, WindowPlacement.Clamp(rect, []));
    }

    [Fact]
    public void PickWorkAreaRequiresAtLeastOneArea() =>
        Assert.Throws<ArgumentException>(() => WindowPlacement.PickWorkArea(new PixelRect(0, 0, 1, 1), []));

    [Fact]
    public void PickWorkAreaPrefersLaterAreaWithMoreOverlap() =>
        Assert.Equal(RightPortrait, WindowPlacement.PickWorkArea(new PixelRect(2000, 10, 100, 100), ThreeMonitors));

    [Fact]
    public void NullArgumentsThrow()
    {
        Assert.Throws<ArgumentNullException>(() => WindowPlacement.Clamp(default, null!));
        Assert.Throws<ArgumentNullException>(() => WindowPlacement.PickWorkArea(default, null!));
    }

    [Theory]
    [InlineData(0, 0, 10, 10, 5, 5, 10, 10, 25)]
    [InlineData(0, 0, 10, 10, 10, 0, 10, 10, 0)]
    [InlineData(0, 0, 10, 10, 20, 20, 5, 5, 0)]
    public void IntersectionArea(int l1, int t1, int w1, int h1, int l2, int t2, int w2, int h2, long expected) =>
        Assert.Equal(expected, new PixelRect(l1, t1, w1, h1).IntersectionArea(new PixelRect(l2, t2, w2, h2)));

    [Theory]
    [InlineData(-100, 0, 20, 10, 8100)]    // center (-90,5): 90px left of the target
    [InlineData(20, 20, 10, 10, 0)]        // center inside the target
    [InlineData(200, 200, 10, 10, 20000)]  // center (205,205): 100px right and 100px below
    public void DistanceSquaredToUsesCenterToNearestEdge(int l, int t, int w, int h, long expected) =>
        Assert.Equal(expected, new PixelRect(l, t, w, h).DistanceSquaredTo(new PixelRect(0, 0, 105, 105)));
}
