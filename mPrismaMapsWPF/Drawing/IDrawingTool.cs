using System.Windows;
using System.Windows.Input;

namespace mPrismaMapsWPF.Drawing;

public interface IDrawingTool
{
    /// <summary>
    /// The name of the tool for display purposes.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The drawing mode this tool represents.
    /// </summary>
    DrawingMode Mode { get; }

    /// <summary>
    /// Whether the tool is currently in the middle of drawing.
    /// </summary>
    bool IsDrawing { get; }

    /// <summary>
    /// Instructions to show in the status bar.
    /// </summary>
    string StatusText { get; }

    /// <summary>
    /// Handle mouse down event.
    /// </summary>
    void OnMouseDown(Point cadPoint, MouseButton button);

    /// <summary>
    /// Handle mouse move event.
    /// </summary>
    void OnMouseMove(Point cadPoint);

    /// <summary>
    /// Handle mouse up event.
    /// </summary>
    void OnMouseUp(Point cadPoint, MouseButton button);

    /// <summary>
    /// Handle key down event.
    /// </summary>
    void OnKeyDown(Key key);

    /// <summary>
    /// Get the points for preview rendering (rubber-banding).
    /// </summary>
    IReadOnlyList<Point>? GetPreviewPoints();

    /// <summary>
    /// Whether the preview should be rendered as closed.
    /// </summary>
    bool IsPreviewClosed { get; }

    /// <summary>
    /// Raised when the drawing is completed.
    /// </summary>
    event EventHandler<DrawingCompletedEventArgs>? Completed;

    /// <summary>
    /// Raised when the drawing is cancelled.
    /// </summary>
    event EventHandler? Cancelled;

    /// <summary>
    /// Reset the tool to its initial state.
    /// </summary>
    void Reset();
}
