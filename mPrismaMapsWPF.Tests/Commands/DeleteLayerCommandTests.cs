using ACadSharp.Tables;
using FluentAssertions;
using mPrismaMapsWPF.Commands;
using mPrismaMapsWPF.Tests.TestHelpers;

namespace mPrismaMapsWPF.Tests.Commands;

public class DeleteLayerCommandTests
{
    [Fact]
    public void Execute_DeleteEntitiesOption_RemovesLayerAndEntities()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var layer = new Layer("TestLayer");
        doc.Document!.Layers.Add(layer);
        var line = EntityFactory.CreateLine(0, 0, 10, 10, layer);
        doc.Document!.ModelSpace.Entities.Add(line);

        var cmd = new DeleteLayerCommand(doc, layer, LayerDeleteOption.DeleteEntities);
        cmd.Execute();

        doc.ModelSpaceEntities.Should().NotContain(line);
        doc.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void Execute_ReassignEntities_MovesToTargetLayer()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var sourceLayer = new Layer("Source");
        var targetLayer = new Layer("Target");
        doc.Document!.Layers.Add(sourceLayer);
        doc.Document!.Layers.Add(targetLayer);
        var line = EntityFactory.CreateLine(0, 0, 10, 10, sourceLayer);
        doc.Document!.ModelSpace.Entities.Add(line);

        var cmd = new DeleteLayerCommand(doc, sourceLayer, LayerDeleteOption.ReassignEntities, targetLayer);
        cmd.Execute();

        line.Layer.Should().Be(targetLayer);
    }

    [Fact]
    public void Undo_RestoresLayerAndEntities()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var layer = new Layer("TestLayer");
        doc.Document!.Layers.Add(layer);
        var line = EntityFactory.CreateLine(0, 0, 10, 10, layer);
        doc.Document!.ModelSpace.Entities.Add(line);

        var cmd = new DeleteLayerCommand(doc, layer, LayerDeleteOption.DeleteEntities);
        cmd.Execute();
        cmd.Undo();

        doc.ModelSpaceEntities.Should().Contain(line);
    }

    [Fact]
    public void Constructor_ReassignWithoutTarget_Throws()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var layer = new Layer("TestLayer");

        var act = () => new DeleteLayerCommand(doc, layer, LayerDeleteOption.ReassignEntities, null);
        act.Should().Throw<ArgumentException>();
    }
}
