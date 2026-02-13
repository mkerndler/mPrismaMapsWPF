using ACadSharp.Tables;
using FluentAssertions;
using mPrismaMapsWPF.Commands;
using mPrismaMapsWPF.Tests.TestHelpers;

namespace mPrismaMapsWPF.Tests.Commands;

public class ChangeEntityLayerCommandTests
{
    [Fact]
    public void Execute_ChangesEntityLayer()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var line = EntityFactory.CreateLine(0, 0, 10, 10);
        var targetLayer = new Layer("Target");
        doc.Document!.Layers.Add(targetLayer);
        var entityModel = EntityFactory.CreateEntityModel(line);

        var cmd = new ChangeEntityLayerCommand(doc, new[] { entityModel }, targetLayer);
        cmd.Execute();

        line.Layer.Should().Be(targetLayer);
        doc.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void Undo_RestoresOriginalLayer()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var originalLayer = new Layer("Original");
        doc.Document!.Layers.Add(originalLayer);
        var line = EntityFactory.CreateLine(0, 0, 10, 10, originalLayer);
        var targetLayer = new Layer("Target");
        doc.Document!.Layers.Add(targetLayer);
        var entityModel = EntityFactory.CreateEntityModel(line);

        var cmd = new ChangeEntityLayerCommand(doc, new[] { entityModel }, targetLayer);
        cmd.Execute();
        cmd.Undo();

        line.Layer.Should().Be(originalLayer);
    }

    [Fact]
    public void Execute_MultipleEntities_ChangesAll()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var targetLayer = new Layer("Target");
        doc.Document!.Layers.Add(targetLayer);
        var line1 = EntityFactory.CreateLine(0, 0, 10, 10);
        var line2 = EntityFactory.CreateLine(20, 20, 30, 30);

        var cmd = new ChangeEntityLayerCommand(doc,
            new[] { EntityFactory.CreateEntityModel(line1), EntityFactory.CreateEntityModel(line2) },
            targetLayer);
        cmd.Execute();

        line1.Layer.Should().Be(targetLayer);
        line2.Layer.Should().Be(targetLayer);
    }
}
