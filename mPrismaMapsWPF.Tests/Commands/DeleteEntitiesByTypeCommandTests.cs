using ACadSharp.Entities;
using FluentAssertions;
using mPrismaMapsWPF.Commands;
using mPrismaMapsWPF.Tests.TestHelpers;

namespace mPrismaMapsWPF.Tests.Commands;

public class DeleteEntitiesByTypeCommandTests
{
    [Fact]
    public void Execute_DeletesOnlyEntitiesOfSpecifiedType()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var line = EntityFactory.CreateLine(0, 0, 10, 10);
        var circle = EntityFactory.CreateCircle(5, 5, 3);
        doc.Document!.ModelSpace.Entities.Add(line);
        doc.Document!.ModelSpace.Entities.Add(circle);

        var cmd = new DeleteEntitiesByTypeCommand(doc, typeof(Line));
        cmd.Execute();

        doc.ModelSpaceEntities.Should().NotContain(line);
        doc.ModelSpaceEntities.Should().Contain(circle);
        cmd.DeletedCount.Should().Be(1);
    }

    [Fact]
    public void Undo_RestoresDeletedEntities()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var line = EntityFactory.CreateLine(0, 0, 10, 10);
        doc.Document!.ModelSpace.Entities.Add(line);

        var cmd = new DeleteEntitiesByTypeCommand(doc, typeof(Line));
        cmd.Execute();
        cmd.Undo();

        doc.ModelSpaceEntities.Should().Contain(line);
    }

    [Fact]
    public void Execute_NoMatchingEntities_NoOpWithZeroCount()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var circle = EntityFactory.CreateCircle(5, 5, 3);
        doc.Document!.ModelSpace.Entities.Add(circle);

        var cmd = new DeleteEntitiesByTypeCommand(doc, typeof(Line));
        cmd.Execute();

        cmd.DeletedCount.Should().Be(0);
        doc.ModelSpaceEntities.Should().Contain(circle);
    }

    [Fact]
    public void Description_ContainsTypeName()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var cmd = new DeleteEntitiesByTypeCommand(doc, typeof(Line));
        cmd.Description.Should().Contain("Line");
    }
}
