using ACadSharp.Tables;
using FluentAssertions;
using mPrismaMapsWPF.Commands;
using mPrismaMapsWPF.Tests.TestHelpers;

namespace mPrismaMapsWPF.Tests.Commands;

public class DeleteHiddenEntitiesCommandTests
{
    [Fact]
    public void Execute_RemovesEntitiesOnHiddenLayers()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var hiddenLayer = new Layer("Hidden");
        doc.Document!.Layers.Add(hiddenLayer);
        var line = EntityFactory.CreateLine(0, 0, 10, 10, hiddenLayer);
        doc.Document!.ModelSpace.Entities.Add(line);

        var cmd = new DeleteHiddenEntitiesCommand(doc, new[] { "Hidden" });
        cmd.Execute();

        doc.ModelSpaceEntities.Should().NotContain(line);
        cmd.DeletedCount.Should().Be(1);
        cmd.EntitiesOnHiddenLayers.Should().Be(1);
        doc.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void Execute_KeepsEntitiesOnVisibleLayers()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var visibleLayer = new Layer("Visible");
        doc.Document!.Layers.Add(visibleLayer);
        var line = EntityFactory.CreateLine(0, 0, 10, 10, visibleLayer);
        doc.Document!.ModelSpace.Entities.Add(line);

        var cmd = new DeleteHiddenEntitiesCommand(doc, new[] { "Hidden" });
        cmd.Execute();

        doc.ModelSpaceEntities.Should().Contain(line);
        cmd.DeletedCount.Should().Be(0);
    }

    [Fact]
    public void Undo_RestoresDeletedEntities()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var hiddenLayer = new Layer("Hidden");
        doc.Document!.Layers.Add(hiddenLayer);
        var line = EntityFactory.CreateLine(0, 0, 10, 10, hiddenLayer);
        doc.Document!.ModelSpace.Entities.Add(line);

        var cmd = new DeleteHiddenEntitiesCommand(doc, new[] { "Hidden" });
        cmd.Execute();
        cmd.Undo();

        doc.ModelSpaceEntities.Should().Contain(line);
    }
}
