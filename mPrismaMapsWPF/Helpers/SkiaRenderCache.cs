using System.Collections.Concurrent;
using SkiaSharp;

namespace mPrismaMapsWPF.Helpers;

public static class SkiaRenderCache
{
    private static readonly ConcurrentDictionary<(uint, float), SKPaint> _strokeCache = new();
    private static readonly ConcurrentDictionary<uint, SKPaint> _fillCache = new();
    private static readonly SKTypeface _typeface =
        SKTypeface.FromFamilyName("Arial") ?? SKTypeface.Default;

    public static SKPaint GetStrokePaint(SKColor color, float thickness)
    {
        uint key = Pack(color);
        return _strokeCache.GetOrAdd((key, thickness), _ => new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = color,
            StrokeWidth = thickness,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round
        });
    }

    public static SKPaint GetFillPaint(SKColor color)
    {
        uint key = Pack(color);
        return _fillCache.GetOrAdd(key, _ => new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = color,
            IsAntialias = true
        });
    }

    // SKFont is lightweight — create per call, caller disposes
    public static SKFont MakeFont(float size, SKTypeface? typeface = null)
        => new SKFont(typeface ?? _typeface, size);

    public static void Clear()
    {
        var strokePaints = _strokeCache.Values.ToList();
        _strokeCache.Clear();
        foreach (var p in strokePaints) p.Dispose();

        var fillPaints = _fillCache.Values.ToList();
        _fillCache.Clear();
        foreach (var p in fillPaints) p.Dispose();
    }

    private static uint Pack(SKColor c)
        => ((uint)c.Alpha << 24) | ((uint)c.Red << 16) | ((uint)c.Green << 8) | c.Blue;
}
