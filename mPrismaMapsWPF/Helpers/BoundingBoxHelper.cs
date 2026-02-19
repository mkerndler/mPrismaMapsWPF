using System.Text.RegularExpressions;
using System.Windows;
using ACadSharp.Entities;
using Point = ACadSharp.Entities.Point;

namespace mPrismaMapsWPF.Helpers;

public static class BoundingBoxHelper
{
    private static readonly Dictionary<ulong, Rect?> _boundsCache = new();

    public static Rect? GetBounds(Entity entity)
    {
        if (_boundsCache.TryGetValue(entity.Handle, out var cached))
            return cached;

        // Note: Arc inherits from Circle, so check Arc first
        var bounds = entity switch
        {
            Line line => GetLineBounds(line),
            Arc arc => GetArcBounds(arc),
            Circle circle => GetCircleBounds(circle),
            Ellipse ellipse => GetEllipseBounds(ellipse),
            LwPolyline polyline => GetPolylineBounds(polyline),
            Polyline2D polyline2D => GetPolyline2DBounds(polyline2D),
            TextEntity text => GetTextBounds(text),
            MText mtext => GetMTextBounds(mtext),
            Insert insert => GetInsertBounds(insert),
            Point point => GetPointBounds(point),
            _ => null
        };

        _boundsCache[entity.Handle] = bounds;
        return bounds;
    }

    public static void InvalidateCache()
    {
        _boundsCache.Clear();
    }

    public static void InvalidateEntity(ulong handle)
    {
        _boundsCache.Remove(handle);
    }

    private static Rect GetLineBounds(Line line)
    {
        double minX = Math.Min(line.StartPoint.X, line.EndPoint.X);
        double minY = Math.Min(line.StartPoint.Y, line.EndPoint.Y);
        double maxX = Math.Max(line.StartPoint.X, line.EndPoint.X);
        double maxY = Math.Max(line.StartPoint.Y, line.EndPoint.Y);
        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    private static Rect GetCircleBounds(Circle circle)
    {
        return new Rect(
            circle.Center.X - circle.Radius,
            circle.Center.Y - circle.Radius,
            circle.Radius * 2,
            circle.Radius * 2);
    }

    private static Rect GetArcBounds(Arc arc)
    {
        // Conservative bounding box using full circle extent
        return new Rect(
            arc.Center.X - arc.Radius,
            arc.Center.Y - arc.Radius,
            arc.Radius * 2,
            arc.Radius * 2);
    }

    private static Rect GetEllipseBounds(Ellipse ellipse)
    {
        double majorRadius = Math.Sqrt(
            ellipse.MajorAxisEndPoint.X * ellipse.MajorAxisEndPoint.X +
            ellipse.MajorAxisEndPoint.Y * ellipse.MajorAxisEndPoint.Y);
        double minorRadius = majorRadius * ellipse.RadiusRatio;
        double maxRadius = Math.Max(majorRadius, minorRadius);

        return new Rect(
            ellipse.Center.X - maxRadius,
            ellipse.Center.Y - maxRadius,
            maxRadius * 2,
            maxRadius * 2);
    }

    private static Rect? GetPolylineBounds(LwPolyline polyline)
    {
        var vertices = polyline.Vertices.ToList();
        if (vertices.Count == 0)
            return null;

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        foreach (var vertex in vertices)
        {
            minX = Math.Min(minX, vertex.Location.X);
            minY = Math.Min(minY, vertex.Location.Y);
            maxX = Math.Max(maxX, vertex.Location.X);
            maxY = Math.Max(maxY, vertex.Location.Y);
        }

        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    private static Rect? GetPolyline2DBounds(Polyline2D polyline)
    {
        var vertices = polyline.Vertices.ToList();
        if (vertices.Count == 0)
            return null;

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        foreach (var vertex in vertices)
        {
            minX = Math.Min(minX, vertex.Location.X);
            minY = Math.Min(minY, vertex.Location.Y);
            maxX = Math.Max(maxX, vertex.Location.X);
            maxY = Math.Max(maxY, vertex.Location.Y);
        }

        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    private static Rect GetTextBounds(TextEntity text)
    {
        double width = string.IsNullOrEmpty(text.Value) ? 0 : Math.Max(text.Value.Length * text.Height * 0.6, text.Height);
        double height = text.Height;

        // Text renders upward from insert point
        return new Rect(
            text.InsertPoint.X,
            text.InsertPoint.Y,
            width,
            height);
    }

    private static Rect GetMTextBounds(MText mtext)
    {
        string cleanText = string.IsNullOrEmpty(mtext.Value) ? "" : StripMTextFormatting(mtext.Value);
        // Use a minimum effective height so zero-height text (e.g. legacy imports) still
        // produces a non-degenerate bounding box for spatial grid coverage.
        double effectiveHeight = mtext.Height > 0 ? mtext.Height : 1.0;
        double width = mtext.RectangleWidth > 0
            ? mtext.RectangleWidth
            : Math.Max(cleanText.Length * effectiveHeight * 0.6, effectiveHeight);
        int lineCount = cleanText.Count(c => c == '\n') + 1;
        double height = effectiveHeight * lineCount;

        // Text renders upward from insert point
        return new Rect(
            mtext.InsertPoint.X,
            mtext.InsertPoint.Y,
            width,
            height);
    }

    private static string StripMTextFormatting(string mtext)
    {
        var result = Regex.Replace(mtext, @"\\[A-Za-z][^;]*;", "");
        result = Regex.Replace(result, @"\{|\}", "");
        result = result.Replace("\\P", "\n");
        return result;
    }

    private static Rect GetInsertBounds(Insert insert)
    {
        if (insert.Block == null)
            return new Rect(insert.InsertPoint.X, insert.InsertPoint.Y, 0, 0);

        // Calculate approximate bounds from block entities
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        bool hasEntities = false;

        foreach (var entity in insert.Block.Entities)
        {
            var bounds = GetBounds(entity);
            if (bounds.HasValue)
            {
                hasEntities = true;
                minX = Math.Min(minX, bounds.Value.Left);
                minY = Math.Min(minY, bounds.Value.Top);
                maxX = Math.Max(maxX, bounds.Value.Right);
                maxY = Math.Max(maxY, bounds.Value.Bottom);
            }
        }

        if (!hasEntities)
            return new Rect(insert.InsertPoint.X, insert.InsertPoint.Y, 0, 0);

        // Apply insert transformation (scale and position)
        double width = (maxX - minX) * Math.Abs(insert.XScale);
        double height = (maxY - minY) * Math.Abs(insert.YScale);

        return new Rect(
            insert.InsertPoint.X + minX * insert.XScale,
            insert.InsertPoint.Y + minY * insert.YScale,
            width,
            height);
    }

    private static Rect GetPointBounds(Point point)
    {
        const double pointSize = 4.0;
        return new Rect(
            point.Location.X - pointSize,
            point.Location.Y - pointSize,
            pointSize * 2,
            pointSize * 2);
    }
}
