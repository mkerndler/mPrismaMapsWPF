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

        // Find all entities that should be deleted:
        // 1. Entities on hidden layers
        // 2. Entities that are individually invisible
        var entitiesToDelete = _document.ModelSpaceEntities
            .Where(e =>
                (e.Layer != null && _hiddenLayerNames.Contains(e.Layer.Name)) ||
                e.IsInvisible)
            .ToList();

        foreach (var entity in entitiesToDelete)
        {
            if (entity.Owner is BlockRecord owner)
            {
                bool isOnHiddenLayer = entity.Layer != null && _hiddenLayerNames.Contains(entity.Layer.Name);
                bool isInvisible = entity.IsInvisible;

                if (isOnHiddenLayer) EntitiesOnHiddenLayers++;
                if (isInvisible) InvisibleEntities++;

                _deletedEntities.Add((entity, owner, isInvisible));
                owner.Entities.Remove(entity);
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
