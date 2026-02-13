using ACadSharp.Entities;
using FluentAssertions;
using mPrismaMapsWPF.Commands;
using mPrismaMapsWPF.Tests.TestHelpers;

namespace mPrismaMapsWPF.Tests.Commands;

public class MoveEntitiesCommandTests
{
    [Fact]
    public void Execute_TranslatesEntities()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var line = EntityFactory.CreateLine(0, 0, 10, 10);
        var cmd = new MoveEntitiesCommand(doc, new List<Entity> { line }, 5, 3);

        cmd.Execute();

        line.StartPoint.X.Should().Be(5);
        line.StartPoint.Y.Should().Be(3);
        line.EndPoint.X.Should().Be(15);
        line.EndPoint.Y.Should().Be(13);
        doc.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void Undo_TranslatesBack()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var line = EntityFactory.CreateLine(0, 0, 10, 10);
        var cmd = new MoveEntitiesCommand(doc, new List<Entity> { line }, 5, 3);

        cmd.Execute();
        cmd.Undo();

        line.StartPoint.X.Should().BeApproximately(0, 0.001);
        line.StartPoint.Y.Should().BeApproximately(0, 0.001);
        line.EndPoint.X.Should().BeApproximately(10, 0.001);
        line.EndPoint.Y.Should().BeApproximately(10, 0.001);
    }

    [Fact]
    public void Execute_MultipleEntities_TranslatesAll()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var line1 = EntityFactory.CreateLine(0, 0, 10, 10);
        var circle = EntityFactory.CreateCircle(20, 20, 5);
        var cmd = new MoveEntitiesCommand(doc, new List<Entity> { line1, circle }, 10, 10);

        cmd.Execute();

        line1.StartPoint.X.Should().Be(10);
        circle.Center.X.Should().Be(30);
        circle.Center.Y.Should().Be(30);
    }
}
