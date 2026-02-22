using System.Windows;

namespace mPrismaMapsWPF.Helpers;

public enum TransformHandle
{
    None,
    TopLeft, TopCenter, TopRight,
    MiddleLeft, MiddleRight,
    BottomLeft, BottomCenter, BottomRight,
    Rotation
}

public static class TransformHitTestHelper
{
    private const double RotationHandleOffset = 30.0;

    public static Dictionary<TransformHandle, Point> GetHandlePositions(Rect boundingBoxScreen)
    {
        double left = boundingBoxScreen.Left;
        double right = boundingBoxScreen.Right;
        double top = boundingBoxScreen.Top;
        double bottom = boundingBoxScreen.Bottom;
        double centerX = (left + right) / 2;
        double centerY = (top + bottom) / 2;

        return new Dictionary<TransformHandle, Point>
        {
            [TransformHandle.TopLeft] = new(left, top),
            [TransformHandle.TopCenter] = new(centerX, top),
            [TransformHandle.TopRight] = new(right, top),
            [TransformHandle.MiddleLeft] = new(left, centerY),
            [TransformHandle.MiddleRight] = new(right, centerY),
            [TransformHandle.BottomLeft] = new(left, bottom),
            [TransformHandle.BottomCenter] = new(centerX, bottom),
            [TransformHandle.BottomRight] = new(right, bottom),
            [TransformHandle.Rotation] = new(centerX, top - RotationHandleOffset)
        };
    }

    public static TransformHandle HitTest(Point screenPoint, Rect boundingBoxScreen, double tolerance = 6.0)
    {
        var handles = GetHandlePositions(boundingBoxScreen);
        double toleranceSq = tolerance * tolerance;

        foreach (var (handle, pos) in handles)
        {
            double dx = screenPoint.X - pos.X;
            double dy = screenPoint.Y - pos.Y;
            if (dx * dx + dy * dy <= toleranceSq)
                return handle;
        }

        return TransformHandle.None;
    }
}
