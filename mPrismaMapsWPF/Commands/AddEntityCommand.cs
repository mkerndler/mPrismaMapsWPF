using ACadSharp.Entities;
using ACadSharp.Tables;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Services;

namespace mPrismaMapsWPF.Commands;

/// <summary>
/// Command to add an entity to the document with undo support.
/// </summary>
public class AddEntityCommand : IUndoableCommand
{
    private readonly CadDocumentModel _document;
    private readonly Entity _entity;
    private BlockRecord? _owner;

    public string Description { get; }

    public AddEntityCommand(CadDocumentModel document, Entity entity, string? description = null)
    {
        _document = document;
        _entity = entity;
        Description = description ?? $"Add {entity.GetType().Name}";
    }

    public void Execute()
    {
        if (_document.Document == null)
            return;

        // Get the model space block record
        _owner = _document.Document.ModelSpace;
        _owner.Entities.Add(_entity);
        _document.IsDirty = true;
    }

    public void Undo()
    {
        if (_owner == null)
            return;

        _owner.Entities.Remove(_entity);
        _document.IsDirty = true;
    }
}
