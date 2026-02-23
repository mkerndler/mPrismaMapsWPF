using ACadSharp.Entities;
using FluentAssertions;
using mPrismaMapsWPF.Commands;
using mPrismaMapsWPF.Tests.TestHelpers;

namespace mPrismaMapsWPF.Tests.Commands;

public class AdjustWalkwayEdgesCommandTests
{
    private static AdjustWalkwayEdgesCommand MakeCommand(
        List<(Line, bool, bool)> adjustments, double dx, double dy)
    {
        var doc = EntityFactory.CreateDocumentModel();
        return new AdjustWalkwayEdgesCommand(doc, adjustments, dx, dy);
    }

    [Fact]
    public void Execute_AdjustsStartPoint()
    {
        var line = EntityFactory.CreateLine(0, 0, 10, 10);
        var adjustments = new List<(Line, bool, bool)> { (line, true, false) };

        var cmd = MakeCommand(adjustments, 5, 3);
        cmd.Execute();

        line.StartPoint.X.Should().Be(5);
        line.StartPoint.Y.Should().Be(3);
        line.EndPoint.X.Should().Be(10); // unchanged
        line.EndPoint.Y.Should().Be(10);
    }

    [Fact]
    public void Execute_AdjustsEndPoint()
    {
        var line = EntityFactory.CreateLine(0, 0, 10, 10);
        var adjustments = new List<(Line, bool, bool)> { (line, false, true) };

        var cmd = MakeCommand(adjustments, 5, 3);
        cmd.Execute();

        line.StartPoint.X.Should().Be(0); // unchanged
        line.StartPoint.Y.Should().Be(0);
        line.EndPoint.X.Should().Be(15);
        line.EndPoint.Y.Should().Be(13);
    }

    [Fact]
    public void Execute_AdjustsBothEndpoints()
    {
        var line = EntityFactory.CreateLine(0, 0, 10, 10);
        var adjustments = new List<(Line, bool, bool)> { (line, true, true) };

        var cmd = MakeCommand(adjustments, 5, 3);
        cmd.Execute();

        line.StartPoint.X.Should().Be(5);
        line.StartPoint.Y.Should().Be(3);
        line.EndPoint.X.Should().Be(15);
        line.EndPoint.Y.Should().Be(13);
    }

    [Fact]
    public void Undo_RestoresOriginalCoordinates()
    {
        var line = EntityFactory.CreateLine(0, 0, 10, 10);
        var adjustments = new List<(Line, bool, bool)> { (line, true, true) };

        var cmd = MakeCommand(adjustments, 5, 3);
        cmd.Execute();
        cmd.Undo();

        line.StartPoint.X.Should().BeApproximately(0, 0.001);
        line.StartPoint.Y.Should().BeApproximately(0, 0.001);
        line.EndPoint.X.Should().BeApproximately(10, 0.001);
        line.EndPoint.Y.Should().BeApproximately(10, 0.001);
    }
}
