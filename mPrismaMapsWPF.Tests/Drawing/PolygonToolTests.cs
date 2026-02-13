using System.Windows;
using System.Windows.Input;
using FluentAssertions;
using mPrismaMapsWPF.Drawing;

namespace mPrismaMapsWPF.Tests.Drawing;

public class PolygonToolTests
{
    private readonly PolygonTool _tool = new();

    [Fact]
    public void IsPreviewClosed_IsTrue()
    {
        _tool.IsPreviewClosed.Should().BeTrue();
    }

    [Fact]
    public void Name_IsPolygon()
    {
        _tool.Name.Should().Be("Polygon");
    }

    [Fact]
    public void Enter_RequiresMinimumThreePoints()
    {
        DrawingCompletedEventArgs? args = null;
        _tool.Completed += (_, e) => args = e;

        _tool.OnMouseDown(new Point(0, 0), MouseButton.Left);
        _tool.OnMouseDown(new Point(10, 0), MouseButton.Left);
        _tool.OnKeyDown(Key.Enter);

        args.Should().BeNull(); // Not enough points
    }

    [Fact]
    public void Enter_WithThreePoints_Completes()
    {
        DrawingCompletedEventArgs? args = null;
        _tool.Completed += (_, e) => args = e;

        _tool.OnMouseDown(new Point(0, 0), MouseButton.Left);
        _tool.OnMouseDown(new Point(10, 0), MouseButton.Left);
        _tool.OnMouseDown(new Point(10, 10), MouseButton.Left);
        _tool.OnKeyDown(Key.Enter);

        args.Should().NotBeNull();
        args!.Points.Should().HaveCount(3);
        args.IsClosed.Should().BeTrue();
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
        _tool.OnMouseDown(new Point(0, 0), MouseButton.Left);
        _tool.OnMouseDown(new Point(10, 0), MouseButton.Left);
        _tool.OnMouseDown(new Point(10, 10), MouseButton.Left);
        _tool.OnKeyDown(Key.Back);

        // After removing one, should have 2 points + mouse = 3 preview points
        var preview = _tool.GetPreviewPoints();
        preview.Should().HaveCount(3);
    }

    [Fact]
    public void RightClick_WithThreePoints_Completes()
    {
        DrawingCompletedEventArgs? args = null;
        _tool.Completed += (_, e) => args = e;

        _tool.OnMouseDown(new Point(0, 0), MouseButton.Left);
        _tool.OnMouseDown(new Point(10, 0), MouseButton.Left);
        _tool.OnMouseDown(new Point(10, 10), MouseButton.Left);
        _tool.OnMouseDown(new Point(5, 5), MouseButton.Right);

        args.Should().NotBeNull();
        args!.IsClosed.Should().BeTrue();
    }

    [Fact]
    public void RightClick_WithTwoPoints_Cancels()
    {
        bool cancelled = false;
        _tool.Cancelled += (_, _) => cancelled = true;

        _tool.OnMouseDown(new Point(0, 0), MouseButton.Left);
        _tool.OnMouseDown(new Point(10, 0), MouseButton.Left);
        _tool.OnMouseDown(new Point(5, 5), MouseButton.Right);

        cancelled.Should().BeTrue();
    }

    [Fact]
    public void GetPreviewPoints_ReturnsNull_WhenNotDrawing()
    {
        _tool.GetPreviewPoints().Should().BeNull();
    }
}
