using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.Tables;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Services;

namespace mPrismaMapsWPF.Commands;

/// <summary>
/// Command to delete selected entities with undo support.
/// </summary>
public class DeleteEntitiesCommand : IUndoableCommand
{
    private readonly CadDocumentModel _document;
    private readonly IReadOnlyList<EntityModel> _entityModels;
    private readonly List<(Entity Entity, BlockRecord Owner)> _deletedEntities = new();

    public string Description { get; }

    public DeleteEntitiesCommand(
        CadDocumentModel document,
        IEnumerable<EntityModel> entityModels)
    {
        _document = document;
        _entityModels = entityModels.ToList();
        Description = _entityModels.Count == 1
            ? $"Delete {_entityModels[0].TypeName}"
            : $"Delete {_entityModels.Count} entities";
    }

    public void Execute()
    {
        _deletedEntities.Clear();

        foreach (var entityModel in _entityModels)
        {
            var entity = entityModel.Entity;
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
}
