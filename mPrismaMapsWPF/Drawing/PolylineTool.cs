using System.Windows;
using System.Windows.Input;

namespace mPrismaMapsWPF.Drawing;

public class PolylineTool : IDrawingTool
{
    private readonly List<Point> _points = new();
    private Point _currentPoint;
    private DateTime _lastClickTime = DateTime.MinValue;
    private const double DoubleClickThresholdMs = 300;

    public string Name => "Polyline";
    public DrawingMode Mode => DrawingMode.DrawPolyline;
    public bool IsDrawing => _points.Count > 0;
    public bool IsPreviewClosed => false;

    public string StatusText => _points.Count switch
    {
        0 => "Click to set first point",
        1 => "Click to add points, double-click or Enter to finish, Escape to cancel",
        _ => $"{_points.Count} points - Click to add, double-click or Enter to finish, Escape to cancel"
    };

    public event EventHandler<DrawingCompletedEventArgs>? Completed;
    public event EventHandler? Cancelled;

    public void OnMouseDown(Point cadPoint, MouseButton button)
    {
        if (button == MouseButton.Right)
        {
            if (_points.Count >= 2)
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
        if ((now - _lastClickTime).TotalMilliseconds < DoubleClickThresholdMs && _points.Count >= 2)
        {
            Complete();
            return;
        }
        _lastClickTime = now;

        _points.Add(cadPoint);
        _currentPoint = cadPoint;
    }

    public void OnMouseMove(Point cadPoint)
    {
        _currentPoint = cadPoint;
    }

    public void OnMouseUp(Point cadPoint, MouseButton button)
    {
        // Not used for polyline tool
    }

    public void OnKeyDown(Key key)
    {
        switch (key)
        {
            case Key.Escape:
                Cancel();
                break;
            case Key.Enter:
                if (_points.Count >= 2)
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

    private void Complete()
    {
        if (_points.Count < 2)
            return;

        var completedPoints = _points.ToList();
        Reset();
        Completed?.Invoke(this, new DrawingCompletedEventArgs(completedPoints, false, Mode));
    }

    private void Cancel()
    {
        Reset();
        Cancelled?.Invoke(this, EventArgs.Empty);
    }
}
