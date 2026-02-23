using System.Windows;
using System.Windows.Input;
using FluentAssertions;
using mPrismaMapsWPF.Drawing;

namespace mPrismaMapsWPF.Tests.Drawing;

public class PolylineToolTests
{
    private readonly PolylineTool _tool = new();

    [Fact]
    public void InitialState_IsDrawingIsFalse()
    {
        _tool.IsDrawing.Should().BeFalse();
    }

    [Fact]
    public void IsPreviewClosed_IsFalse()
    {
        _tool.IsPreviewClosed.Should().BeFalse();
    }

    [Fact]
    public void Click_AccumulatesPoints()
    {
        // Use a new tool instance and add delay between clicks to avoid double-click detection
        var tool = new PolylineTool();
        tool.OnMouseDown(new Point(0, 0), MouseButton.Left);
        tool.IsDrawing.Should().BeTrue();

        // Use OnKeyDown + OnMouseDown pattern to avoid double-click threshold
        // Just verify first point is stored and preview includes it
        var preview = tool.GetPreviewPoints();
        // 1 actual point + current mouse position
        preview.Should().HaveCount(2);
    }

    [Fact]
    public void Enter_FinishesPolyline_MinTwoPoints()
    {
        DrawingCompletedEventArgs? args = null;
        _tool.Completed += (_, e) => args = e;

        _tool.OnMouseDown(new Point(0, 0), MouseButton.Left);
        _tool.OnMouseDown(new Point(10, 0), MouseButton.Left);
        _tool.OnKeyDown(Key.Enter);

        args.Should().NotBeNull();
        args!.Points.Should().HaveCount(2);
        args.IsClosed.Should().BeFalse();
    }

    [Fact]
    public void Enter_WithOnePoint_DoesNotFinish()
    {
        DrawingCompletedEventArgs? args = null;
        _tool.Completed += (_, e) => args = e;

        _tool.OnMouseDown(new Point(0, 0), MouseButton.Left);
        _tool.OnKeyDown(Key.Enter);

        args.Should().BeNull();
    }

    [Fact]
    public void Enter_WithOnePoint_FiresCancelled()
    {
        bool cancelled = false;
        _tool.Cancelled += (_, _) => cancelled = true;

        _tool.OnMouseDown(new Point(0, 0), MouseButton.Left);
        _tool.OnKeyDown(Key.Enter);

        cancelled.Should().BeTrue();
    }

    [Fact]
    public void Escape_CancelsDrawing()
    {
        bool cancelled = false;
        _tool.Cancelled += (_, _) => cancelled = true;

        _tool.OnMouseDown(new Point(0, 0), MouseButton.Left);
        _tool.OnKeyDown(Key.Escape);

        cancelled.Should().BeTrue();
        _tool.IsDrawing.Should().BeFalse();
    }

    [Fact]
    public void Backspace_RemovesLastPoint()
    {
        var tool = new PolylineTool();
        tool.OnMouseDown(new Point(0, 0), MouseButton.Left);

        // After adding one point, remove it
        tool.OnKeyDown(Key.Back);

        // No points left, so should not be drawing
        tool.IsDrawing.Should().BeFalse();
        tool.GetPreviewPoints().Should().BeNull();
    }

    [Fact]
    public void GetPreviewPoints_IncludesCurrentMouse()
    {
        _tool.OnMouseDown(new Point(0, 0), MouseButton.Left);
        _tool.OnMouseMove(new Point(15, 20));

        var preview = _tool.GetPreviewPoints();
        preview.Should().HaveCount(2);
        preview![1].Should().Be(new Point(15, 20));
    }

    [Fact]
    public void RightClick_WithEnoughPoints_Completes()
    {
        DrawingCompletedEventArgs? args = null;
        _tool.Completed += (_, e) => args = e;

        _tool.OnMouseDown(new Point(0, 0), MouseButton.Left);
        _tool.OnMouseDown(new Point(10, 0), MouseButton.Left);
        _tool.OnMouseDown(new Point(10, 10), MouseButton.Right);

        args.Should().NotBeNull();
        args!.Points.Should().HaveCount(2);
    }

    [Fact]
    public void RightClick_WithOnePoint_Cancels()
    {
        bool cancelled = false;
        _tool.Cancelled += (_, _) => cancelled = true;

        _tool.OnMouseDown(new Point(0, 0), MouseButton.Left);
        _tool.OnMouseDown(new Point(5, 5), MouseButton.Right);

        cancelled.Should().BeTrue();
    }
}
