using System.Windows;
using mPrismaMapsWPF.Drawing;

namespace mPrismaMapsWPF.Helpers;

public static class SnapHelper
{
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
