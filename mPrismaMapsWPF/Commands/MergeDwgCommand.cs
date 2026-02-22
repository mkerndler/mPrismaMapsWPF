using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.Tables;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Services;

namespace mPrismaMapsWPF.Commands;

/// <summary>
/// Undoable command that merges all model-space content from a secondary CAD document
/// into the currently open document.
/// </summary>
public class MergeDwgCommand : IUndoableCommand
{
    private readonly CadDocumentModel _document;
    private readonly CadDocument _secondary;
    private readonly MergeOptions _options;
    private readonly IMergeDocumentService _mergeService;

    // Populated by Execute(); used by Undo().
    private List<Entity> _addedEntities = [];
    private List<Layer> _addedLayers = [];
    private List<BlockRecord> _addedBlocks = [];
    private List<(Layer Layer, Color OriginalColor)> _updatedLayers = [];

    public string Description => "Merge DWG/DXF";

    public MergeDwgCommand(
        CadDocumentModel document,
        CadDocument secondary,
        MergeOptions options,
        IMergeDocumentService mergeService)
    {
        _document     = document;
        _secondary    = secondary;
        _options      = options;
        _mergeService = mergeService;
    }

    public void Execute()
    {
        if (_document.Document == null)
            return;

        var result = _mergeService.Merge(_document.Document, _secondary, _options);

        _addedEntities  = result.AddedEntities;
        _addedLayers    = result.AddedLayers;
        _addedBlocks    = result.AddedBlocks;
        _updatedLayers  = result.UpdatedLayers;

        _document.IsDirty = true;
    }

    public void Undo()
    {
        if (_document.Document == null)
            return;

        var modelSpace = _document.Document.ModelSpace;

        // Remove added entities
        foreach (var entity in _addedEntities)
            modelSpace.Entities.Remove(entity);

        // Restore layers that had their color overwritten (KeepSecondary strategy)
        foreach (var (layer, originalColor) in _updatedLayers)
            layer.Color = originalColor;

        // Remove added layers
        foreach (var layer in _addedLayers)
            _document.Document.Layers.Remove(layer.Name);

        // Remove added block records
        foreach (var block in _addedBlocks)
            _document.Document.BlockRecords.Remove(block.Name);

        _document.IsDirty = true;
    }

    /// <summary>
    /// Summary of the last Execute() call for status display.
    /// </summary>
    public string ResultSummary =>
        $"Merged {_addedEntities.Count} entities, {_addedLayers.Count} layers";
}
