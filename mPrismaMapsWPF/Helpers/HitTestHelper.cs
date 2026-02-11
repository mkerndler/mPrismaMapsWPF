using System.Text.RegularExpressions;
using ACadSharp.Entities;
using WpfPoint = System.Windows.Point;

namespace mPrismaMapsWPF.Helpers;

public static class HitTestHelper
{
    private const double DefaultTolerance = 5.0;

    public static bool HitTest(Entity entity, WpfPoint point, double tolerance = DefaultTolerance)
    {
        return entity switch
        {
            Line line => HitTestLine(line, point, tolerance),
            Arc arc => HitTestArc(arc, point, tolerance),
            Circle circle => HitTestCircle(circle, point, tolerance),
            LwPolyline polyline => HitTestPolyline(polyline, point, tolerance),
            TextEntity text => HitTestText(text, point, tolerance),
            MText mtext => HitTestMText(mtext, point, tolerance),
            Insert insert => HitTestInsert(insert, point, tolerance),
            _ => false
        };
    }

    private static bool HitTestLine(Line line, WpfPoint point, double tolerance)
    {
        double x1 = line.StartPoint.X;
        double y1 = line.StartPoint.Y;
        double x2 = line.EndPoint.X;
        double y2 = line.EndPoint.Y;

        double distance = DistanceToLineSegment(point.X, point.Y, x1, y1, x2, y2);
        return distance <= tolerance;
    }

    private static bool HitTestCircle(Circle circle, WpfPoint point, double tolerance)
    {
        double dx = point.X - circle.Center.X;
        double dy = point.Y - circle.Center.Y;
        double distance = Math.Sqrt(dx * dx + dy * dy);

        return Math.Abs(distance - circle.Radius) <= tolerance;
    }

    private static bool HitTestArc(Arc arc, WpfPoint point, double tolerance)
    {
        double dx = point.X - arc.Center.X;
        double dy = point.Y - arc.Center.Y;
        double distance = Math.Sqrt(dx * dx + dy * dy);

        if (Math.Abs(distance - arc.Radius) > tolerance)
            return false;

        double angle = Math.Atan2(dy, dx);
        if (angle < 0) angle += 2 * Math.PI;

        double startAngle = arc.StartAngle;
        double endAngle = arc.EndAngle;

        if (startAngle < 0) startAngle += 2 * Math.PI;
        if (endAngle < 0) endAngle += 2 * Math.PI;

        if (endAngle < startAngle)
        {
            return angle >= startAngle || angle <= endAngle;
        }
        return angle >= startAngle && angle <= endAngle;
    }

    private static bool HitTestPolyline(LwPolyline polyline, WpfPoint point, double tolerance)
    {
        var vertices = polyline.Vertices.ToList();
        if (vertices.Count < 2)
            return false;

        for (int i = 0; i < vertices.Count - 1; i++)
        {
            double distance = DistanceToLineSegment(
                point.X, point.Y,
                vertices[i].Location.X, vertices[i].Location.Y,
                vertices[i + 1].Location.X, vertices[i + 1].Location.Y);

            if (distance <= tolerance)
                return true;
        }

        if (polyline.IsClosed && vertices.Count > 2)
        {
            double distance = DistanceToLineSegment(
                point.X, point.Y,
                vertices[^1].Location.X, vertices[^1].Location.Y,
                vertices[0].Location.X, vertices[0].Location.Y);

            if (distance <= tolerance)
                return true;
        }

        return false;
    }

    private static bool HitTestText(TextEntity text, WpfPoint point, double tolerance)
    {
        if (string.IsNullOrEmpty(text.Value))
            return false;

        double width = Math.Max(text.Value.Length * text.Height * 0.6, text.Height);
        double height = text.Height;

        double x = text.InsertPoint.X;
        double y = text.InsertPoint.Y;

        // Text renders upward from insert point (insert point is baseline/bottom)
        return point.X >= x - tolerance &&
               point.X <= x + width + tolerance &&
               point.Y >= y - tolerance &&
               point.Y <= y + height + tolerance;
    }

    private static bool HitTestMText(MText mtext, WpfPoint point, double tolerance)
    {
        if (string.IsNullOrEmpty(mtext.Value))
            return false;

        // Strip formatting codes to get actual visible text length
        string cleanText = StripMTextFormatting(mtext.Value);
        double width = mtext.RectangleWidth > 0
            ? mtext.RectangleWidth
            : Math.Max(cleanText.Length * mtext.Height * 0.6, mtext.Height);
        int lineCount = cleanText.Count(c => c == '\n') + 1;
        double height = mtext.Height * lineCount;

        double x = mtext.InsertPoint.X;
        double y = mtext.InsertPoint.Y;

        // Text renders upward from insert point (insert point is baseline/bottom)
        return point.X >= x - tolerance &&
               point.X <= x + width + tolerance &&
               point.Y >= y - tolerance &&
               point.Y <= y + height + tolerance;
    }

    private static string StripMTextFormatting(string mtext)
    {
        var result = Regex.Replace(mtext, @"\\[A-Za-z][^;]*;", "");
        result = Regex.Replace(result, @"\{|\}", "");
        result = result.Replace("\\P", "\n");
        return result;
    }

    private static bool HitTestInsert(Insert insert, WpfPoint point, double tolerance)
    {
        double dx = point.X - insert.InsertPoint.X;
        double dy = point.Y - insert.InsertPoint.Y;
        return Math.Sqrt(dx * dx + dy * dy) <= tolerance * 5;
    }

    private static double DistanceToLineSegment(double px, double py, double x1, double y1, double x2, double y2)
    {
        double dx = x2 - x1;
        double dy = y2 - y1;

        if (dx == 0 && dy == 0)
        {
            return Math.Sqrt((px - x1) * (px - x1) + (py - y1) * (py - y1));
        }

        double t = ((px - x1) * dx + (py - y1) * dy) / (dx * dx + dy * dy);
        t = Math.Max(0, Math.Min(1, t));

        double nearestX = x1 + t * dx;
        double nearestY = y1 + t * dy;

        return Math.Sqrt((px - nearestX) * (px - nearestX) + (py - nearestY) * (py - nearestY));
    }
}
