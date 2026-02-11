using ACadSharp;
using ACadSharp.Entities;
using mPrismaMapsWPF.Services;

namespace mPrismaMapsWPF.Commands;

public class ToggleEntranceCommand : IUndoableCommand
{
    private readonly Circle _circle;
    private readonly Color _oldColor;
    private readonly Color _newColor;

    public string Description => "Toggle entrance";

    public ToggleEntranceCommand(Circle circle)
    {
        _circle = circle;
        _oldColor = circle.Color;
        // Toggle: green (3) = entrance, blue (5) = regular
        _newColor = circle.Color.Index == 3 ? new Color(5) : new Color(3);
    }

    public void Execute()
    {
        _circle.Color = _newColor;
    }

    public void Undo()
    {
        _circle.Color = _oldColor;
    }
}
