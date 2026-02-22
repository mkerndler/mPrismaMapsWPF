using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.Tables;
using CSMath;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Services;
using mPrismaMapsWPF.Tests.TestHelpers;

namespace mPrismaMapsWPF.Tests.Services;

public class MergeDocumentServiceTests
{
    private readonly MergeDocumentService _service = new(NullLogger<MergeDocumentService>.Instance);

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static CadDocument MakePrimary(params Entity[] entities)
    {
        var doc = new CadDocument();
        foreach (var e in entities)
            doc.ModelSpace.Entities.Add(e);
        return doc;
    }

    private static CadDocument MakeSecondary(params Entity[] entities)
    {
        var doc = new CadDocument();
        foreach (var e in entities)
            doc.ModelSpace.Entities.Add(e);
        return doc;
    }

    private static MergeOptions DefaultOptions() => new();

    // ── Entity merging ───────────────────────────────────────────────────────

    [Fact]
    public void Merge_CopiesEntitiesFromSecondaryIntoPrimary()
    {
        var primary   = MakePrimary();
        var secondary = MakeSecondary(
            EntityFactory.CreateLine(0, 0, 10, 10),
            EntityFactory.CreateCircle(5, 5, 3));

        var result = _service.Merge(primary, secondary, DefaultOptions());

        result.AddedEntities.Should().HaveCount(2);
        primary.Entities.Should().HaveCount(2);
    }

    [Fact]
    public void Merge_AppliesOffset_ToAllCopiedEntities()
    {
        var line      = EntityFactory.CreateLine(0, 0, 10, 10);
        var primary   = MakePrimary();
        var secondary = MakeSecondary(line);

        var options = new MergeOptions { OffsetX = 5, OffsetY = -3 };
        _service.Merge(primary, secondary, options);

        var added = primary.Entities.OfType<Line>().Single();
        added.StartPoint.X.Should().Be(5);
        added.StartPoint.Y.Should().Be(-3);
        added.EndPoint.X.Should().Be(15);
        added.EndPoint.Y.Should().Be(7);
    }

    [Fact]
    public void Merge_SkipsUnsupportedEntityTypes_AndCountsThem()
    {
        var primary   = MakePrimary();
        var secondary = MakeSecondary(new Hatch());

        var result = _service.Merge(primary, secondary, DefaultOptions());

        result.EntitiesSkipped.Should().Be(1);
        result.AddedEntities.Should().BeEmpty();
        primary.Entities.Should().BeEmpty();
    }

    [Fact]
    public void Merge_EntityCount_PrimaryEntitiesAreUntouched()
    {
        var existingLine = EntityFactory.CreateLine(100, 100, 200, 200);
        var primary   = MakePrimary(existingLine);
        var secondary = MakeSecondary(EntityFactory.CreateLine(0, 0, 10, 10));

        _service.Merge(primary, secondary, DefaultOptions());

        primary.Entities.Should().Contain(existingLine);
        primary.Entities.Should().HaveCount(2);
    }

    // ── Layer merging ─────────────────────────────────────────────────────────

    [Fact]
    public void Merge_NewLayerFromSecondary_IsAddedToPrimary()
    {
        var newLayer = new Layer("NewLayer") { Color = new Color(3) };
        var secondary = new CadDocument();
        secondary.Layers.Add(newLayer);
        var line = EntityFactory.CreateLine(0, 0, 1, 1);
        line.Layer = newLayer;
        secondary.ModelSpace.Entities.Add(line);

        var primary = new CadDocument();

        var result = _service.Merge(primary, secondary, DefaultOptions());

        result.AddedLayers.Should().HaveCount(1);
        primary.Layers.Any(l => l.Name == "NewLayer").Should().BeTrue();
    }

    [Fact]
    public void Merge_LayerConflict_KeepPrimary_DoesNotChangePrimaryLayer()
    {
        var primaryLayer   = new Layer("Shared") { Color = new Color(1) }; // red
        var secondaryLayer = new Layer("Shared") { Color = new Color(3) }; // green

        var primary = new CadDocument();
        primary.Layers.Add(primaryLayer);

        var secondary = new CadDocument();
        secondary.Layers.Add(secondaryLayer);

        _service.Merge(primary, secondary, new MergeOptions { LayerConflictStrategy = LayerConflictStrategy.KeepPrimary });

        primary.Layers.First(l => l.Name == "Shared").Color.Index.Should().Be(1);
    }

    [Fact]
    public void Merge_LayerConflict_KeepSecondary_UpdatesPrimaryLayerColor()
    {
        var primaryLayer   = new Layer("Shared") { Color = new Color(1) }; // red
        var secondaryLayer = new Layer("Shared") { Color = new Color(3) }; // green

        var primary = new CadDocument();
        primary.Layers.Add(primaryLayer);

        var secondary = new CadDocument();
        secondary.Layers.Add(secondaryLayer);

        var result = _service.Merge(primary, secondary,
            new MergeOptions { LayerConflictStrategy = LayerConflictStrategy.KeepSecondary });

        primary.Layers.First(l => l.Name == "Shared").Color.Index.Should().Be(3);
        result.UpdatedLayers.Should().HaveCount(1);
        result.UpdatedLayers[0].OriginalColor.Index.Should().Be(1);
    }

    [Fact]
    public void Merge_LayerConflict_RenameSecondary_AddsMergedLayer()
    {
        var primaryLayer   = new Layer("Shared") { Color = new Color(1) };
        var secondaryLayer = new Layer("Shared") { Color = new Color(3) };

        var primary = new CadDocument();
        primary.Layers.Add(primaryLayer);

        var secondary = new CadDocument();
        secondary.Layers.Add(secondaryLayer);

        var result = _service.Merge(primary, secondary,
            new MergeOptions { LayerConflictStrategy = LayerConflictStrategy.RenameSecondary });

        result.AddedLayers.Should().HaveCount(1);
        result.AddedLayers[0].Name.Should().Be("Shared_merged");
        primary.Layers.Any(l => l.Name == "Shared_merged").Should().BeTrue();
        // Original layer unchanged
        primary.Layers.First(l => l.Name == "Shared").Color.Index.Should().Be(1);
    }

    [Fact]
    public void Merge_LayerConflict_ReturnsCorrectConflictCount()
    {
        var primary = new CadDocument();
        primary.Layers.Add(new Layer("A"));
        primary.Layers.Add(new Layer("B"));

        var secondary = new CadDocument();
        secondary.Layers.Add(new Layer("A"));
        secondary.Layers.Add(new Layer("B"));
        secondary.Layers.Add(new Layer("C")); // new — no conflict

        var result = _service.Merge(primary, secondary, DefaultOptions());

        result.LayerConflictsResolved.Should().Be(2);
        result.AddedLayers.Should().HaveCount(1); // only "C" is new
    }

    // ── Block record merging ─────────────────────────────────────────────────

    [Fact]
    public void Merge_NewBlockFromSecondary_IsAddedToPrimary()
    {
        var secondary = new CadDocument();
        var block     = new BlockRecord("MyBlock");
        secondary.BlockRecords.Add(block);

        var primary = new CadDocument();

        var result = _service.Merge(primary, secondary, DefaultOptions());

        result.AddedBlocks.Should().HaveCount(1);
        primary.BlockRecords.Any(b => b.Name == "MyBlock").Should().BeTrue();
    }

    [Fact]
    public void Merge_BlockConflict_RenamesSecondaryBlockWithSuffix()
    {
        var primary = new CadDocument();
        primary.BlockRecords.Add(new BlockRecord("Door"));

        var secondary = new CadDocument();
        secondary.BlockRecords.Add(new BlockRecord("Door"));

        var result = _service.Merge(primary, secondary, DefaultOptions());

        result.BlockConflictsResolved.Should().Be(1);
        result.AddedBlocks.Should().HaveCount(1);
        result.AddedBlocks[0].Name.Should().Be("Door_merged");
    }

    // ── Undo support ─────────────────────────────────────────────────────────

    [Fact]
    public void Merge_Result_ContainsAllAddedEntitiesForUndo()
    {
        var primary   = MakePrimary();
        var secondary = MakeSecondary(
            EntityFactory.CreateLine(0, 0, 1, 1),
            EntityFactory.CreateCircle(5, 5, 2));

        var result = _service.Merge(primary, secondary, DefaultOptions());

        result.AddedEntities.Should().HaveCount(2);
        result.AddedEntities.Should().AllSatisfy(e => primary.Entities.Should().Contain(e));
    }
}
