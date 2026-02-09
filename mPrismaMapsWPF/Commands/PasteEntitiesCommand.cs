using ACadSharp.Entities;
using ACadSharp.Tables;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Services;

namespace mPrismaMapsWPF.Commands;

public class PasteEntitiesCommand : IUndoableCommand
{
    private readonly CadDocumentModel _document;
    private readonly IReadOnlyList<Entity> _entities;
    private BlockRecord? _owner;

    public string Description { get; }

    public PasteEntitiesCommand(CadDocumentModel document, IReadOnlyList<Entity> entities)
    {
        _document = document;
        _entities = entities;
        Description = _entities.Count == 1
            ? $"Paste {_entities[0].GetType().Name}"
            : $"Paste {_entities.Count} entities";
    }

    public void Execute()
    {
        if (_document.Document == null)
            return;

        _owner = _document.Document.ModelSpace;
        foreach (var entity in _entities)
        {
            _owner.Entities.Add(entity);
        }
        _document.IsDirty = true;
    }

    public void Undo()
    {
        if (_owner == null)
            return;

        foreach (var entity in _entities)
        {
            _owner.Entities.Remove(entity);
        }
        _document.IsDirty = true;
    }
}
