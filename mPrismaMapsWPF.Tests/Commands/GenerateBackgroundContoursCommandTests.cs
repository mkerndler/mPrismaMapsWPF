using FluentAssertions;
using mPrismaMapsWPF.Commands;
using mPrismaMapsWPF.Tests.TestHelpers;

namespace mPrismaMapsWPF.Tests.Commands;

public class GenerateBackgroundContoursCommandTests
{
    [Fact]
    public void Execute_NoDocument_NoOp()
    {
        var doc = new mPrismaMapsWPF.Models.CadDocumentModel();
        var cmd = new GenerateBackgroundContoursCommand(doc, new HashSet<string>());
        cmd.Execute();

        cmd.GeneratedCount.Should().Be(0);
    }

    [Fact]
    public void Execute_NoBackgroundEntities_NoContoursGenerated()
    {
        var doc = EntityFactory.CreateDocumentModel();
        // No entities in model space
        var cmd = new GenerateBackgroundContoursCommand(doc, new HashSet<string>());
        cmd.Execute();

        cmd.GeneratedCount.Should().Be(0);
    }

    [Fact]
    public void Undo_DoesNotThrow()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var cmd = new GenerateBackgroundContoursCommand(doc, new HashSet<string>());
        cmd.Execute();
        var act = () => cmd.Undo();
        act.Should().NotThrow();
    }

    [Fact]
    public void Description_ContainsGeneratedCount()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var cmd = new GenerateBackgroundContoursCommand(doc, new HashSet<string>());
        cmd.Description.Should().Contain("0");
    }
}
