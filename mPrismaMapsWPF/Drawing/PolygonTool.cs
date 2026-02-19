using System.Windows;
using System.Windows.Input;
using mPrismaMapsWPF.Helpers;

namespace mPrismaMapsWPF.Drawing;

public class PolygonTool : IDrawingTool
{
    private readonly List<Point> _points = new();
    private Point _currentPoint;
    private DateTime _lastClickTime = DateTime.MinValue;
    private const double DoubleClickThresholdMs = 300;

    public string Name => "Polygon";
    public DrawingMode Mode => DrawingMode.DrawPolygon;
    public bool IsDrawing => _points.Count > 0;
    public bool IsPreviewClosed => true; // Polygon preview is always closed

    public string StatusText => _points.Count switch
    {
        0 => "Click to set first point",
        1 => "Click to add points (minimum 3 for polygon)",
        2 => "Click to add points | Hold Shift to snap angle | double-click or Enter to close | Escape to cancel",
        _ => $"{_points.Count} points - Click to add | Hold Shift to snap angle | double-click or Enter to close | Escape to cancel"
    };

    public event EventHandler<DrawingCompletedEventArgs>? Completed;
    public event EventHandler? Cancelled;

    public void OnMouseDown(Point cadPoint, MouseButton button)
    {
        if (button == MouseButton.Right)
        {
            if (_points.Count >= 3)
            {
                // Complete with current points
                Complete();
            }
            else
            {
                Cancel();
            }
            return;
        }

        if (button != MouseButton.Left)
            return;

        // Check for double-click
        var now = DateTime.Now;
        if ((now - _lastClickTime).TotalMilliseconds < DoubleClickThresholdMs && _points.Count >= 3)
        {
            Complete();
            return;
        }
        _lastClickTime = now;

        _points.Add(ApplyAngleSnap(cadPoint));
        _currentPoint = _points[^1];
    }

    public void OnMouseMove(Point cadPoint)
    {
        _currentPoint = ApplyAngleSnap(cadPoint);
    }

    public void OnMouseUp(Point cadPoint, MouseButton button)
    {
        // Not used for polygon tool
    }

    public void OnKeyDown(Key key)
    {
        switch (key)
        {
            case Key.Escape:
                Cancel();
                break;
            case Key.Enter:
                if (_points.Count >= 3)
                    Complete();
                break;
            case Key.Back:
                // Remove last point
                if (_points.Count > 0)
                    _points.RemoveAt(_points.Count - 1);
                break;
        }
    }

    public IReadOnlyList<Point>? GetPreviewPoints()
    {
        if (_points.Count == 0)
            return null;

        var preview = new List<Point>(_points) { _currentPoint };
        return preview;
    }

    public void Reset()
    {
        _points.Clear();
        _currentPoint = default;
        _lastClickTime = DateTime.MinValue;
    }

    private Point ApplyAngleSnap(Point cadPoint)
    {
        if (_points.Count > 0 && IsShiftHeld())
            return SnapHelper.SnapToAngle(_points[^1], cadPoint);
        return cadPoint;
    }

    private static bool IsShiftHeld()
    {
        try { return Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift); }
        catch (InvalidOperationException) { return false; }
    }

    private void Complete()
    {
        if (_points.Count < 3)
            return;

        var completedPoints = _points.ToList();
        Reset();
        Completed?.Invoke(this, new DrawingCompletedEventArgs(completedPoints, true, Mode));
    }

    private void Cancel()
    {
        Reset();
        Cancelled?.Invoke(this, EventArgs.Empty);
    }
}
