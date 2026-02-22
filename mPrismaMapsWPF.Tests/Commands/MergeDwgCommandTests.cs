using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.Tables;
using FluentAssertions;
using Moq;
using mPrismaMapsWPF.Commands;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Services;
using mPrismaMapsWPF.Tests.TestHelpers;

namespace mPrismaMapsWPF.Tests.Commands;

public class MergeDwgCommandTests
{
    private static MergeResult EmptyResult() => new MergeResult
    {
        AddedEntities  = [],
        AddedLayers    = [],
        AddedBlocks    = [],
        UpdatedLayers  = [],
        EntitiesSkipped = 0,
        LayerConflictsResolved = 0,
        BlockConflictsResolved = 0,
    };

    [Fact]
    public void Execute_CallsMergeService()
    {
        var doc      = EntityFactory.CreateDocumentModel();
        var secondary = new CadDocument();
        var options  = new MergeOptions();
        var mock     = new Mock<IMergeDocumentService>();
        mock.Setup(s => s.Merge(It.IsAny<CadDocument>(), secondary, options))
            .Returns(EmptyResult());

        var cmd = new MergeDwgCommand(doc, secondary, options, mock.Object);
        cmd.Execute();

        mock.Verify(s => s.Merge(doc.Document!, secondary, options), Times.Once);
    }

    [Fact]
    public void Execute_MarksDocumentDirty()
    {
        var doc      = EntityFactory.CreateDocumentModel();
        var secondary = new CadDocument();
        var mock     = new Mock<IMergeDocumentService>();
        mock.Setup(s => s.Merge(It.IsAny<CadDocument>(), It.IsAny<CadDocument>(), It.IsAny<MergeOptions>()))
            .Returns(EmptyResult());

        var cmd = new MergeDwgCommand(doc, secondary, new MergeOptions(), mock.Object);
        cmd.Execute();

        doc.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void Execute_DoesNothing_WhenDocumentIsNull()
    {
        var doc      = new CadDocumentModel();   // no document loaded
        var secondary = new CadDocument();
        var mock     = new Mock<IMergeDocumentService>();

        var cmd = new MergeDwgCommand(doc, secondary, new MergeOptions(), mock.Object);
        var act = () => cmd.Execute();

        act.Should().NotThrow();
        mock.Verify(s => s.Merge(It.IsAny<CadDocument>(), It.IsAny<CadDocument>(), It.IsAny<MergeOptions>()), Times.Never);
    }

    [Fact]
    public void Undo_RemovesAddedEntitiesFromModelSpace()
    {
        var doc      = EntityFactory.CreateDocumentModel();
        var line     = EntityFactory.CreateLine(0, 0, 10, 10);
        var secondary = new CadDocument();

        var mergeResult = new MergeResult
        {
            AddedEntities  = [line],
            AddedLayers    = [],
            AddedBlocks    = [],
            UpdatedLayers  = [],
            EntitiesSkipped = 0,
            LayerConflictsResolved = 0,
            BlockConflictsResolved = 0,
        };

        // Simulate: the merge service added the line to the primary's model space.
        doc.Document!.ModelSpace.Entities.Add(line);

        var mock = new Mock<IMergeDocumentService>();
        mock.Setup(s => s.Merge(It.IsAny<CadDocument>(), It.IsAny<CadDocument>(), It.IsAny<MergeOptions>()))
            .Returns(mergeResult);

        var cmd = new MergeDwgCommand(doc, secondary, new MergeOptions(), mock.Object);
        cmd.Execute();
        cmd.Undo();

        doc.ModelSpaceEntities.Should().NotContain(line);
        doc.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void Undo_RemovesAddedLayersFromDocument()
    {
        var doc      = EntityFactory.CreateDocumentModel();
        var newLayer = new Layer("MergedLayer");
        var secondary = new CadDocument();

        // Simulate the merge service added this layer
        doc.Document!.Layers.Add(newLayer);

        var mergeResult = new MergeResult
        {
            AddedEntities  = [],
            AddedLayers    = [newLayer],
            AddedBlocks    = [],
            UpdatedLayers  = [],
            EntitiesSkipped = 0,
            LayerConflictsResolved = 0,
            BlockConflictsResolved = 0,
        };

        var mock = new Mock<IMergeDocumentService>();
        mock.Setup(s => s.Merge(It.IsAny<CadDocument>(), It.IsAny<CadDocument>(), It.IsAny<MergeOptions>()))
            .Returns(mergeResult);

        var cmd = new MergeDwgCommand(doc, secondary, new MergeOptions(), mock.Object);
        cmd.Execute();
        cmd.Undo();

        doc.Document.Layers.Any(l => l.Name == "MergedLayer").Should().BeFalse();
    }

    [Fact]
    public void Undo_RestoresUpdatedLayerColors()
    {
        var doc          = EntityFactory.CreateDocumentModel();
        var layer        = new Layer("Shared") { Color = new ACadSharp.Color(1) };
        var originalColor = new ACadSharp.Color(1); // red
        doc.Document!.Layers.Add(layer);

        // Simulate: merge overwrote the layer's color
        layer.Color = new ACadSharp.Color(3); // green

        var mergeResult = new MergeResult
        {
            AddedEntities  = [],
            AddedLayers    = [],
            AddedBlocks    = [],
            UpdatedLayers  = [(layer, originalColor)],
            EntitiesSkipped = 0,
            LayerConflictsResolved = 1,
            BlockConflictsResolved = 0,
        };

        var secondary = new CadDocument();
        var mock = new Mock<IMergeDocumentService>();
        mock.Setup(s => s.Merge(It.IsAny<CadDocument>(), It.IsAny<CadDocument>(), It.IsAny<MergeOptions>()))
            .Returns(mergeResult);

        var cmd = new MergeDwgCommand(doc, secondary, new MergeOptions(), mock.Object);
        cmd.Execute();
        cmd.Undo();

        // Color should be restored to original red
        doc.Document.Layers.First(l => l.Name == "Shared").Color.Index.Should().Be(1);
    }

    [Fact]
    public void Description_IsFixed()
    {
        var doc  = EntityFactory.CreateDocumentModel();
        var mock = new Mock<IMergeDocumentService>();

        var cmd = new MergeDwgCommand(doc, new CadDocument(), new MergeOptions(), mock.Object);

        cmd.Description.Should().Be("Merge DWG/DXF");
    }
}
