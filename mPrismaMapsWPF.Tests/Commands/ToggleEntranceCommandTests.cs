using ACadSharp;
using FluentAssertions;
using mPrismaMapsWPF.Commands;
using mPrismaMapsWPF.Tests.TestHelpers;

namespace mPrismaMapsWPF.Tests.Commands;

public class ToggleEntranceCommandTests
{
    [Fact]
    public void Execute_GreenToBlue_TogglesColor()
    {
        var circle = EntityFactory.CreateCircle(5, 5, 1);
        circle.Color = new Color(3); // green = entrance

        var cmd = new ToggleEntranceCommand(circle);
        cmd.Execute();

        circle.Color.Index.Should().Be(5); // blue = regular
    }

    [Fact]
    public void Execute_BlueToGreen_TogglesColor()
    {
        var circle = EntityFactory.CreateCircle(5, 5, 1);
        circle.Color = new Color(5); // blue = regular

        var cmd = new ToggleEntranceCommand(circle);
        cmd.Execute();

        circle.Color.Index.Should().Be(3); // green = entrance
    }

    [Fact]
    public void Undo_RestoresOriginalColor()
    {
        var circle = EntityFactory.CreateCircle(5, 5, 1);
        circle.Color = new Color(3);

        var cmd = new ToggleEntranceCommand(circle);
        cmd.Execute();
        cmd.Undo();

        circle.Color.Index.Should().Be(3);
    }

    [Fact]
    public void Description_IsToggleEntrance()
    {
        var circle = EntityFactory.CreateCircle(5, 5, 1);
        circle.Color = new Color(3);
        var cmd = new ToggleEntranceCommand(circle);
        cmd.Description.Should().Be("Toggle entrance");
    }
}
