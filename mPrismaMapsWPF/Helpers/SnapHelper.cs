using System.Windows;
using mPrismaMapsWPF.Drawing;

namespace mPrismaMapsWPF.Helpers;

public static class SnapHelper
{
    private static readonly double[] DefaultSnapAngles =
        Enumerable.Range(0, 8).Select(i => i * 45.0).ToArray();

    /// <summary>
    /// Snaps <paramref name="target"/> to the nearest 45° angle from <paramref name="anchor"/>,
    /// preserving the distance between the two points.
    /// </summary>
    public static Point SnapToAngle(Point anchor, Point target)
    {
        double dx = target.X - anchor.X;
        double dy = target.Y - anchor.Y;
        double distance = Math.Sqrt(dx * dx + dy * dy);

        if (distance < 1e-9)
            return target;

        double angleDeg = Math.Atan2(dy, dx) * 180.0 / Math.PI;

        double bestAngle = DefaultSnapAngles.MinBy(a => AngleDelta(angleDeg, a));
        double rad = bestAngle * Math.PI / 180.0;

        return new Point(
            anchor.X + distance * Math.Cos(rad),
            anchor.Y + distance * Math.Sin(rad));
    }

    private static double AngleDelta(double a, double b)
    {
        double d = Math.Abs(a - b) % 360.0;
        return d > 180.0 ? 360.0 - d : d;
    }

    /// <summary>
    /// Snaps a point to the nearest grid intersection.
    /// </summary>
    public static Point SnapToGrid(Point point, GridSnapSettings settings)
    {
        if (!settings.IsEnabled)
            return point;

        double snappedX = Math.Round((point.X - settings.OriginX) / settings.SpacingX)
                          * settings.SpacingX + settings.OriginX;
        double snappedY = Math.Round((point.Y - settings.OriginY) / settings.SpacingY)
                          * settings.SpacingY + settings.OriginY;

        return new Point(snappedX, snappedY);
    }

    /// <summary>
    /// Snaps X and Y coordinates to the nearest grid intersection.
    /// </summary>
    public static (double X, double Y) SnapToGrid(double x, double y, GridSnapSettings settings)
    {
        if (!settings.IsEnabled)
            return (x, y);

        double snappedX = Math.Round((x - settings.OriginX) / settings.SpacingX)
                          * settings.SpacingX + settings.OriginX;
        double snappedY = Math.Round((y - settings.OriginY) / settings.SpacingY)
                          * settings.SpacingY + settings.OriginY;

        return (snappedX, snappedY);
    }
}
