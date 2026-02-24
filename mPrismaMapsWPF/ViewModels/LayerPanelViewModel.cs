using System.Collections.ObjectModel;
using ACadSharp.Tables;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using mPrismaMapsWPF.Commands;
using mPrismaMapsWPF.Helpers;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Services;

namespace mPrismaMapsWPF.ViewModels;

public partial class LayerPanelViewModel : ObservableObject, IDisposable
{
    private readonly IDocumentService _documentService;
    private readonly IUndoRedoService _undoRedoService;

    public LayerPanelViewModel(IDocumentService documentService, IUndoRedoService undoRedoService)
    {
        _documentService = documentService;
        _undoRedoService = undoRedoService;
        _documentService.DocumentLoaded += OnDocumentLoaded;
        _documentService.DocumentClosed += OnDocumentClosed;
    }

    public void Dispose()
    {
        _documentService.DocumentLoaded -= OnDocumentLoaded;
        _documentService.DocumentClosed -= OnDocumentClosed;
        UnsubscribeAllLayers();
    }

    public BulkObservableCollection<LayerModel> Layers { get; } = new();

    [ObservableProperty]
    private LayerModel? _selectedLayer;

    /// <summary>
    /// Collection of currently selected layers (for multi-selection).
    /// </summary>
    public ObservableCollection<LayerModel> SelectedLayers { get; } = new();

    [ObservableProperty]
    private int _selectedLayerCount;

    public event EventHandler? LayerVisibilityChanged;
    public event EventHandler? LayerLockChanged;
    public event EventHandler<DeleteLayerRequestedEventArgs>? DeleteLayerRequested;
    public event EventHandler<DeleteMultipleLayersRequestedEventArgs>? DeleteMultipleLayersRequested;
    public event EventHandler? LayersChanged;

    [RelayCommand]
    private void ShowAllLayers()
    {
        foreach (var layer in Layers)
        {
            layer.IsVisible = true;
        }
        LayerVisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void HideAllLayers()
    {
        foreach (var layer in Layers)
        {
            layer.IsVisible = false;
        }
        LayerVisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void IsolateSelectedLayer()
    {
        if (SelectedLayer == null) return;

        foreach (var layer in Layers)
        {
            layer.IsVisible = layer == SelectedLayer;
        }
        LayerVisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand(CanExecute = nameof(CanToggleSelectedLayersVisibility))]
    private void ToggleSelectedLayersVisibility()
    {
        if (SelectedLayers.Count == 0)
            return;

        // If all selected are visible, hide them all; otherwise show them all
        bool allVisible = SelectedLayers.All(l => l.IsVisible);

        foreach (var layer in SelectedLayers)
        {
            layer.IsVisible = !allVisible;
        }

        _documentService.CurrentDocument.IsDirty = true;
        LayerVisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool CanToggleSelectedLayersVisibility() => SelectedLayers.Count > 0;

    [RelayCommand(CanExecute = nameof(CanShowSelectedLayers))]
    private void ShowSelectedLayers()
    {
        foreach (var layer in SelectedLayers)
        {
            layer.IsVisible = true;
        }
        _documentService.CurrentDocument.IsDirty = true;
        LayerVisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool CanShowSelectedLayers() => SelectedLayers.Count > 0;

    [RelayCommand(CanExecute = nameof(CanHideSelectedLayers))]
    private void HideSelectedLayers()
    {
        foreach (var layer in SelectedLayers)
        {
            layer.IsVisible = false;
        }
        _documentService.CurrentDocument.IsDirty = true;
        LayerVisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool CanHideSelectedLayers() => SelectedLayers.Count > 0;

    [RelayCommand(CanExecute = nameof(CanIsolateSelectedLayers))]
    private void IsolateSelectedLayers()
    {
        if (SelectedLayers.Count == 0)
            return;

        var selectedSet = SelectedLayers.ToHashSet();
        foreach (var layer in Layers)
        {
            layer.IsVisible = selectedSet.Contains(layer);
        }
        _documentService.CurrentDocument.IsDirty = true;
        LayerVisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool CanIsolateSelectedLayers() => SelectedLayers.Count > 0;

    public void OnLayerVisibilityToggled(LayerModel layer)
    {
        _documentService.CurrentDocument.IsDirty = true;
        LayerVisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

    public void OnLayerLockToggled(LayerModel layer)
    {
        LayerLockChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand(CanExecute = nameof(CanToggleSelectedLayersLock))]
    private void ToggleSelectedLayersLock()
    {
        if (SelectedLayers.Count == 0)
            return;

        bool allLocked = SelectedLayers.All(l => l.IsLocked);

        foreach (var layer in SelectedLayers)
        {
            layer.IsLocked = !allLocked;
        }

        LayerLockChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool CanToggleSelectedLayersLock() => SelectedLayers.Count > 0;

    [RelayCommand]
    private void LockAllLayers()
    {
        foreach (var layer in Layers)
        {
            layer.IsLocked = true;
        }
        LayerLockChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void UnlockAllLayers()
    {
        foreach (var layer in Layers)
        {
            layer.IsLocked = false;
        }
        LayerLockChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand(CanExecute = nameof(CanDeleteEmptyLayers))]
    private void DeleteEmptyLayers()
    {
        // O(E + L): build occupied-layer set in one pass, then filter layers
        var occupiedLayers = _documentService.CurrentDocument.ModelSpaceEntities
            .Select(e => e.Layer?.Name)
            .Where(n => n != null)
            .ToHashSet();

        var emptyLayerModels = Layers
            .Where(l => l.Name != "0" && !occupiedLayers.Contains(l.Name))
            .ToList();

        if (emptyLayerModels.Count == 0) return;

        // Single Execute → StateChanged fires once → one RefreshEntities/RebuildSpatialIndex
        var command = new DeleteEmptyLayersCommand(
            _documentService.CurrentDocument,
            emptyLayerModels.Select(l => l.Layer));
        _undoRedoService.Execute(command);

        foreach (var layerModel in emptyLayerModels)
        {
            Layers.Remove(layerModel);
            SelectedLayers.Remove(layerModel);
        }
        SelectedLayer = Layers.FirstOrDefault();
        UpdateSelectedLayerCount();
        LayersChanged?.Invoke(this, EventArgs.Empty);
    }

    // CanExecute only checks that a document is open; the O(E+L) empty-layer scan
    // runs in the method body so it is not repeated on every WPF CanExecute poll.
    private bool CanDeleteEmptyLayers() => _documentService.CurrentDocument.Document != null;

    [RelayCommand(CanExecute = nameof(CanDeleteSelectedLayers))]
    private void DeleteSelectedLayers()
    {
        // Filter out layer "0" which cannot be deleted
        var layersToDelete = SelectedLayers
            .Where(l => l.Name != "0")
            .ToList();

        if (layersToDelete.Count == 0)
            return;

        if (layersToDelete.Count == 1)
        {
            // Single layer deletion - use existing dialog
            var layer = layersToDelete[0];
            int entityCount = _documentService.CurrentDocument.ModelSpaceEntities
                .Count(e => e.Layer?.Name == layer.Name);

            DeleteLayerRequested?.Invoke(this, new DeleteLayerRequestedEventArgs(
                layer.Layer,
                _documentService.CurrentDocument.Layers,
                entityCount));
        }
        else
        {
            // Multiple layer deletion
            var layerInfos = layersToDelete.Select(l => new LayerDeleteInfo(
                l.Layer,
                _documentService.CurrentDocument.ModelSpaceEntities.Count(e => e.Layer?.Name == l.Name)
            )).ToList();

            DeleteMultipleLayersRequested?.Invoke(this, new DeleteMultipleLayersRequestedEventArgs(
                layerInfos,
                _documentService.CurrentDocument.Layers));
        }
    }

    private bool CanDeleteSelectedLayers()
    {
        return SelectedLayers.Any(l => l.Name != "0");
    }

    // Keep for backward compatibility - maps to new command
    [RelayCommand(CanExecute = nameof(CanDeleteSelectedLayers))]
    private void DeleteSelectedLayer()
    {
        DeleteSelectedLayers();
    }

    /// <summary>
    /// Executes the layer deletion after user confirmation from dialog.
    /// </summary>
    public void ExecuteDeleteLayer(Layer layer, LayerDeleteOption option, Layer? targetLayer)
    {
        var command = new DeleteLayerCommand(
            _documentService.CurrentDocument,
            layer,
            option,
            targetLayer);

        _undoRedoService.Execute(command);

        // Remove from layers collection
        var layerModel = Layers.FirstOrDefault(l => l.Layer == layer);
        if (layerModel != null)
        {
            Layers.Remove(layerModel);
            SelectedLayers.Remove(layerModel);
        }

        SelectedLayer = Layers.FirstOrDefault();
        UpdateSelectedLayerCount();
        LayersChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Executes deletion of multiple layers after user confirmation.
    /// </summary>
    public void ExecuteDeleteMultipleLayers(IEnumerable<(Layer Layer, LayerDeleteOption Option, Layer? TargetLayer)> layerDeletions)
    {
        foreach (var (layer, option, targetLayer) in layerDeletions)
        {
            var command = new DeleteLayerCommand(
                _documentService.CurrentDocument,
                layer,
                option,
                targetLayer);

            _undoRedoService.Execute(command);

            var layerModel = Layers.FirstOrDefault(l => l.Layer == layer);
            if (layerModel != null)
            {
                Layers.Remove(layerModel);
                SelectedLayers.Remove(layerModel);
            }
        }

        SelectedLayer = Layers.FirstOrDefault();
        UpdateSelectedLayerCount();
        LayersChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Selects layers that correspond to the given entity layer names.
    /// </summary>
    public void SelectLayersForEntities(IEnumerable<string> layerNames)
    {
        var layerNameSet = layerNames.ToHashSet();

        // Clear existing selection
        foreach (var layer in Layers)
        {
            layer.IsSelected = layerNameSet.Contains(layer.Name);
        }

        // Update SelectedLayers collection
        SelectedLayers.Clear();
        foreach (var layer in Layers.Where(l => l.IsSelected))
        {
            SelectedLayers.Add(layer);
        }

        UpdateSelectedLayerCount();
        NotifySelectionCommandsChanged();
    }

    /// <summary>
    /// Updates the SelectedLayers collection based on IsSelected property of each layer.
    /// Called from the view when ListBox selection changes.
    /// </summary>
    public void UpdateSelectedLayersFromUI(IEnumerable<LayerModel> selectedItems)
    {
        SelectedLayers.Clear();
        foreach (var layer in selectedItems)
        {
            layer.IsSelected = true;
            SelectedLayers.Add(layer);
        }

        // Mark non-selected as not selected
        foreach (var layer in Layers.Where(l => !selectedItems.Contains(l)))
        {
            layer.IsSelected = false;
        }

        UpdateSelectedLayerCount();
        NotifySelectionCommandsChanged();
    }

    private void UpdateSelectedLayerCount()
    {
        SelectedLayerCount = SelectedLayers.Count;
    }

    private void NotifySelectionCommandsChanged()
    {
        DeleteSelectedLayersCommand.NotifyCanExecuteChanged();
        DeleteSelectedLayerCommand.NotifyCanExecuteChanged();
        DeleteEmptyLayersCommand.NotifyCanExecuteChanged();
        ToggleSelectedLayersVisibilityCommand.NotifyCanExecuteChanged();
        ShowSelectedLayersCommand.NotifyCanExecuteChanged();
        HideSelectedLayersCommand.NotifyCanExecuteChanged();
        IsolateSelectedLayersCommand.NotifyCanExecuteChanged();
        ToggleSelectedLayersLockCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Refreshes the layers collection from the document.
    /// </summary>
    public void RefreshLayers()
    {
        var hiddenNames = Layers.Where(l => !l.IsVisible).Select(l => l.Name).ToHashSet();
        UnsubscribeAllLayers();
        var newLayers = _documentService.CurrentDocument.Layers
            .Select(layer =>
            {
                var m = new LayerModel(layer);
                m.IsVisible = !hiddenNames.Contains(layer.Name);
                m.PropertyChanged += OnLayerModelPropertyChanged;
                return m;
            })
            .ToList();
        Layers.ReplaceAll(newLayers);   // single Reset notification instead of N Add notifications
    }

    private void OnLayerModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not LayerModel layerModel) return;
        if (e.PropertyName == nameof(LayerModel.IsVisible))
            OnLayerVisibilityToggled(layerModel);
        else if (e.PropertyName == nameof(LayerModel.IsLocked))
            OnLayerLockToggled(layerModel);
    }

    private void UnsubscribeAllLayers()
    {
        foreach (var layer in Layers)
            layer.PropertyChanged -= OnLayerModelPropertyChanged;
    }

    partial void OnSelectedLayerChanged(LayerModel? value)
    {
        NotifySelectionCommandsChanged();
    }

    private void OnDocumentLoaded(object? sender, DocumentLoadedEventArgs e)
    {
        RefreshLayers();
    }

    private void OnDocumentClosed(object? sender, EventArgs e)
    {
        UnsubscribeAllLayers();
        Layers.Clear();
        SelectedLayers.Clear();
        SelectedLayer = null;
        UpdateSelectedLayerCount();
    }
}

/// <summary>
/// Event args for layer deletion request.
/// </summary>
public class DeleteLayerRequestedEventArgs : EventArgs
{
    public Layer Layer { get; }
    public IEnumerable<Layer> AvailableLayers { get; }
    public int EntityCount { get; }

    public DeleteLayerRequestedEventArgs(Layer layer, IEnumerable<Layer> availableLayers, int entityCount)
    {
        Layer = layer;
        AvailableLayers = availableLayers;
        EntityCount = entityCount;
    }
}

/// <summary>
/// Information about a layer to be deleted.
/// </summary>
public class LayerDeleteInfo
{
    public Layer Layer { get; }
    public int EntityCount { get; }

    public LayerDeleteInfo(Layer layer, int entityCount)
    {
        Layer = layer;
        EntityCount = entityCount;
    }
}

/// <summary>
/// Event args for multiple layer deletion request.
/// </summary>
public class DeleteMultipleLayersRequestedEventArgs : EventArgs
{
    public IReadOnlyList<LayerDeleteInfo> Layers { get; }
    public IEnumerable<Layer> AvailableLayers { get; }
    public int TotalEntityCount => Layers.Sum(l => l.EntityCount);

    public DeleteMultipleLayersRequestedEventArgs(IReadOnlyList<LayerDeleteInfo> layers, IEnumerable<Layer> availableLayers)
    {
        Layers = layers;
        AvailableLayers = availableLayers;
    }
}
