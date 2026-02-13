using FluentAssertions;
using mPrismaMapsWPF.Commands;
using mPrismaMapsWPF.Tests.TestHelpers;

namespace mPrismaMapsWPF.Tests.Commands;

public class EditUnitNumberCommandTests
{
    [Fact]
    public void Execute_ChangesMTextValue()
    {
        var mtext = EntityFactory.CreateMText(5, 5, "101");

        var cmd = new EditUnitNumberCommand(mtext, "101", "202");
        cmd.Execute();

        mtext.Value.Should().Be("202");
    }

    [Fact]
    public void Undo_RestoresOriginalValue()
    {
        var mtext = EntityFactory.CreateMText(5, 5, "101");

        var cmd = new EditUnitNumberCommand(mtext, "101", "202");
        cmd.Execute();
        cmd.Undo();

        mtext.Value.Should().Be("101");
    }

    [Fact]
    public void Description_ContainsOldAndNewValues()
    {
        var mtext = EntityFactory.CreateMText(5, 5, "101");
        var cmd = new EditUnitNumberCommand(mtext, "101", "202");

        cmd.Description.Should().Contain("101");
        cmd.Description.Should().Contain("202");
    }
}
