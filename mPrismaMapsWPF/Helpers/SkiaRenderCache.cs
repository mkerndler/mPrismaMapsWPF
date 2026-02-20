using SkiaSharp;

namespace mPrismaMapsWPF.Helpers;

public static class SkiaRenderCache
{
    private static readonly Dictionary<(uint, float), SKPaint> _strokeCache = new();
    private static readonly Dictionary<uint, SKPaint> _fillCache = new();
    private static readonly SKTypeface _typeface =
        SKTypeface.FromFamilyName("Arial") ?? SKTypeface.Default;

    public static SKPaint GetStrokePaint(SKColor color, float thickness)
    {
        uint key = Pack(color);
        if (_strokeCache.TryGetValue((key, thickness), out var cached)) return cached;
        var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = color,
            StrokeWidth = thickness,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round
        };
        _strokeCache[(key, thickness)] = paint;
        return paint;
    }

    public static SKPaint GetFillPaint(SKColor color)
    {
        uint key = Pack(color);
        if (_fillCache.TryGetValue(key, out var cached)) return cached;
        var paint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = color,
            IsAntialias = true
        };
        _fillCache[key] = paint;
        return paint;
    }

    // SKFont is lightweight — create per call, caller disposes
    public static SKFont MakeFont(float size, SKTypeface? typeface = null)
        => new SKFont(typeface ?? _typeface, size);

    public static void Clear()
    {
        foreach (var p in _strokeCache.Values) p.Dispose();
        _strokeCache.Clear();
        foreach (var p in _fillCache.Values) p.Dispose();
        _fillCache.Clear();
    }

    private static uint Pack(SKColor c)
        => ((uint)c.Alpha << 24) | ((uint)c.Red << 16) | ((uint)c.Green << 8) | c.Blue;
}
