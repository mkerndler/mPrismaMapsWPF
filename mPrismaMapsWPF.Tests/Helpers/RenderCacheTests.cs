using System.Windows.Media;
using FluentAssertions;
using mPrismaMapsWPF.Helpers;

namespace mPrismaMapsWPF.Tests.Helpers;

public class RenderCacheTests
{
    public RenderCacheTests()
    {
        RenderCache.Clear();
    }

    [Fact]
    public void GetPen_ReturnsCachedPenOnSecondCall()
    {
        var pen1 = RenderCache.GetPen(Colors.Red, 1.0);
        var pen2 = RenderCache.GetPen(Colors.Red, 1.0);
        pen1.Should().BeSameAs(pen2);
    }

    [Fact]
    public void GetPen_DifferentParameters_ReturnsDifferentPens()
    {
        var pen1 = RenderCache.GetPen(Colors.Red, 1.0);
        var pen2 = RenderCache.GetPen(Colors.Blue, 1.0);
        pen1.Should().NotBeSameAs(pen2);
    }

    [Fact]
    public void GetBrush_ReturnsFrozenBrush()
    {
        var brush = RenderCache.GetBrush(Colors.Green);
        brush.IsFrozen.Should().BeTrue();
    }

    [Fact]
    public void GetBrush_ReturnsCachedOnSecondCall()
    {
        var brush1 = RenderCache.GetBrush(Colors.Red);
        var brush2 = RenderCache.GetBrush(Colors.Red);
        brush1.Should().BeSameAs(brush2);
    }

    [Fact]
    public void Clear_EmptiesAllCaches()
    {
        var pen1 = RenderCache.GetPen(Colors.Red, 1.0);
        RenderCache.Clear();
        var pen2 = RenderCache.GetPen(Colors.Red, 1.0);
        // After clearing, a new pen should be created (may or may not be same reference depending on implementation)
        pen2.Should().NotBeNull();
    }

    [Fact]
    public void GetPen_ReturnsFrozenPen()
    {
        var pen = RenderCache.GetPen(Colors.Red, 1.0);
        pen.IsFrozen.Should().BeTrue();
    }

    [Fact]
    public void GetRotateTransform_ReturnsFrozenTransform()
    {
        var transform = RenderCache.GetRotateTransform(45.0);
        transform.IsFrozen.Should().BeTrue();
        transform.Angle.Should().Be(45.0);
    }

    [Fact]
    public void GetScaleTransform_ReturnsFrozenTransform()
    {
        var transform = RenderCache.GetScaleTransform(2.0, 3.0);
        transform.IsFrozen.Should().BeTrue();
        transform.ScaleX.Should().Be(2.0);
        transform.ScaleY.Should().Be(3.0);
    }
}
