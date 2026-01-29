using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.Tables;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Services;

namespace mPrismaMapsWPF.Commands;

/// <summary>
/// Specifies how to handle entities when deleting a layer.
/// </summary>
public enum LayerDeleteOption
{
    /// <summary>
    /// Delete all entities on the layer.
    /// </summary>
    DeleteEntities,

    /// <summary>
    /// Reassign entities to another layer.
    /// </summary>
    ReassignEntities
}

/// <summary>
/// Command to delete a layer with undo support.
/// </summary>
public class DeleteLayerCommand : IUndoableCommand
{
    private readonly CadDocumentModel _document;
    private readonly Layer _layer;
    private readonly LayerDeleteOption _option;
    private readonly Layer? _targetLayer;

    private readonly List<(Entity Entity, Layer OriginalLayer)> _movedEntities = new();
    private readonly List<(Entity Entity, BlockRecord Owner)> _deletedEntities = new();

    public string Description { get; }

    /// <summary>
    /// Creates a DeleteLayerCommand.
    /// </summary>
    /// <param name="document">The CAD document model.</param>
    /// <param name="layer">The layer to delete.</param>
    /// <param name="option">How to handle entities on the layer.</param>
    /// <param name="targetLayer">Target layer for reassignment (required if option is ReassignEntities).</param>
    public DeleteLayerCommand(
        CadDocumentModel document,
        Layer layer,
        LayerDeleteOption option,
        Layer? targetLayer = null)
    {
        _document = document;
        _layer = layer;
        _option = option;
        _targetLayer = targetLayer;

        if (option == LayerDeleteOption.ReassignEntities && targetLayer == null)
            throw new ArgumentException("Target layer is required for reassignment.", nameof(targetLayer));

        Description = option == LayerDeleteOption.DeleteEntities
            ? $"Delete layer '{layer.Name}' with entities"
            : $"Delete layer '{layer.Name}' (reassign to '{targetLayer?.Name}')";
    }

    public void Execute()
    {
        if (_document.Document == null)
            return;

        _movedEntities.Clear();
        _deletedEntities.Clear();

        // Find all entities on this layer
        var entitiesOnLayer = _document.ModelSpaceEntities
            .Where(e => e.Layer == _layer)
            .ToList();

        if (_option == LayerDeleteOption.DeleteEntities)
        {
            foreach (var entity in entitiesOnLayer)
            {
                var owner = entity.Owner as BlockRecord;
                if (owner != null)
                {
                    _deletedEntities.Add((entity, owner));
                    owner.Entities.Remove(entity);
                }
            }
        }
        else if (_option == LayerDeleteOption.ReassignEntities && _targetLayer != null)
        {
            foreach (var entity in entitiesOnLayer)
            {
                _movedEntities.Add((entity, _layer));
                entity.Layer = _targetLayer;
            }
        }

        // Remove the layer from the document
        _document.Document.Layers.Remove(_layer.Name);
        _document.IsDirty = true;
    }

    public void Undo()
    {
        if (_document.Document == null)
            return;

        // Re-add the layer
        _document.Document.Layers.Add(_layer);

        // Restore entities
        if (_option == LayerDeleteOption.DeleteEntities)
        {
            foreach (var (entity, owner) in _deletedEntities)
            {
                owner.Entities.Add(entity);
                entity.Layer = _layer;
            }
        }
        else if (_option == LayerDeleteOption.ReassignEntities)
        {
            foreach (var (entity, originalLayer) in _movedEntities)
            {
                entity.Layer = originalLayer;
            }
        }

        _document.IsDirty = true;
    }

    public Layer Layer => _layer;
    public int AffectedEntitiesCount => _option == LayerDeleteOption.DeleteEntities
        ? _deletedEntities.Count
        : _movedEntities.Count;
}
