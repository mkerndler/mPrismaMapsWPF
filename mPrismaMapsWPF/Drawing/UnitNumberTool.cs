using System.Windows;
using System.Windows.Input;

namespace mPrismaMapsWPF.Drawing;

public class UnitNumberTool : IDrawingTool
{
    private Point _currentPoint;

    public string Name => "Place Unit Number";
    public DrawingMode Mode => DrawingMode.PlaceUnitNumber;
    public bool IsDrawing => true; // Always show preview
    public bool IsPreviewClosed => false;

    public string StatusText => $"Click to place unit number '{CurrentText}', Right-click or Escape to cancel";

    // Numbering properties - set by ViewModel via CadCanvas.ConfigureUnitNumberTool
    public string Prefix { get; set; } = "";
    public int NextNumber { get; set; } = 1;
    public string FormatString { get; set; } = "D3";
    public double TextHeight { get; set; } = 10.0;

    public string CurrentText => Prefix + NextNumber.ToString(FormatString);

    public event EventHandler<DrawingCompletedEventArgs>? Completed;
    public event EventHandler? Cancelled;

    public void OnMouseDown(Point cadPoint, MouseButton button)
    {
        if (button == MouseButton.Right)
        {
            Cancelled?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (button != MouseButton.Left)
            return;

        var points = new List<Point> { cadPoint };
        Completed?.Invoke(this, new DrawingCompletedEventArgs(points, false, Mode, CurrentText));
    }

    public void OnMouseMove(Point cadPoint)
    {
        _currentPoint = cadPoint;
    }

    public void OnMouseUp(Point cadPoint, MouseButton button)
    {
        // Not used
    }

    public void OnKeyDown(Key key)
    {
        if (key == Key.Escape)
        {
            Cancelled?.Invoke(this, EventArgs.Empty);
        }
    }

    public IReadOnlyList<Point>? GetPreviewPoints()
    {
        return new List<Point> { _currentPoint };
    }

    public void Reset()
    {
        // Do NOT reset numbering state - numbering is forward-only
    }
}
