using ACadSharp;
using FluentAssertions;
using mPrismaMapsWPF.Commands;
using mPrismaMapsWPF.Tests.TestHelpers;

namespace mPrismaMapsWPF.Tests.Commands;

public class ChangeEntityColorCommandTests
{
    [Fact]
    public void Execute_ChangesEntityColor()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var line = EntityFactory.CreateLine(0, 0, 10, 10);
        line.Color = new Color(1); // Red
        var entityModel = EntityFactory.CreateEntityModel(line);

        var cmd = new ChangeEntityColorCommand(doc, new[] { entityModel }, new Color(5)); // Blue
        cmd.Execute();

        line.Color.Index.Should().Be(5);
        doc.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void Undo_RestoresOriginalColor()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var line = EntityFactory.CreateLine(0, 0, 10, 10);
        line.Color = new Color(1);
        var entityModel = EntityFactory.CreateEntityModel(line);

        var cmd = new ChangeEntityColorCommand(doc, new[] { entityModel }, new Color(5));
        cmd.Execute();
        cmd.Undo();

        line.Color.Index.Should().Be(1);
    }

    [Fact]
    public void Execute_MultipleEntities_ChangesAll()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var line1 = EntityFactory.CreateLine(0, 0, 10, 10);
        var line2 = EntityFactory.CreateLine(20, 20, 30, 30);
        line1.Color = new Color(1);
        line2.Color = new Color(2);

        var cmd = new ChangeEntityColorCommand(doc,
            new[] { EntityFactory.CreateEntityModel(line1), EntityFactory.CreateEntityModel(line2) },
            new Color(3));
        cmd.Execute();

        line1.Color.Index.Should().Be(3);
        line2.Color.Index.Should().Be(3);
    }
}
