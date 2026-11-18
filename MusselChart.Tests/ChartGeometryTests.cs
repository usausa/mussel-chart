namespace MusselChart;

using MusselChart.Internal;

public sealed class ChartGeometryTests
{
    [Fact]
    public void FullCircleIsDrawnWithTwoArcs()
    {
        var path = ChartGeometry.Arc(0, 0, 10, 0, 360);

        Assert.Equal("M0 -10A10 10 0 1 1 0 10A10 10 0 1 1 0 -10Z", path);
    }

    [Fact]
    public void ArcThatAlmostClosesIsDrawnAsACircle()
    {
        var path = ChartGeometry.Arc(0, 0, 10, 0, 359.9999);

        Assert.Equal("M0 -10A10 10 0 1 1 0 10A10 10 0 1 1 0 -10Z", path);
    }

    [Fact]
    public void SliceThatAlmostClosesIsDrawnAsARing()
    {
        var path = ChartGeometry.Slice(0, 0, 10, 5, 0, 359.9999, 0, 0);

        Assert.Equal(ChartGeometry.Slice(0, 0, 10, 5, 0, 360, 0, 0), path);
    }

    [Fact]
    public void ArcGoesClockwiseFromTwelveOClock()
    {
        var path = ChartGeometry.Arc(0, 0, 10, 0, 90);

        Assert.Equal("M0 -10A10 10 0 0 1 10 0", path);
    }

    [Fact]
    public void WholeRingHasAHoleOfItsOwn()
    {
        var path = ChartGeometry.Slice(0, 0, 10, 5, 0, 360, 0, 0);

        Assert.Equal(2, path.Count(static x => x == 'M'));
    }

    [Fact]
    public void SliceOfADonutMovesItsEdgesByTheGap()
    {
        var path = ChartGeometry.Slice(0, 0, 100, 50, 0, 90, 1, 1);

        Assert.StartsWith("M1 -99.99", path, StringComparison.Ordinal);
        Assert.Contains("L49.99 -1", path, StringComparison.Ordinal);
        Assert.EndsWith("Z", path, StringComparison.Ordinal);
    }

    [Fact]
    public void SliceOfAPieMeetsAtTheMovedEdges()
    {
        var path = ChartGeometry.Slice(0, 0, 100, 0, 0, 90, 1, 1);

        Assert.EndsWith("L1 -1Z", path, StringComparison.Ordinal);
    }

    [Fact]
    public void SliceSmallerThanItsGapsIsNotDrawn()
    {
        Assert.Empty(ChartGeometry.Slice(0, 0, 10, 5, 0, 1, 2, 2));
    }

    [Fact]
    public void UnitBoundsCoverTheSweptSector()
    {
        var (left, top, right, bottom) = ChartGeometry.GetUnitBounds(270, 180, 0.5);

        Assert.Equal(-1, left, 6);
        Assert.Equal(-1, top, 6);
        Assert.Equal(1, right, 6);
        Assert.Equal(0, bottom, 6);
    }
}
