using System.Windows;
using System.Windows.Input;

namespace mPrismaMapsWPF.Drawing;

public class ZoomAreaTool : IDrawingTool
{
    private Point? _startPoint;
    private Point _currentPoint;

    public string Name => "Zoom to Area";
    public DrawingMode Mode => DrawingMode.ZoomToArea;
    public bool IsDrawing => _startPoint.HasValue;
    public bool IsPreviewClosed => true;

    public string StatusText => _startPoint.HasValue
        ? "Release to zoom to area, or press Escape to cancel"
        : "Click and drag to define zoom area";

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
    }

    public void OnMouseMove(Point cadPoint)
    {
        _currentPoint = cadPoint;
    }

    public void OnMouseUp(Point cadPoint, MouseButton button)
    {
        if (button != MouseButton.Left || !_startPoint.HasValue)
            return;

        var start = _startPoint.Value;
        var end = cadPoint;

        // Require minimum drag distance
        double dx = Math.Abs(end.X - start.X);
        double dy = Math.Abs(end.Y - start.Y);
        if (dx < 0.001 && dy < 0.001)
        {
            Reset();
            return;
        }

        var minX = Math.Min(start.X, end.X);
        var minY = Math.Min(start.Y, end.Y);
        var maxX = Math.Max(start.X, end.X);
        var maxY = Math.Max(start.Y, end.Y);

        var points = new List<Point>
        {
            new(minX, minY),
            new(maxX, maxY)
        };

        Reset();
        Completed?.Invoke(this, new DrawingCompletedEventArgs(points, false, Mode));
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

        var s = _startPoint.Value;
        var c = _currentPoint;

        return new List<Point>
        {
            new(s.X, s.Y),
            new(c.X, s.Y),
            new(c.X, c.Y),
            new(s.X, c.Y)
        };
    }

    public void Reset()
    {
        _startPoint = null;
        _currentPoint = default;
    }

    private void Cancel()
    {
        Reset();
        Cancelled?.Invoke(this, EventArgs.Empty);
    }
}
