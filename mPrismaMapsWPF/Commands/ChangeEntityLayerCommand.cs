using ACadSharp.Entities;
using ACadSharp.Tables;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Services;

namespace mPrismaMapsWPF.Commands;

/// <summary>
/// Command to change entity layers with undo support.
/// </summary>
public class ChangeEntityLayerCommand : IUndoableCommand
{
    private readonly CadDocumentModel _document;
    private readonly IReadOnlyList<EntityModel> _entityModels;
    private readonly Layer _newLayer;
    private readonly List<(Entity Entity, Layer OriginalLayer)> _originalLayers = new();

    public string Description { get; }

    /// <summary>
    /// Creates a ChangeEntityLayerCommand.
    /// </summary>
    /// <param name="document">The CAD document model.</param>
    /// <param name="entityModels">The entities to move to new layer.</param>
    /// <param name="newLayer">The target layer.</param>
    public ChangeEntityLayerCommand(
        CadDocumentModel document,
        IEnumerable<EntityModel> entityModels,
        Layer newLayer)
    {
        _document = document;
        _entityModels = entityModels.ToList();
        _newLayer = newLayer;

        Description = _entityModels.Count == 1
            ? $"Move {_entityModels[0].TypeName} to layer '{newLayer.Name}'"
            : $"Move {_entityModels.Count} entities to layer '{newLayer.Name}'";
    }

    public void Execute()
    {
        _originalLayers.Clear();

        foreach (var entityModel in _entityModels)
        {
            var entity = entityModel.Entity;
            _originalLayers.Add((entity, entity.Layer));
            entity.Layer = _newLayer;
        }

        _document.IsDirty = true;
    }

    public void Undo()
    {
        foreach (var (entity, originalLayer) in _originalLayers)
        {
            entity.Layer = originalLayer;
        }

        _document.IsDirty = true;
    }
}
