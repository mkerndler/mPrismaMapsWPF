using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.Tables;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Services;

namespace mPrismaMapsWPF.Commands;

/// <summary>
/// Command to delete all entities of a specific type with undo support.
/// </summary>
public class DeleteEntitiesByTypeCommand : IUndoableCommand
{
    private readonly CadDocumentModel _document;
    private readonly Type _entityType;
    private readonly List<(Entity Entity, BlockRecord Owner)> _deletedEntities = new();

    public string Description { get; }

    public DeleteEntitiesByTypeCommand(CadDocumentModel document, Type entityType)
    {
        _document = document;
        _entityType = entityType;
        Description = $"Delete all {entityType.Name}";
    }

    public void Execute()
    {
        _deletedEntities.Clear();

        if (_document.Document == null)
            return;

        // Find all entities of the specified type
        var entitiesToDelete = _document.ModelSpaceEntities
            .Where(e => e.GetType() == _entityType)
            .ToList();

        foreach (var entity in entitiesToDelete)
        {
            var owner = entity.Owner as BlockRecord;
            if (owner != null)
            {
                _deletedEntities.Add((entity, owner));
                owner.Entities.Remove(entity);
            }
        }

        _document.IsDirty = true;
    }

    public void Undo()
    {
        foreach (var (entity, owner) in _deletedEntities)
        {
            owner.Entities.Add(entity);
        }

        _document.IsDirty = true;
    }

    public int DeletedCount => _deletedEntities.Count;
}
