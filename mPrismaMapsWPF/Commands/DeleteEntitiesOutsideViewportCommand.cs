using System.Windows;
using ACadSharp.Entities;
using ACadSharp.Tables;
using mPrismaMapsWPF.Helpers;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Services;

namespace mPrismaMapsWPF.Commands;

/// <summary>
/// Command that deletes all entities outside the current viewport bounds.
/// </summary>
public class DeleteEntitiesOutsideViewportCommand : IUndoableCommand
{
    private readonly CadDocumentModel _document;
    private readonly Rect _viewportBounds;
    private readonly List<(Entity Entity, BlockRecord Owner)> _deletedEntities = new();

    public string Description => $"Delete entities outside viewport ({_deletedEntities.Count} entities)";
    public int DeletedCount => _deletedEntities.Count;

    public DeleteEntitiesOutsideViewportCommand(CadDocumentModel document, Rect viewportBounds)
    {
        _document = document;
        _viewportBounds = viewportBounds;
    }

    public void Execute()
    {
        if (_document.Document == null)
            return;

        _deletedEntities.Clear();

        // Find all entities completely outside the viewport that have a valid owner
        var entitiesToDelete = _document.ModelSpaceEntities
            .Where(e => e.Owner is BlockRecord && IsEntityOutsideViewport(e))
            .ToList();

        if (entitiesToDelete.Count == 0)
            return;

        // Group by owner and rebuild each owner's collection from the survivors.
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
                _deletedEntities.Add((e, owner));
        }

        _document.IsDirty = true;
    }

    private bool IsEntityOutsideViewport(Entity entity)
    {
        var bounds = BoundingBoxHelper.GetBounds(entity);
        if (!bounds.HasValue)
            return false; // Can't determine bounds, keep the entity

        var entityRect = bounds.Value;

        // Check if entity bounds are completely outside viewport
        // Entity is outside if there's no intersection with viewport
        return !_viewportBounds.IntersectsWith(entityRect) &&
               !_viewportBounds.Contains(entityRect) &&
               !entityRect.Contains(_viewportBounds);
    }

    public void Undo()
    {
        if (_document.Document == null)
            return;

        // Restore deleted entities
        foreach (var (entity, owner) in _deletedEntities)
        {
            owner.Entities.Add(entity);
        }

        _document.IsDirty = true;
    }
}
