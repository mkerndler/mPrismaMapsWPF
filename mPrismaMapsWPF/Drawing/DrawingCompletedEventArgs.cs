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

    /// <summary>
    /// Handle of an existing node that was snapped to (for FairwayTool).
    /// </summary>
    public ulong? SnappedToHandle { get; }

    /// <summary>
    /// Handle of the previous node in the chain (for FairwayTool edge creation).
    /// </summary>
    public ulong? PreviousNodeHandle { get; }

    public DrawingCompletedEventArgs(
        IReadOnlyList<Point> points, bool isClosed, DrawingMode mode,
        string? text = null, ulong? snappedToHandle = null, ulong? previousNodeHandle = null)
    {
        Points = points;
        IsClosed = isClosed;
        Mode = mode;
        Text = text;
        SnappedToHandle = snappedToHandle;
        PreviousNodeHandle = previousNodeHandle;
    }
}
