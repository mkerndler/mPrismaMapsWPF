using ACadSharp.Entities;
using ACadSharp.Tables;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Services;

namespace mPrismaMapsWPF.Commands;

/// <summary>
/// Command that deletes all hidden/invisible entities:
/// - Entities on hidden layers
/// - Entities with IsInvisible = true
/// </summary>
public class DeleteHiddenEntitiesCommand : IUndoableCommand
{
    private readonly CadDocumentModel _document;
    private readonly ISet<string> _hiddenLayerNames;
    private readonly List<(Entity Entity, BlockRecord Owner, bool WasInvisible)> _deletedEntities = new();

    public string Description => $"Delete hidden entities ({_deletedEntities.Count} entities)";
    public int DeletedCount => _deletedEntities.Count;
    public int EntitiesOnHiddenLayers { get; private set; }
    public int InvisibleEntities { get; private set; }

    public DeleteHiddenEntitiesCommand(CadDocumentModel document, IEnumerable<string>? hiddenLayerNames = null)
    {
        _document = document;
        _hiddenLayerNames = hiddenLayerNames?.ToHashSet() ?? new HashSet<string>();
    }

    public void Execute()
    {
        if (_document.Document == null)
            return;

        _deletedEntities.Clear();
        EntitiesOnHiddenLayers = 0;
        InvisibleEntities = 0;

        // Find all entities that should be deleted (hidden layer or individually invisible)
        // that also have a valid owner BlockRecord.
        var entitiesToDelete = _document.ModelSpaceEntities
            .Where(e =>
                e.Owner is BlockRecord &&
                ((e.Layer != null && _hiddenLayerNames.Contains(e.Layer.Name)) || e.IsInvisible))
            .ToList();

        if (entitiesToDelete.Count == 0)
            return;

        // Group by owner and rebuild each owner's entity collection from the survivors.
        // This is O(E) rather than the O(E²) of calling Remove() per entity on a list.
        var toDeleteByOwner = entitiesToDelete
            .GroupBy(e => (BlockRecord)e.Owner!)
            .ToDictionary(g => g.Key, g => g.ToHashSet());

        foreach (var (owner, toDelete) in toDeleteByOwner)
        {
            var toKeep = owner.Entities.Where(e => !toDelete.Contains(e)).ToList();
            owner.Entities.Clear();
            foreach (var e in toKeep)
                owner.Entities.Add(e);
            foreach (var e in toDelete)
            {
                bool isOnHiddenLayer = e.Layer != null && _hiddenLayerNames.Contains(e.Layer.Name);
                bool isInvisible = e.IsInvisible;
                if (isOnHiddenLayer) EntitiesOnHiddenLayers++;
                if (isInvisible) InvisibleEntities++;
                _deletedEntities.Add((e, owner, isInvisible));
            }
        }

        _document.IsDirty = true;
    }

    public void Undo()
    {
        if (_document.Document == null)
            return;

        // Restore deleted entities
        foreach (var (entity, owner, _) in _deletedEntities)
        {
            owner.Entities.Add(entity);
        }

        _document.IsDirty = true;
    }
}
