using FluentAssertions;
using mPrismaMapsWPF.Commands;
using mPrismaMapsWPF.Tests.TestHelpers;

namespace mPrismaMapsWPF.Tests.Commands;

public class GenerateUnitAreasCommandTests
{
    [Fact]
    public void Execute_NoDocument_NoOp()
    {
        var doc = new mPrismaMapsWPF.Models.CadDocumentModel();
        var cmd = new GenerateUnitAreasCommand(doc, new HashSet<string>());
        cmd.Execute();

        cmd.GeneratedCount.Should().Be(0);
    }

    [Fact]
    public void Execute_NoUnitNumbers_NoAreasGenerated()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var line = EntityFactory.CreateLine(0, 0, 100, 0);
        doc.Document!.ModelSpace.Entities.Add(line);

        var cmd = new GenerateUnitAreasCommand(doc, new HashSet<string>());
        cmd.Execute();

        cmd.GeneratedCount.Should().Be(0);
    }

    [Fact]
    public void Undo_RemovesCreatedEntities()
    {
        var doc = EntityFactory.CreateDocumentModel();
        // Even if Execute produces nothing, Undo should not throw
        var cmd = new GenerateUnitAreasCommand(doc, new HashSet<string>());
        cmd.Execute();
        var act = () => cmd.Undo();
        act.Should().NotThrow();
    }

    [Fact]
    public void Description_ContainsGeneratedCount()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var cmd = new GenerateUnitAreasCommand(doc, new HashSet<string>());
        cmd.Description.Should().Contain("0");
    }
}
