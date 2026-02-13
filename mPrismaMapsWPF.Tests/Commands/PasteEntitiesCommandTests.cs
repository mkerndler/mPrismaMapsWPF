using ACadSharp.Entities;
using FluentAssertions;
using mPrismaMapsWPF.Commands;
using mPrismaMapsWPF.Tests.TestHelpers;

namespace mPrismaMapsWPF.Tests.Commands;

public class PasteEntitiesCommandTests
{
    [Fact]
    public void Execute_AddsEntitiesToModelSpace()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var line = EntityFactory.CreateLine(0, 0, 10, 10);

        var cmd = new PasteEntitiesCommand(doc, new List<Entity> { line });
        cmd.Execute();

        doc.ModelSpaceEntities.Should().Contain(line);
        doc.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void Undo_RemovesPastedEntities()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var line = EntityFactory.CreateLine(0, 0, 10, 10);

        var cmd = new PasteEntitiesCommand(doc, new List<Entity> { line });
        cmd.Execute();
        cmd.Undo();

        doc.ModelSpaceEntities.Should().NotContain(line);
    }

    [Fact]
    public void Execute_MultipleEntities_AddsAll()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var line = EntityFactory.CreateLine(0, 0, 10, 10);
        var circle = EntityFactory.CreateCircle(5, 5, 3);

        var cmd = new PasteEntitiesCommand(doc, new List<Entity> { line, circle });
        cmd.Execute();

        doc.ModelSpaceEntities.Should().Contain(line);
        doc.ModelSpaceEntities.Should().Contain(circle);
    }

    [Fact]
    public void Description_SingleEntity_ContainsTypeName()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var line = EntityFactory.CreateLine(0, 0, 10, 10);
        var cmd = new PasteEntitiesCommand(doc, new List<Entity> { line });
        cmd.Description.Should().Contain("Line");
    }
}
