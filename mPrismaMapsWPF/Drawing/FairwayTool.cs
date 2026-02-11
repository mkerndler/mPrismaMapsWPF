using System.Windows;
using System.Windows.Input;

namespace mPrismaMapsWPF.Drawing;

public class FairwayTool : IDrawingTool
{
    private readonly List<Point> _points = new();
    private Point _currentPoint;
    private readonly List<ulong?> _snappedHandles = new();

    // Existing nodes for snap detection
    private List<(ulong handle, double x, double y)> _existingNodes = new();

    public double NodeRadius { get; set; } = 0.3;
    public double SnapDistance { get; set; } = 1.5;

    public string Name => "Fairway";
    public DrawingMode Mode => DrawingMode.DrawFairway;
    public bool IsDrawing => _points.Count > 0;
    public bool IsPreviewClosed => false;

    public string StatusText => _points.Count switch
    {
        0 => "Click to place first node",
        _ => $"{_points.Count} node(s) - Click to add, Right-click/Escape to end segment"
    };

    public event EventHandler<DrawingCompletedEventArgs>? Completed;
    public event EventHandler? Cancelled;

    public void SetExistingNodes(List<(ulong handle, double x, double y)> nodes)
    {
        _existingNodes = nodes;
    }

    public void OnMouseDown(Point cadPoint, MouseButton button)
    {
        if (button == MouseButton.Right)
        {
            EndSegment();
            return;
        }

        if (button != MouseButton.Left)
            return;

        // Check for snap to existing node
        ulong? snappedHandle = null;
        var snapResult = FindSnapNode(cadPoint);
        if (snapResult != null)
        {
            cadPoint = new Point(snapResult.Value.x, snapResult.Value.y);
            snappedHandle = snapResult.Value.handle;
        }

        // Also check snap to nodes placed in this segment
        if (snappedHandle == null && _points.Count > 0)
        {
            for (int i = 0; i < _points.Count; i++)
            {
                double dx = _points[i].X - cadPoint.X;
                double dy = _points[i].Y - cadPoint.Y;
                if (Math.Sqrt(dx * dx + dy * dy) < SnapDistance)
                {
                    cadPoint = _points[i];
                    snappedHandle = _snappedHandles[i];
                    break;
                }
            }
        }

        // Determine previous node handle
        ulong? previousNodeHandle = null;
        if (_points.Count > 0)
        {
            previousNodeHandle = _snappedHandles[^1];
        }

        _points.Add(cadPoint);
        _snappedHandles.Add(snappedHandle);
        _currentPoint = cadPoint;

        // Fire completed for each point placed (node + optional edge)
        var points = new List<Point> { cadPoint };
        if (_points.Count >= 2)
        {
            // Include previous point for edge creation
            points.Insert(0, _points[^2]);
        }

        Completed?.Invoke(this, new DrawingCompletedEventArgs(
            points, false, Mode,
            snappedToHandle: snappedHandle,
            previousNodeHandle: previousNodeHandle));
    }

    public void OnMouseMove(Point cadPoint)
    {
        _currentPoint = cadPoint;
    }

    public void OnMouseUp(Point cadPoint, MouseButton button)
    {
    }

    public void OnKeyDown(Key key)
    {
        switch (key)
        {
            case Key.Escape:
                if (IsDrawing)
                    EndSegment();
                else
                    Cancelled?.Invoke(this, EventArgs.Empty);
                break;
            case Key.Enter:
                EndSegment();
                break;
            case Key.Back:
                if (_points.Count > 0)
                {
                    _points.RemoveAt(_points.Count - 1);
                    _snappedHandles.RemoveAt(_snappedHandles.Count - 1);
                }
                break;
        }
    }

    public IReadOnlyList<Point>? GetPreviewPoints()
    {
        if (_points.Count == 0)
            return null;

        var preview = new List<Point> { _points[^1], _currentPoint };
        return preview;
    }

    public void Reset()
    {
        _points.Clear();
        _snappedHandles.Clear();
        _currentPoint = default;
    }

    private void EndSegment()
    {
        Reset();
        // Don't fire Cancelled - just end the current chain so the next click starts fresh
    }

    private (ulong handle, double x, double y)? FindSnapNode(Point cadPoint)
    {
        double bestDist = SnapDistance;
        (ulong handle, double x, double y)? best = null;

        foreach (var node in _existingNodes)
        {
            double dx = node.x - cadPoint.X;
            double dy = node.y - cadPoint.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = node;
            }
        }

        return best;
    }
}
