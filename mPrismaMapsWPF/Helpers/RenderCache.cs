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

    public static void Clear()
    {
        _penCache.Clear();
        _brushCache.Clear();
        _textCache.Clear();
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
