using ACadSharp.Tables;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Services;

namespace mPrismaMapsWPF.Commands;

/// <summary>
/// Deletes all supplied empty layers in a single undoable operation, so that
/// UndoRedoService.StateChanged (and the resulting RefreshEntities + RebuildSpatialIndex)
/// fires exactly once instead of once per layer.
/// </summary>
public class DeleteEmptyLayersCommand : IUndoableCommand
{
    private readonly List<DeleteLayerCommand> _steps;

    public string Description => $"Delete {_steps.Count} empty layer{(_steps.Count == 1 ? "" : "s")}";
    public int DeletedCount => _steps.Count;

    public DeleteEmptyLayersCommand(CadDocumentModel document, IEnumerable<Layer> emptyLayers)
    {
        _steps = emptyLayers
            .Select(layer => new DeleteLayerCommand(document, layer, LayerDeleteOption.DeleteEntities))
            .ToList();
    }

    public void Execute()
    {
        foreach (var step in _steps)
            step.Execute();
    }

    public void Undo()
    {
        // Undo in reverse order so layers are restored in the original sequence
        foreach (var step in Enumerable.Reverse(_steps))
            step.Undo();
    }
}
