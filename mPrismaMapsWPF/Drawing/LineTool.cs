using System.Windows;
using System.Windows.Input;
using mPrismaMapsWPF.Helpers;

namespace mPrismaMapsWPF.Drawing;

public class LineTool : IDrawingTool
{
    private Point? _startPoint;
    private Point _currentPoint;

    public string Name => "Line";
    public DrawingMode Mode => DrawingMode.DrawLine;
    public bool IsDrawing => _startPoint.HasValue;
    public bool IsPreviewClosed => false;

    public string StatusText => _startPoint.HasValue
        ? "Click to set end point | Hold Shift to snap angle | Escape to cancel"
        : "Click to set start point";

    public event EventHandler<DrawingCompletedEventArgs>? Completed;
    public event EventHandler? Cancelled;

    public void OnMouseDown(Point cadPoint, MouseButton button)
    {
        if (button == MouseButton.Right)
        {
            Cancel();
            return;
        }

        if (button != MouseButton.Left)
            return;

        if (!_startPoint.HasValue)
        {
            _startPoint = cadPoint;
            _currentPoint = cadPoint;
        }
        else
        {
            // Complete the line
            var snapped = ApplyAngleSnap(cadPoint);
            var points = new List<Point> { _startPoint.Value, snapped };
            Reset();
            Completed?.Invoke(this, new DrawingCompletedEventArgs(points, false, Mode));
        }
    }

    public void OnMouseMove(Point cadPoint)
    {
        _currentPoint = ApplyAngleSnap(cadPoint);
    }

    public void OnMouseUp(Point cadPoint, MouseButton button)
    {
        // Not used for line tool
    }

    public void OnKeyDown(Key key)
    {
        if (key == Key.Escape)
        {
            Cancel();
        }
    }

    public IReadOnlyList<Point>? GetPreviewPoints()
    {
        if (!_startPoint.HasValue)
            return null;

        return new List<Point> { _startPoint.Value, _currentPoint };
    }

    public void Reset()
    {
        _startPoint = null;
        _currentPoint = default;
    }

    private Point ApplyAngleSnap(Point cadPoint)
    {
        if (_startPoint.HasValue && IsShiftHeld())
            return SnapHelper.SnapToAngle(_startPoint.Value, cadPoint);
        return cadPoint;
    }

    private static bool IsShiftHeld() =>
        Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

    private void Cancel()
    {
        Reset();
        Cancelled?.Invoke(this, EventArgs.Empty);
    }
}
