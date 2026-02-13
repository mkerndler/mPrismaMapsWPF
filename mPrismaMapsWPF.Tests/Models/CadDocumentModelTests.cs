using ACadSharp;
using FluentAssertions;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Tests.TestHelpers;

namespace mPrismaMapsWPF.Tests.Models;

public class CadDocumentModelTests
{
    [Fact]
    public void Load_SetsDocumentAndFilePath()
    {
        var model = new CadDocumentModel();
        var doc = new CadDocument();
        model.Load(doc, "test.dwg");

        model.Document.Should().Be(doc);
        model.FilePath.Should().Be("test.dwg");
        model.IsDirty.Should().BeFalse();
    }

    [Fact]
    public void Clear_NullsDocumentAndFilePath()
    {
        var model = EntityFactory.CreateDocumentModel();
        model.Clear();

        model.Document.Should().BeNull();
        model.FilePath.Should().BeNull();
        model.IsDirty.Should().BeFalse();
    }

    [Fact]
    public void ModelSpaceEntities_EmptyWhenNoDocument()
    {
        var model = new CadDocumentModel();
        model.ModelSpaceEntities.Should().BeEmpty();
    }

    [Fact]
    public void GetOrCreateUserDrawingsLayer_CreatesOnFirstCall()
    {
        var model = EntityFactory.CreateDocumentModel();
        var layer = model.GetOrCreateUserDrawingsLayer();

        layer.Should().NotBeNull();
        layer!.Name.Should().Be(CadDocumentModel.UserDrawingsLayerName);
    }

    [Fact]
    public void GetOrCreateUserDrawingsLayer_ReusesOnSecondCall()
    {
        var model = EntityFactory.CreateDocumentModel();
        var layer1 = model.GetOrCreateUserDrawingsLayer();
        var layer2 = model.GetOrCreateUserDrawingsLayer();

        layer1.Should().BeSameAs(layer2);
    }

    [Fact]
    public void GetOrCreateWalkwaysLayer_CreatesLayer()
    {
        var model = EntityFactory.CreateDocumentModel();
        var layer = model.GetOrCreateWalkwaysLayer();

        layer.Should().NotBeNull();
        layer!.Name.Should().Be(CadDocumentModel.WalkwaysLayerName);
    }

    [Fact]
    public void EnsureDocumentExists_CreatesNewDocument()
    {
        var model = new CadDocumentModel();
        model.EnsureDocumentExists();

        model.Document.Should().NotBeNull();
        model.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void EnsureDocumentExists_DoesNotReplaceExisting()
    {
        var model = EntityFactory.CreateDocumentModel();
        var originalDoc = model.Document;
        model.EnsureDocumentExists();

        model.Document.Should().BeSameAs(originalDoc);
    }

    [Fact]
    public void GetExtents_ReturnsInvalid_WhenNoEntities()
    {
        var model = EntityFactory.CreateDocumentModel();
        var extents = model.GetExtents();
        extents.IsValid.Should().BeFalse();
    }

    [Fact]
    public void GetExtents_ComputesBoundingBox()
    {
        var model = EntityFactory.CreateDocumentModel();
        var line = EntityFactory.CreateLine(0, 0, 100, 50);
        model.Document!.ModelSpace.Entities.Add(line);

        var extents = model.GetExtents();

        extents.IsValid.Should().BeTrue();
        extents.MinX.Should().Be(0);
        extents.MinY.Should().Be(0);
        extents.MaxX.Should().Be(100);
        extents.MaxY.Should().Be(50);
        extents.Width.Should().Be(100);
        extents.Height.Should().Be(50);
    }

    [Fact]
    public void Layers_EmptyWhenNoDocument()
    {
        var model = new CadDocumentModel();
        model.Layers.Should().BeEmpty();
    }

    [Fact]
    public void GetOrCreateLayer_ReturnsNull_WhenNoDocument()
    {
        var model = new CadDocumentModel();
        model.GetOrCreateUserDrawingsLayer().Should().BeNull();
    }
}
