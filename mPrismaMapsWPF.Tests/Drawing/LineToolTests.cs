using System.Windows;
using System.Windows.Input;
using FluentAssertions;
using mPrismaMapsWPF.Drawing;

namespace mPrismaMapsWPF.Tests.Drawing;

public class LineToolTests
{
    private readonly LineTool _tool = new();

    [Fact]
    public void InitialState_IsDrawingIsFalse()
    {
        _tool.IsDrawing.Should().BeFalse();
    }

    [Fact]
    public void Name_IsLine()
    {
        _tool.Name.Should().Be("Line");
    }

    [Fact]
    public void IsPreviewClosed_IsFalse()
    {
        _tool.IsPreviewClosed.Should().BeFalse();
    }

    [Fact]
    public void FirstClick_SetsStart_IsDrawingTrue()
    {
        _tool.OnMouseDown(new Point(5, 5), MouseButton.Left);
        _tool.IsDrawing.Should().BeTrue();
    }

    [Fact]
    public void SecondClick_CompletesLine()
    {
        DrawingCompletedEventArgs? args = null;
        _tool.Completed += (_, e) => args = e;

        _tool.OnMouseDown(new Point(0, 0), MouseButton.Left);
        _tool.OnMouseDown(new Point(10, 10), MouseButton.Left);

        args.Should().NotBeNull();
        args!.Points.Should().HaveCount(2);
        args.Points[0].Should().Be(new Point(0, 0));
        args.Points[1].Should().Be(new Point(10, 10));
        args.IsClosed.Should().BeFalse();
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
    public void MouseMove_UpdatesPreviewPoints()
    {
        _tool.OnMouseDown(new Point(0, 0), MouseButton.Left);
        _tool.OnMouseMove(new Point(15, 20));

        var preview = _tool.GetPreviewPoints();
        preview.Should().NotBeNull();
        preview.Should().HaveCount(2);
        preview![1].Should().Be(new Point(15, 20));
    }

    [Fact]
    public void GetPreviewPoints_ReturnsNull_WhenNotDrawing()
    {
        _tool.GetPreviewPoints().Should().BeNull();
    }

    [Fact]
    public void Reset_ClearsState()
    {
        _tool.OnMouseDown(new Point(0, 0), MouseButton.Left);
        _tool.Reset();

        _tool.IsDrawing.Should().BeFalse();
        _tool.GetPreviewPoints().Should().BeNull();
    }

    [Fact]
    public void RightClick_Cancels()
    {
        bool cancelled = false;
        _tool.Cancelled += (_, _) => cancelled = true;

        _tool.OnMouseDown(new Point(0, 0), MouseButton.Left);
        _tool.OnMouseDown(new Point(10, 10), MouseButton.Right);

        cancelled.Should().BeTrue();
    }
}
