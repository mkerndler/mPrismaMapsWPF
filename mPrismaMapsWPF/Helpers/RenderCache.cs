using System.Collections.Concurrent;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace mPrismaMapsWPF.Helpers;

public static class RenderCache
{
    private static readonly ConcurrentDictionary<(Color color, double thickness), Pen> _penCache = new();
    private static readonly ConcurrentDictionary<Color, SolidColorBrush> _brushCache = new();
    private static readonly ConcurrentDictionary<FormattedTextKey, FormattedText> _textCache = new();
    private static readonly ConcurrentDictionary<double, RotateTransform> _rotateTransformCache = new();
    private static readonly ConcurrentDictionary<(double x, double y), ScaleTransform> _scaleTransformCache = new();

    private static readonly Typeface DefaultTypeface = new("Arial");

    public static Pen GetPen(Color color, double thickness)
    {
        var key = (color, thickness);
        return _penCache.GetOrAdd(key, k =>
        {
            var brush = GetBrush(k.color);
            var pen = new Pen(brush, k.thickness);
            pen.Freeze();
            return pen;
        });
    }

    public static SolidColorBrush GetBrush(Color color)
    {
        return _brushCache.GetOrAdd(color, c =>
        {
            var brush = new SolidColorBrush(c);
            brush.Freeze();
            return brush;
        });
    }

    public static FormattedText GetFormattedText(
        ulong handle,
        string text,
        double fontSize,
        Color color,
        double maxWidth = 0)
    {
        var key = new FormattedTextKey(handle, text, fontSize, color, maxWidth);

        return _textCache.GetOrAdd(key, k =>
        {
            var brush = GetBrush(k.Color);
            var formattedText = new FormattedText(
                k.Text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                DefaultTypeface,
                k.FontSize,
                brush,
                1.0);

            if (k.MaxWidth > 0)
            {
                formattedText.MaxTextWidth = k.MaxWidth;
            }

            return formattedText;
        });
    }

    /// <summary>
    /// Gets a frozen RotateTransform for the specified angle (in degrees).
    /// Note: These transforms have no center point - use for simple rotations only.
    /// </summary>
    public static RotateTransform GetRotateTransform(double angleDegrees)
    {
        // Round to reduce cache entries for nearly identical angles
        double roundedAngle = Math.Round(angleDegrees, 2);
        return _rotateTransformCache.GetOrAdd(roundedAngle, angle =>
        {
            var transform = new RotateTransform(angle);
            transform.Freeze();
            return transform;
        });
    }

    /// <summary>
    /// Gets a frozen ScaleTransform for the specified scale factors.
    /// Note: These transforms have no center point - use for simple scales only.
    /// </summary>
    public static ScaleTransform GetScaleTransform(double scaleX, double scaleY)
    {
        // Round to reduce cache entries
        var key = (Math.Round(scaleX, 4), Math.Round(scaleY, 4));
        return _scaleTransformCache.GetOrAdd(key, k =>
        {
            var transform = new ScaleTransform(k.x, k.y);
            transform.Freeze();
            return transform;
        });
    }

    public static void Clear()
    {
        _penCache.Clear();
        _brushCache.Clear();
        _textCache.Clear();
        _rotateTransformCache.Clear();
        _scaleTransformCache.Clear();
    }

    public static void ClearTextCache()
    {
        _textCache.Clear();
    }

    private readonly record struct FormattedTextKey(
        ulong Handle,
        string Text,
        double FontSize,
        Color Color,
        double MaxWidth);
}
