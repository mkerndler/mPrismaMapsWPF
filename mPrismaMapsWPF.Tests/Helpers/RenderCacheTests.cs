using FluentAssertions;
using mPrismaMapsWPF.Helpers;
using SkiaSharp;

namespace mPrismaMapsWPF.Tests.Helpers;

public class RenderCacheTests
{
    public RenderCacheTests()
    {
        SkiaRenderCache.Clear();
    }

    [Fact]
    public void GetStrokePaint_ReturnsCachedPaintOnSecondCall()
    {
        var paint1 = SkiaRenderCache.GetStrokePaint(SKColors.Red, 1.0f);
        var paint2 = SkiaRenderCache.GetStrokePaint(SKColors.Red, 1.0f);
        paint1.Should().BeSameAs(paint2);
    }

    [Fact]
    public void GetStrokePaint_DifferentColor_ReturnsDifferentPaint()
    {
        var paint1 = SkiaRenderCache.GetStrokePaint(SKColors.Red, 1.0f);
        var paint2 = SkiaRenderCache.GetStrokePaint(SKColors.Blue, 1.0f);
        paint1.Should().NotBeSameAs(paint2);
    }

    [Fact]
    public void GetStrokePaint_DifferentThickness_ReturnsDifferentPaint()
    {
        var paint1 = SkiaRenderCache.GetStrokePaint(SKColors.Red, 1.0f);
        var paint2 = SkiaRenderCache.GetStrokePaint(SKColors.Red, 2.0f);
        paint1.Should().NotBeSameAs(paint2);
    }

    [Fact]
    public void GetStrokePaint_IsStrokeStyle()
    {
        var paint = SkiaRenderCache.GetStrokePaint(SKColors.Green, 1.5f);
        paint.Style.Should().Be(SKPaintStyle.Stroke);
    }

    [Fact]
    public void GetFillPaint_ReturnsCachedPaintOnSecondCall()
    {
        var paint1 = SkiaRenderCache.GetFillPaint(SKColors.Red);
        var paint2 = SkiaRenderCache.GetFillPaint(SKColors.Red);
        paint1.Should().BeSameAs(paint2);
    }

    [Fact]
    public void GetFillPaint_IsFillStyle()
    {
        var paint = SkiaRenderCache.GetFillPaint(SKColors.Green);
        paint.Style.Should().Be(SKPaintStyle.Fill);
    }

    [Fact]
    public void Clear_CausesNewInstanceOnNextCall()
    {
        var paint1 = SkiaRenderCache.GetStrokePaint(SKColors.Red, 1.0f);
        SkiaRenderCache.Clear();
        var paint2 = SkiaRenderCache.GetStrokePaint(SKColors.Red, 1.0f);
        paint2.Should().NotBeNull();
        paint2.Should().NotBeSameAs(paint1);
    }
}
