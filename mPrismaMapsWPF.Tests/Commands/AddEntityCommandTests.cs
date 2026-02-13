using ACadSharp.Entities;
using FluentAssertions;
using mPrismaMapsWPF.Commands;
using mPrismaMapsWPF.Tests.TestHelpers;

namespace mPrismaMapsWPF.Tests.Commands;

public class AddEntityCommandTests
{
    [Fact]
    public void Execute_AddsEntityToModelSpace()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var line = EntityFactory.CreateLine(0, 0, 10, 10);
        var cmd = new AddEntityCommand(doc, line);

        cmd.Execute();

        doc.ModelSpaceEntities.Should().Contain(line);
        doc.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void Undo_RemovesEntityFromModelSpace()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var line = EntityFactory.CreateLine(0, 0, 10, 10);
        var cmd = new AddEntityCommand(doc, line);

        cmd.Execute();
        cmd.Undo();

        doc.ModelSpaceEntities.Should().NotContain(line);
    }

    [Fact]
    public void Description_DefaultsToEntityTypeName()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var line = EntityFactory.CreateLine(0, 0, 10, 10);
        var cmd = new AddEntityCommand(doc, line);

        cmd.Description.Should().Be("Add Line");
    }

    [Fact]
    public void Description_UsesCustomDescription()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var line = EntityFactory.CreateLine(0, 0, 10, 10);
        var cmd = new AddEntityCommand(doc, line, "Custom add");

        cmd.Description.Should().Be("Custom add");
    }
}
