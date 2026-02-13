using System.Windows;
using FluentAssertions;
using mPrismaMapsWPF.Drawing;
using mPrismaMapsWPF.Helpers;

namespace mPrismaMapsWPF.Tests.Helpers;

public class SnapHelperTests
{
    [Fact]
    public void SnapToGrid_WhenEnabled_RoundsToNearestGridPoint()
    {
        var settings = new GridSnapSettings { IsEnabled = true, SpacingX = 10, SpacingY = 10 };
        var result = SnapHelper.SnapToGrid(new Point(13, 27), settings);
        result.X.Should().Be(10);
        result.Y.Should().Be(30);
    }

    [Fact]
    public void SnapToGrid_WhenDisabled_ReturnsOriginalPoint()
    {
        var settings = new GridSnapSettings { IsEnabled = false, SpacingX = 10, SpacingY = 10 };
        var result = SnapHelper.SnapToGrid(new Point(13, 27), settings);
        result.X.Should().Be(13);
        result.Y.Should().Be(27);
    }

    [Fact]
    public void SnapToGrid_WithSmallSpacing_SnapsCorrectly()
    {
        var settings = new GridSnapSettings { IsEnabled = true, SpacingX = 0.5, SpacingY = 0.5 };
        var result = SnapHelper.SnapToGrid(new Point(1.3, 2.7), settings);
        result.X.Should().Be(1.5);
        result.Y.Should().Be(2.5);
    }

    [Fact]
    public void SnapToGrid_WithLargeSpacing_SnapsCorrectly()
    {
        var settings = new GridSnapSettings { IsEnabled = true, SpacingX = 100, SpacingY = 100 };
        var result = SnapHelper.SnapToGrid(new Point(130, 270), settings);
        result.X.Should().Be(100);
        result.Y.Should().Be(300);
    }

    [Fact]
    public void SnapToGrid_WithOriginOffset_AffectsSnap()
    {
        var settings = new GridSnapSettings { IsEnabled = true, SpacingX = 10, SpacingY = 10, OriginX = 5, OriginY = 5 };
        var result = SnapHelper.SnapToGrid(new Point(13, 27), settings);
        result.X.Should().Be(15);
        result.Y.Should().Be(25);
    }

    [Fact]
    public void SnapToGrid_TupleOverload_WhenEnabled_Snaps()
    {
        var settings = new GridSnapSettings { IsEnabled = true, SpacingX = 10, SpacingY = 10 };
        var (x, y) = SnapHelper.SnapToGrid(13.0, 27.0, settings);
        x.Should().Be(10);
        y.Should().Be(30);
    }

    [Fact]
    public void SnapToGrid_TupleOverload_WhenDisabled_ReturnsOriginal()
    {
        var settings = new GridSnapSettings { IsEnabled = false };
        var (x, y) = SnapHelper.SnapToGrid(13.0, 27.0, settings);
        x.Should().Be(13);
        y.Should().Be(27);
    }
}
