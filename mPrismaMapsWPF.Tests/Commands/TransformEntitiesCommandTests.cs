using ACadSharp.Entities;
using FluentAssertions;
using mPrismaMapsWPF.Commands;
using mPrismaMapsWPF.Tests.TestHelpers;

namespace mPrismaMapsWPF.Tests.Commands;

public class TransformEntitiesCommandTests
{
    [Fact]
    public void Execute_Scale_ScalesEntities()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var line = EntityFactory.CreateLine(0, 0, 10, 0);
        var cmd = new TransformEntitiesCommand(doc, new List<Entity> { line }, 0, 0, 2.0, 2.0);

        cmd.Execute();

        line.StartPoint.X.Should().BeApproximately(0, 0.001);
        line.EndPoint.X.Should().BeApproximately(20, 0.001);
        doc.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void Undo_Scale_ReversesScale()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var line = EntityFactory.CreateLine(0, 0, 10, 10);
        var cmd = new TransformEntitiesCommand(doc, new List<Entity> { line }, 0, 0, 2.0, 3.0);

        cmd.Execute();
        cmd.Undo();

        line.StartPoint.X.Should().BeApproximately(0, 0.001);
        line.StartPoint.Y.Should().BeApproximately(0, 0.001);
        line.EndPoint.X.Should().BeApproximately(10, 0.001);
        line.EndPoint.Y.Should().BeApproximately(10, 0.001);
    }

    [Fact]
    public void Execute_Rotate_RotatesEntities()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var line = EntityFactory.CreateLine(10, 0, 20, 0);
        var cmd = new TransformEntitiesCommand(doc, new List<Entity> { line }, 0, 0, angleRadians: Math.PI / 2);

        cmd.Execute();

        line.StartPoint.X.Should().BeApproximately(0, 0.001);
        line.StartPoint.Y.Should().BeApproximately(10, 0.001);
        line.EndPoint.X.Should().BeApproximately(0, 0.001);
        line.EndPoint.Y.Should().BeApproximately(20, 0.001);
    }

    [Fact]
    public void Undo_Rotate_ReversesRotation()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var line = EntityFactory.CreateLine(10, 0, 20, 0);
        var cmd = new TransformEntitiesCommand(doc, new List<Entity> { line }, 0, 0, angleRadians: Math.PI / 4);

        cmd.Execute();
        cmd.Undo();

        line.StartPoint.X.Should().BeApproximately(10, 0.001);
        line.StartPoint.Y.Should().BeApproximately(0, 0.001);
        line.EndPoint.X.Should().BeApproximately(20, 0.001);
        line.EndPoint.Y.Should().BeApproximately(0, 0.001);
    }

    [Fact]
    public void Execute_MultipleEntities_TransformsAll()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var line = EntityFactory.CreateLine(0, 0, 10, 0);
        var circle = EntityFactory.CreateCircle(20, 0, 5);
        var cmd = new TransformEntitiesCommand(doc, new List<Entity> { line, circle }, 0, 0, 2.0, 2.0);

        cmd.Execute();

        line.EndPoint.X.Should().BeApproximately(20, 0.001);
        circle.Center.X.Should().BeApproximately(40, 0.001);
        circle.Radius.Should().BeApproximately(10, 0.001);
    }

    [Fact]
    public void Description_SingleEntity_Scale()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var line = EntityFactory.CreateLine(0, 0, 10, 10);
        var cmd = new TransformEntitiesCommand(doc, new List<Entity> { line }, 0, 0, 2.0, 2.0);

        cmd.Description.Should().Be("Scale Line");
    }

    [Fact]
    public void Description_MultipleEntities_Rotate()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var entities = new List<Entity>
        {
            EntityFactory.CreateLine(0, 0, 10, 10),
            EntityFactory.CreateCircle(5, 5, 3)
        };
        var cmd = new TransformEntitiesCommand(doc, entities, 0, 0, angleRadians: 1.0);

        cmd.Description.Should().Be("Rotate 2 entities");
    }

    [Fact]
    public void Execute_ScaleAndRotate_AppliesBoth()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var point = EntityFactory.CreatePoint(10, 0);
        var cmd = new TransformEntitiesCommand(doc, new List<Entity> { point }, 0, 0, 2.0, 2.0, Math.PI / 2);

        cmd.Execute();

        // Scale first: (10,0) -> (20,0), then rotate 90 deg: (20,0) -> (0,20)
        point.Location.X.Should().BeApproximately(0, 0.001);
        point.Location.Y.Should().BeApproximately(20, 0.001);
    }

    [Fact]
    public void Undo_ScaleAndRotate_ReversesCorrectly()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var point = EntityFactory.CreatePoint(10, 0);
        var cmd = new TransformEntitiesCommand(doc, new List<Entity> { point }, 0, 0, 2.0, 2.0, Math.PI / 2);

        cmd.Execute();
        cmd.Undo();

        point.Location.X.Should().BeApproximately(10, 0.001);
        point.Location.Y.Should().BeApproximately(0, 0.001);
    }
}
