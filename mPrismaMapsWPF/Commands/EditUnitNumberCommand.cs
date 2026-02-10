using ACadSharp.Entities;
using mPrismaMapsWPF.Services;

namespace mPrismaMapsWPF.Commands;

public class EditUnitNumberCommand : IUndoableCommand
{
    private readonly MText _entity;
    private readonly string _oldValue;
    private readonly string _newValue;

    public string Description => $"Edit unit number '{_oldValue}' -> '{_newValue}'";

    public EditUnitNumberCommand(MText entity, string oldValue, string newValue)
    {
        _entity = entity;
        _oldValue = oldValue;
        _newValue = newValue;
    }

    public void Execute()
    {
        _entity.Value = _newValue;
    }

    public void Undo()
    {
        _entity.Value = _oldValue;
    }
}
