using System.Windows;

namespace mPrismaMapsWPF.Drawing;

public class DrawingCompletedEventArgs : EventArgs
{
    /// <summary>
    /// The points that define the completed shape.
    /// </summary>
    public IReadOnlyList<Point> Points { get; }

    /// <summary>
    /// Whether the shape should be closed (polygon vs polyline).
    /// </summary>
    public bool IsClosed { get; }

    /// <summary>
    /// The drawing mode that was used.
    /// </summary>
    public DrawingMode Mode { get; }

    /// <summary>
    /// Optional text associated with the drawing (e.g., unit number).
    /// </summary>
    public string? Text { get; }

    public DrawingCompletedEventArgs(IReadOnlyList<Point> points, bool isClosed, DrawingMode mode, string? text = null)
    {
        Points = points;
        IsClosed = isClosed;
        Mode = mode;
        Text = text;
    }
}
