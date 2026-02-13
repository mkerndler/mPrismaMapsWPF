using ACadSharp.Entities;
using FluentAssertions;
using mPrismaMapsWPF.Commands;
using mPrismaMapsWPF.Tests.TestHelpers;

namespace mPrismaMapsWPF.Tests.Commands;

public class AddWalkwaySegmentCommandTests
{
    [Fact]
    public void Execute_CreatesNodeCircle()
    {
        var doc = EntityFactory.CreateDocumentModel();

        var cmd = new AddWalkwaySegmentCommand(doc, 10, 20, 1.5, null, null, null, null);
        cmd.Execute();

        cmd.CreatedNodeHandle.Should().NotBeNull();
        doc.ModelSpaceEntities.OfType<Circle>().Should().ContainSingle();
        doc.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void Execute_WithPreviousNode_CreatesEdgeLine()
    {
        var doc = EntityFactory.CreateDocumentModel();

        var cmd = new AddWalkwaySegmentCommand(doc, 10, 20, 1.5, null, 0, 0, null);
        cmd.Execute();

        cmd.CreatedEdgeHandle.Should().NotBeNull();
        doc.ModelSpaceEntities.OfType<Line>().Should().ContainSingle();
    }

    [Fact]
    public void Execute_SnappedToExistingNode_DoesNotCreateCircle()
    {
        var doc = EntityFactory.CreateDocumentModel();

        var cmd = new AddWalkwaySegmentCommand(doc, 10, 20, 1.5, snappedToHandle: 123, prevX: 0, prevY: 0, previousNodeHandle: null);
        cmd.Execute();

        cmd.CreatedNodeHandle.Should().BeNull();
        // Only edge line should be created, no new circle
        doc.ModelSpaceEntities.OfType<Circle>().Should().BeEmpty();
        doc.ModelSpaceEntities.OfType<Line>().Should().ContainSingle();
    }

    [Fact]
    public void Undo_RemovesCreatedEntities()
    {
        var doc = EntityFactory.CreateDocumentModel();

        var cmd = new AddWalkwaySegmentCommand(doc, 10, 20, 1.5, null, 0, 0, null);
        cmd.Execute();
        cmd.Undo();

        doc.ModelSpaceEntities.OfType<Circle>().Should().BeEmpty();
        doc.ModelSpaceEntities.OfType<Line>().Should().BeEmpty();
    }
}
