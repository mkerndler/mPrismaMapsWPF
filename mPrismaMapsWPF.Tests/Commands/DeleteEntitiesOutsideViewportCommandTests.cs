using System.Windows;
using FluentAssertions;
using mPrismaMapsWPF.Commands;
using mPrismaMapsWPF.Helpers;
using mPrismaMapsWPF.Tests.TestHelpers;

namespace mPrismaMapsWPF.Tests.Commands;

[Collection("BoundingBox")]
public class DeleteEntitiesOutsideViewportCommandTests
{
    [Fact]
    public void Execute_KeepsEntitiesInsideViewport()
    {
        BoundingBoxHelper.InvalidateCache();
        var doc = EntityFactory.CreateDocumentModel();
        var line = EntityFactory.CreateLine(5, 5, 10, 10);
        doc.Document!.ModelSpace.Entities.Add(line);

        var viewport = new Rect(0, 0, 20, 20);
        var cmd = new DeleteEntitiesOutsideViewportCommand(doc, viewport);
        cmd.Execute();

        doc.ModelSpaceEntities.Should().Contain(line);
    }

    [Fact]
    public void Execute_DeletesEntitiesOutsideViewport()
    {
        BoundingBoxHelper.InvalidateCache();
        var doc = EntityFactory.CreateDocumentModel();
        var line = EntityFactory.CreateLine(100, 100, 200, 200);
        doc.Document!.ModelSpace.Entities.Add(line);

        var viewport = new Rect(0, 0, 20, 20);
        var cmd = new DeleteEntitiesOutsideViewportCommand(doc, viewport);
        cmd.Execute();

        doc.ModelSpaceEntities.Should().NotContain(line);
        cmd.DeletedCount.Should().Be(1);
        doc.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void Undo_RestoresDeletedEntities()
    {
        BoundingBoxHelper.InvalidateCache();
        var doc = EntityFactory.CreateDocumentModel();
        var line = EntityFactory.CreateLine(100, 100, 200, 200);
        doc.Document!.ModelSpace.Entities.Add(line);

        var viewport = new Rect(0, 0, 20, 20);
        var cmd = new DeleteEntitiesOutsideViewportCommand(doc, viewport);
        cmd.Execute();
        cmd.Undo();

        doc.ModelSpaceEntities.Should().Contain(line);
    }
}
