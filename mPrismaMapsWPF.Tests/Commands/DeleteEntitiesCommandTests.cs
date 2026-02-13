using FluentAssertions;
using mPrismaMapsWPF.Commands;
using mPrismaMapsWPF.Tests.TestHelpers;

namespace mPrismaMapsWPF.Tests.Commands;

public class DeleteEntitiesCommandTests
{
    [Fact]
    public void Execute_RemovesEntities()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var line = EntityFactory.CreateLine(0, 0, 10, 10);
        doc.Document!.ModelSpace.Entities.Add(line);
        var entityModel = EntityFactory.CreateEntityModel(line);

        var cmd = new DeleteEntitiesCommand(doc, new[] { entityModel });
        cmd.Execute();

        doc.ModelSpaceEntities.Should().NotContain(line);
        doc.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void Undo_RestoresEntities()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var line = EntityFactory.CreateLine(0, 0, 10, 10);
        doc.Document!.ModelSpace.Entities.Add(line);
        var entityModel = EntityFactory.CreateEntityModel(line);

        var cmd = new DeleteEntitiesCommand(doc, new[] { entityModel });
        cmd.Execute();
        cmd.Undo();

        doc.ModelSpaceEntities.Should().Contain(line);
    }

    [Fact]
    public void Execute_MultipleEntities_RemovesAll()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var line1 = EntityFactory.CreateLine(0, 0, 10, 10);
        var line2 = EntityFactory.CreateLine(20, 20, 30, 30);
        doc.Document!.ModelSpace.Entities.Add(line1);
        doc.Document!.ModelSpace.Entities.Add(line2);

        var models = new[]
        {
            EntityFactory.CreateEntityModel(line1),
            EntityFactory.CreateEntityModel(line2)
        };

        var cmd = new DeleteEntitiesCommand(doc, models);
        cmd.Execute();

        doc.ModelSpaceEntities.Should().NotContain(line1);
        doc.ModelSpaceEntities.Should().NotContain(line2);
    }

    [Fact]
    public void Description_SingleEntity_UsesTypeName()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var line = EntityFactory.CreateLine(0, 0, 10, 10);
        var cmd = new DeleteEntitiesCommand(doc, new[] { EntityFactory.CreateEntityModel(line) });
        cmd.Description.Should().Contain("Line");
    }
}
