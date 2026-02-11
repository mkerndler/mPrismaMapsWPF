using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using ACadSharp.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using mPrismaMapsWPF.Commands;
using mPrismaMapsWPF.Drawing;
using mPrismaMapsWPF.Helpers;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Services;
using mPrismaMapsWPF.Controls;
using WpfPoint = System.Windows.Point;

namespace mPrismaMapsWPF.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IDocumentService _documentService;
    private readonly ISelectionService _selectionService;
    private readonly IUndoRedoService _undoRedoService;
    private readonly IWalkwayService _walkwayService;
    private readonly ILogger<MainWindowViewModel> _logger;

    public MainWindowViewModel(
        IDocumentService documentService,
        ISelectionService selectionService,
        IUndoRedoService undoRedoService,
        IWalkwayService walkwayService,
        ILogger<MainWindowViewModel> logger)
    {
        _documentService = documentService;
        _selectionService = selectionService;
        _undoRedoService = undoRedoService;
        _walkwayService = walkwayService;
        _logger = logger;

        _documentService.DocumentLoaded += OnDocumentLoaded;
        _documentService.DocumentClosed += OnDocumentClosed;
        _selectionService.SelectionChanged += OnSelectionChanged;
        _undoRedoService.StateChanged += OnUndoRedoStateChanged;

        LayerPanel = new LayerPanelViewModel(_documentService, _undoRedoService);
        PropertiesPanel = new PropertiesPanelViewModel(_selectionService, _documentService, _undoRedoService);
        EntityViewer = new EntityViewerViewModel(_selectionService);

        // Subscribe to layer visibility changes to update DeleteHiddenEntities command state
        LayerPanel.LayerVisibilityChanged += OnLayerVisibilityChangedForCommand;
    }

    private void OnLayerVisibilityChangedForCommand(object? sender, EventArgs e)
    {
        DeleteHiddenEntitiesCommand.NotifyCanExecuteChanged();
    }

    public LayerPanelViewModel LayerPanel { get; }
    public PropertiesPanelViewModel PropertiesPanel { get; }
    public EntityViewerViewModel EntityViewer { get; }

    [ObservableProperty]
    private string _windowTitle = "mPrismaMaps - DWG Viewer";

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private double _mouseX;

    [ObservableProperty]
    private double _mouseY;

    [ObservableProperty]
    private int _entityCount;

    [ObservableProperty]
    private int _selectedCount;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private int _loadProgress;

    [ObservableProperty]
    private double _zoomLevel = 1.0;

    [ObservableProperty]
    private bool _isPanMode;

    [ObservableProperty]
    private bool _isSelectMode = true;

    [ObservableProperty]
    private bool _isDrawLineMode;

    [ObservableProperty]
    private bool _isDrawPolylineMode;

    [ObservableProperty]
    private bool _isDrawPolygonMode;

    [ObservableProperty]
    private bool _isPlaceUnitNumberMode;

    [ObservableProperty]
    private bool _isDrawFairwayMode;

    [ObservableProperty]
    private bool _canUndo;

    [ObservableProperty]
    private bool _canRedo;

    [ObservableProperty]
    private string? _undoDescription;

    [ObservableProperty]
    private string? _redoDescription;

    [ObservableProperty]
    private DrawingMode _drawingMode = DrawingMode.Select;

    [ObservableProperty]
    private GridSnapSettings _gridSettings = new();

    [ObservableProperty]
    private bool _isSnapEnabled = true;

    [ObservableProperty]
    private bool _isGridVisible = true;

    [ObservableProperty]
    private double _gridSpacing = 10.0;

    [ObservableProperty]
    private string _drawingStatusText = "";

    // View transform properties
    [ObservableProperty]
    private bool _flipX;

    [ObservableProperty]
    private bool _flipY;

    [ObservableProperty]
    private double _viewRotation;

    // Unit number placement properties
    [ObservableProperty]
    private string _unitNumberPrefix = "";

    [ObservableProperty]
    private int _unitNextNumber = 1;

    [ObservableProperty]
    private double _unitTextHeight = 10.0;

    [ObservableProperty]
    private bool _unitAutoIncrement = true;

    [ObservableProperty]
    private HashSet<ulong>? _highlightedPathHandles;

    private List<Entity>? _clipboardEntities;

    public ObservableCollection<EntityModel> Entities { get; } = new();
    private Dictionary<Entity, EntityModel> _entityLookup = new();
    public CadDocumentModel Document => _documentService.CurrentDocument;

    /// <summary>
    /// O(1) lookup of EntityModel by Entity reference. Returns null if not found.
    /// </summary>
    public EntityModel? GetEntityModel(Entity entity)
    {
        return _entityLookup.TryGetValue(entity, out var model) ? model : null;
    }

    public IUndoRedoService UndoRedoService => _undoRedoService;

    public event EventHandler? ZoomToFitRequested;
    public event EventHandler? RenderRequested;
    public event EventHandler? EntitiesChanged;
    public event EventHandler<SelectEntityTypesEventArgs>? SelectEntityTypesRequested;
    public event EventHandler? CenterOnOriginRequested;
    public event EventHandler? ResetViewTransformsRequested;
    public event EventHandler<RotateViewEventArgs>? RotateViewRequested;
    public event EventHandler<DeleteOutsideViewportEventArgs>? DeleteOutsideViewportRequested;
    public event EventHandler<ZoomToEntityEventArgs>? ZoomToEntityRequested;
    public event EventHandler<ZoomToAreaEventArgs>? ZoomToAreaRequested;
    public event EventHandler<EditUnitNumberRequestedEventArgs>? EditUnitNumberRequested;

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "DWG Files (*.dwg)|*.dwg|DXF Files (*.dxf)|*.dxf|All CAD Files (*.dwg;*.dxf)|*.dwg;*.dxf",
            FilterIndex = 3,
            Title = "Open CAD File"
        };

        if (dialog.ShowDialog() == true)
        {
            IsLoading = true;
            StatusText = "Scanning file...";

            try
            {
                _logger.LogInformation("User opening file {FilePath}", dialog.FileName);
                var scanResults = await _documentService.ScanEntityTypesAsync(dialog.FileName);

                IsLoading = false;

                // Raise event for view to show dialog
                SelectEntityTypesRequested?.Invoke(this,
                    new SelectEntityTypesEventArgs(dialog.FileName, scanResults));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to scan file {FilePath}", dialog.FileName);
                IsLoading = false;
                StatusText = "Failed to scan file";
                MessageBox.Show($"Failed to scan file: {dialog.FileName}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public async Task LoadFileWithFilterAsync(string filePath, ISet<Type>? excludedTypes)
    {
        IsLoading = true;
        StatusText = "Loading...";
        LoadProgress = 0;

        var progress = new Progress<int>(p => LoadProgress = p);

        bool success = await _documentService.OpenAsync(filePath, excludedTypes, progress);

        IsLoading = false;

        if (!success)
        {
            _logger.LogError("Failed to load file {FilePath}", filePath);
            StatusText = "Failed to load file";
            MessageBox.Show($"Failed to open file: {filePath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task LoadFileAsync(string filePath)
    {
        await LoadFileWithFilterAsync(filePath, null);
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveFileAsync()
    {
        if (_documentService.CurrentDocument.FilePath == null)
        {
            await SaveFileAsAsync();
            return;
        }

        _logger.LogInformation("Saving file");
        IsLoading = true;
        StatusText = "Saving...";

        bool success = await _documentService.SaveAsync();

        IsLoading = false;
        if (!success) _logger.LogError("Save failed");
        StatusText = success ? "Saved" : "Save failed";
    }

    private bool CanSave() => _documentService.CurrentDocument.Document != null;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveFileAsAsync()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "DWG Files (*.dwg)|*.dwg|DXF Files (*.dxf)|*.dxf",
            FilterIndex = 1,
            Title = "Save CAD File"
        };

        if (dialog.ShowDialog() == true)
        {
            IsLoading = true;
            StatusText = "Saving...";

            bool success = await _documentService.SaveAsync(dialog.FileName);

            IsLoading = false;
            StatusText = success ? "Saved" : "Save failed";
        }
    }

    [RelayCommand]
    private void CloseFile()
    {
        _logger.LogInformation("Closing file");
        if (_documentService.HasUnsavedChanges)
        {
            var result = MessageBox.Show(
                "Do you want to save changes before closing?",
                "Unsaved Changes",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel)
                return;

            if (result == MessageBoxResult.Yes)
            {
                SaveFileCommand.Execute(null);
            }
        }

        _documentService.Close();
    }

    [RelayCommand]
    private void Exit()
    {
        CloseFile();
        Application.Current.Shutdown();
    }

    [RelayCommand]
    private void ZoomIn()
    {
        ZoomLevel = Math.Min(ZoomLevel * 1.25, 100.0);
    }

    [RelayCommand]
    private void ZoomOut()
    {
        ZoomLevel = Math.Max(ZoomLevel / 1.25, 0.01);
    }

    [RelayCommand]
    private void ZoomToFit()
    {
        ZoomToFitRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void SetPanMode()
    {
        DrawingMode = DrawingMode.Pan;
        UpdateModeFlags();
    }

    [RelayCommand]
    private void SetSelectMode()
    {
        DrawingMode = DrawingMode.Select;
        UpdateModeFlags();
    }

    [RelayCommand]
    private void SetDrawLineMode()
    {
        DrawingMode = DrawingMode.DrawLine;
        UpdateModeFlags();
        DrawingStatusText = "Line: Click to set start point";
    }

    [RelayCommand]
    private void SetDrawPolylineMode()
    {
        DrawingMode = DrawingMode.DrawPolyline;
        UpdateModeFlags();
        DrawingStatusText = "Polyline: Click to add points, double-click or Enter to finish";
    }

    [RelayCommand]
    private void SetDrawPolygonMode()
    {
        DrawingMode = DrawingMode.DrawPolygon;
        UpdateModeFlags();
        DrawingStatusText = "Polygon: Click to add points (min 3), double-click or Enter to close";
    }

    [RelayCommand]
    private void SetZoomToAreaMode()
    {
        DrawingMode = DrawingMode.ZoomToArea;
        UpdateModeFlags();
        DrawingStatusText = "Zoom to Area: Click and drag to define zoom rectangle";
    }

    [RelayCommand]
    private void SetPlaceUnitNumberMode()
    {
        DrawingMode = DrawingMode.PlaceUnitNumber;
        UpdateModeFlags();
        DrawingStatusText = $"Place Unit Number: Click to place '{UnitNumberPrefix}{UnitNextNumber.ToString("D3")}'";
    }

    [RelayCommand]
    private void SetDrawFairwayMode()
    {
        DrawingMode = DrawingMode.DrawFairway;
        UpdateModeFlags();
        DrawingStatusText = "Fairway: Click to place nodes, Right-click/Escape to end segment";
    }

    [RelayCommand]
    private void ToggleSnap()
    {
        IsSnapEnabled = !IsSnapEnabled;
        GridSettings.IsEnabled = IsSnapEnabled;
    }

    [RelayCommand]
    private void ToggleGrid()
    {
        IsGridVisible = !IsGridVisible;
        GridSettings.ShowGrid = IsGridVisible;
        RenderRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void ToggleFlipX()
    {
        FlipX = !FlipX;
        StatusText = FlipX ? "View flipped horizontally" : "Horizontal flip removed";
    }

    [RelayCommand]
    private void ToggleFlipY()
    {
        FlipY = !FlipY;
        StatusText = FlipY ? "View flipped vertically" : "Vertical flip removed";
    }

    [RelayCommand]
    private void RotateView(double angle)
    {
        ViewRotation = angle;
        StatusText = $"View rotated to {angle}";
    }

    [RelayCommand]
    private void RotateViewBy(double delta)
    {
        ViewRotation = (ViewRotation + delta) % 360;
        if (ViewRotation < 0) ViewRotation += 360;
        StatusText = $"View rotated to {ViewRotation}";
    }

    [RelayCommand]
    private void ShowRotateViewDialog()
    {
        RotateViewRequested?.Invoke(this, new RotateViewEventArgs(ViewRotation));
    }

    [RelayCommand]
    private void CenterOnOrigin()
    {
        CenterOnOriginRequested?.Invoke(this, EventArgs.Empty);
        StatusText = "View centered on origin (0,0)";
    }

    [RelayCommand]
    private void ResetViewTransforms()
    {
        FlipX = false;
        FlipY = false;
        ViewRotation = 0;
        ResetViewTransformsRequested?.Invoke(this, EventArgs.Empty);
        StatusText = "View transforms reset";
    }

    [RelayCommand(CanExecute = nameof(CanDeleteHidden))]
    private void DeleteHiddenEntities()
    {
        var hiddenLayers = LayerPanel.Layers
            .Where(l => !l.IsVisible)
            .Select(l => l.Name)
            .ToList();

        var command = new DeleteHiddenEntitiesCommand(_documentService.CurrentDocument, hiddenLayers);
        _undoRedoService.Execute(command);

        if (command.DeletedCount == 0)
        {
            StatusText = "No hidden or invisible entities found";
            return;
        }

        // Refresh entities
        RefreshEntities();
        LayerPanel.RefreshLayers();

        // Build status message
        var parts = new List<string>();
        if (command.EntitiesOnHiddenLayers > 0)
            parts.Add($"{command.EntitiesOnHiddenLayers} on hidden layers");
        if (command.InvisibleEntities > 0)
            parts.Add($"{command.InvisibleEntities} invisible");

        StatusText = $"Deleted {command.DeletedCount} entities ({string.Join(", ", parts)})";
    }

    private bool CanDeleteHidden() => _documentService.CurrentDocument.Document != null;

    [RelayCommand(CanExecute = nameof(CanDeleteOutsideViewport))]
    private void DeleteEntitiesOutsideViewport()
    {
        var args = new DeleteOutsideViewportEventArgs();
        DeleteOutsideViewportRequested?.Invoke(this, args);

        if (args.Cancelled || args.ViewportBounds.IsEmpty)
        {
            return;
        }

        var command = new DeleteEntitiesOutsideViewportCommand(_documentService.CurrentDocument, args.ViewportBounds);
        _undoRedoService.Execute(command);

        if (command.DeletedCount == 0)
        {
            StatusText = "No entities outside viewport";
            return;
        }

        // Refresh entities
        RefreshEntities();

        StatusText = $"Deleted {command.DeletedCount} entities outside viewport";
    }

    private bool CanDeleteOutsideViewport() => _documentService.CurrentDocument.Document != null;

    private void UpdateModeFlags()
    {
        IsPanMode = DrawingMode == DrawingMode.Pan;
        IsSelectMode = DrawingMode == DrawingMode.Select;
        IsDrawLineMode = DrawingMode == DrawingMode.DrawLine;
        IsDrawPolylineMode = DrawingMode == DrawingMode.DrawPolyline;
        IsDrawPolygonMode = DrawingMode == DrawingMode.DrawPolygon;
        IsPlaceUnitNumberMode = DrawingMode == DrawingMode.PlaceUnitNumber;
        IsDrawFairwayMode = DrawingMode == DrawingMode.DrawFairway;

        if (DrawingMode == DrawingMode.Select || DrawingMode == DrawingMode.Pan)
        {
            DrawingStatusText = "";
        }
    }

    partial void OnGridSpacingChanged(double value)
    {
        GridSettings.SetUniformSpacing(value);
        RenderRequested?.Invoke(this, EventArgs.Empty);
    }

    partial void OnIsSnapEnabledChanged(bool value)
    {
        GridSettings.IsEnabled = value;
    }

    partial void OnIsGridVisibleChanged(bool value)
    {
        GridSettings.ShowGrid = value;
        RenderRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void SelectAll()
    {
        _selectionService.SelectMultiple(Entities.Where(e => !e.IsLocked));
    }

    [RelayCommand]
    private void ClearSelection()
    {
        _selectionService.ClearSelection();
    }

    [RelayCommand(CanExecute = nameof(CanExecuteUndo))]
    private void Undo()
    {
        _undoRedoService.Undo();
        RefreshAfterEdit();
    }

    private bool CanExecuteUndo() => _undoRedoService.CanUndo;

    [RelayCommand(CanExecute = nameof(CanExecuteRedo))]
    private void Redo()
    {
        _undoRedoService.Redo();
        RefreshAfterEdit();
    }

    private bool CanExecuteRedo() => _undoRedoService.CanRedo;

    [RelayCommand(CanExecute = nameof(CanDeleteSelected))]
    private void DeleteSelected()
    {
        var allSelected = _selectionService.SelectedEntities.ToList();
        if (allSelected.Count == 0)
            return;

        var deletable = allSelected.Where(e => !e.IsLocked).ToList();
        int skipped = allSelected.Count - deletable.Count;

        if (deletable.Count == 0)
        {
            StatusText = $"Cannot delete: {skipped} locked entity(ies)";
            return;
        }

        // Cascade: if deleting walkway nodes, also delete connected edges
        var cascaded = CollectConnectedWalkwayEdges(deletable);
        if (cascaded.Count > 0)
        {
            foreach (var edge in cascaded)
            {
                if (!deletable.Contains(edge))
                    deletable.Add(edge);
            }
        }

        var command = new DeleteEntitiesCommand(_documentService.CurrentDocument, deletable);
        _undoRedoService.Execute(command);

        // Clear selection and remove from entities collection
        _selectionService.ClearSelection();

        foreach (var entityModel in deletable)
        {
            _entityLookup.Remove(entityModel.Entity);
            Entities.Remove(entityModel);
        }

        RefreshAfterEdit();
        _walkwayService.RebuildGraph(Entities);

        if (skipped > 0)
            StatusText = $"Deleted {deletable.Count} entities, {skipped} locked entity(ies) skipped";
    }

    /// <summary>
    /// Finds walkway edge EntityModels (Lines on Walkways layer) connected to any
    /// walkway node (Circle on Walkways layer) in the given list.
    /// </summary>
    private List<EntityModel> CollectConnectedWalkwayEdges(List<EntityModel> entitiesToDelete)
    {
        // Collect centers of walkway nodes being deleted
        var deletedNodeCenters = new List<(double x, double y, double radius)>();
        foreach (var em in entitiesToDelete)
        {
            if (em.Entity is Circle circle and not Arc &&
                circle.Layer?.Name == CadDocumentModel.WalkwaysLayerName)
            {
                deletedNodeCenters.Add((circle.Center.X, circle.Center.Y, circle.Radius));
            }
        }

        if (deletedNodeCenters.Count == 0)
            return new List<EntityModel>();

        // Find all edge lines on Walkways layer that have an endpoint near a deleted node
        var connectedEdges = new List<EntityModel>();
        var deletingHandles = new HashSet<ulong>(entitiesToDelete.Select(e => e.Handle));

        foreach (var em in Entities)
        {
            if (deletingHandles.Contains(em.Handle))
                continue;

            if (em.Entity is Line line && line.Layer?.Name == CadDocumentModel.WalkwaysLayerName)
            {
                foreach (var (cx, cy, radius) in deletedNodeCenters)
                {
                    double tolerance = radius * 2;
                    double dxStart = line.StartPoint.X - cx;
                    double dyStart = line.StartPoint.Y - cy;
                    double dxEnd = line.EndPoint.X - cx;
                    double dyEnd = line.EndPoint.Y - cy;

                    bool startNear = Math.Sqrt(dxStart * dxStart + dyStart * dyStart) < tolerance;
                    bool endNear = Math.Sqrt(dxEnd * dxEnd + dyEnd * dyEnd) < tolerance;

                    if (startNear || endNear)
                    {
                        connectedEdges.Add(em);
                        break;
                    }
                }
            }
        }

        return connectedEdges;
    }

    private bool CanDeleteSelected() => _selectionService.SelectedEntities.Count > 0;

    [RelayCommand(CanExecute = nameof(CanCopy))]
    private void Copy()
    {
        var selected = _selectionService.SelectedEntities.ToList();
        _clipboardEntities = new List<Entity>();
        foreach (var entityModel in selected)
        {
            var clone = EntityTransformHelper.CloneEntity(entityModel.Entity);
            if (clone != null)
                _clipboardEntities.Add(clone);
        }

        PasteCommand.NotifyCanExecuteChanged();
        StatusText = $"Copied {_clipboardEntities.Count} entities";
    }

    private bool CanCopy() => _selectionService.SelectedEntities.Count > 0;

    [RelayCommand(CanExecute = nameof(CanPaste))]
    private void Paste()
    {
        if (_clipboardEntities == null || _clipboardEntities.Count == 0)
            return;

        _documentService.CurrentDocument.EnsureDocumentExists();

        // Clone again so paste can be repeated
        var pasteEntities = new List<Entity>();
        foreach (var entity in _clipboardEntities)
        {
            var clone = EntityTransformHelper.CloneEntity(entity);
            if (clone != null)
            {
                // Offset by (20, 20) CAD units
                EntityTransformHelper.TranslateEntity(clone, 20, 20);
                pasteEntities.Add(clone);
            }
        }

        if (pasteEntities.Count == 0)
            return;

        var command = new PasteEntitiesCommand(_documentService.CurrentDocument, pasteEntities);
        _undoRedoService.Execute(command);

        // Add EntityModels and select pasted entities
        _selectionService.ClearSelection();
        var pastedModels = new List<EntityModel>();
        foreach (var entity in pasteEntities)
        {
            var entityModel = new EntityModel(entity);
            _entityLookup[entity] = entityModel;
            Entities.Add(entityModel);
            pastedModels.Add(entityModel);
        }

        _selectionService.SelectMultiple(pastedModels);
        EntityCount = Entities.Count;

        // Update clipboard to the new clones so next paste offsets from these positions
        _clipboardEntities = pasteEntities;

        StatusText = $"Pasted {pasteEntities.Count} entities";
        EntitiesChanged?.Invoke(this, EventArgs.Empty);
        RenderRequested?.Invoke(this, EventArgs.Empty);
    }

    private bool CanPaste() => _clipboardEntities != null && _clipboardEntities.Count > 0;

    [RelayCommand(CanExecute = nameof(CanLockSelected))]
    private void LockSelected()
    {
        var selected = _selectionService.SelectedEntities.ToList();
        foreach (var entity in selected)
        {
            entity.IsLocked = true;
        }
        _selectionService.ClearSelection();
        StatusText = $"Locked {selected.Count} entity(ies)";
        RenderRequested?.Invoke(this, EventArgs.Empty);
    }

    private bool CanLockSelected() => _selectionService.SelectedEntities.Count > 0;

    [RelayCommand]
    private void UnlockAllEntities()
    {
        int count = 0;
        foreach (var entity in Entities)
        {
            if (entity.IsLocked)
            {
                entity.IsLocked = false;
                count++;
            }
        }
        StatusText = count > 0 ? $"Unlocked {count} entity(ies)" : "No locked entities";
        RenderRequested?.Invoke(this, EventArgs.Empty);
    }

    public void OnMoveCompleted(MoveCompletedEventArgs e)
    {
        var selectedEntities = _selectionService.SelectedEntities
            .Where(em => !em.IsLocked)
            .Select(em => em.Entity)
            .ToList();

        if (selectedEntities.Count == 0)
            return;

        // Collect walkway edges that need endpoint adjustment
        var edgeAdjustments = CollectWalkwayEdgeAdjustments(selectedEntities, e.DeltaX, e.DeltaY);

        // Include edge adjustments in a composite undoable operation
        var allEntities = new List<Entity>(selectedEntities);
        foreach (var (line, _, _) in edgeAdjustments)
        {
            if (!allEntities.Contains(line))
                allEntities.Add(line);
        }

        // Move the selected entities (and any fully-selected edges)
        var command = new MoveEntitiesCommand(_documentService.CurrentDocument, selectedEntities, e.DeltaX, e.DeltaY);
        _undoRedoService.Execute(command);

        // Adjust connected edge endpoints (only the endpoint attached to a moved node)
        if (edgeAdjustments.Count > 0)
        {
            var edgeCommand = new AdjustWalkwayEdgesCommand(edgeAdjustments, e.DeltaX, e.DeltaY);
            _undoRedoService.Execute(edgeCommand);
        }

        // Invalidate caches for all affected entities
        foreach (var entity in allEntities)
        {
            BoundingBoxHelper.InvalidateEntity(entity.Handle);
        }

        _walkwayService.RebuildGraph(Entities);
        EntitiesChanged?.Invoke(this, EventArgs.Empty);
        RenderRequested?.Invoke(this, EventArgs.Empty);
        StatusText = $"Moved {selectedEntities.Count} entities";
    }

    /// <summary>
    /// Finds walkway edges not in the selection that have an endpoint connected to a
    /// moved walkway node, and determines which endpoint (start/end) needs adjusting.
    /// </summary>
    private List<(Line line, bool adjustStart, bool adjustEnd)> CollectWalkwayEdgeAdjustments(
        List<Entity> movedEntities, double dx, double dy)
    {
        // Collect pre-move centers of walkway nodes being moved
        var movedNodeCenters = new List<(double x, double y, double radius)>();
        var movedHandles = new HashSet<ulong>(movedEntities.Select(e => e.Handle));

        foreach (var entity in movedEntities)
        {
            if (entity is Circle circle and not Arc &&
                circle.Layer?.Name == CadDocumentModel.WalkwaysLayerName)
            {
                movedNodeCenters.Add((circle.Center.X, circle.Center.Y, circle.Radius));
            }
        }

        if (movedNodeCenters.Count == 0)
            return new List<(Line, bool, bool)>();

        var adjustments = new List<(Line line, bool adjustStart, bool adjustEnd)>();

        foreach (var em in Entities)
        {
            // Skip entities already in the move selection
            if (movedHandles.Contains(em.Handle))
                continue;

            if (em.Entity is Line line && line.Layer?.Name == CadDocumentModel.WalkwaysLayerName)
            {
                bool adjustStart = false;
                bool adjustEnd = false;

                foreach (var (cx, cy, radius) in movedNodeCenters)
                {
                    double tolerance = radius * 2;

                    double dsX = line.StartPoint.X - cx;
                    double dsY = line.StartPoint.Y - cy;
                    if (Math.Sqrt(dsX * dsX + dsY * dsY) < tolerance)
                        adjustStart = true;

                    double deX = line.EndPoint.X - cx;
                    double deY = line.EndPoint.Y - cy;
                    if (Math.Sqrt(deX * deX + deY * deY) < tolerance)
                        adjustEnd = true;
                }

                if (adjustStart || adjustEnd)
                {
                    adjustments.Add((line, adjustStart, adjustEnd));
                }
            }
        }

        return adjustments;
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void DeleteByType(Type entityType)
    {
        if (entityType == null || _documentService.CurrentDocument.Document == null)
            return;

        var command = new DeleteEntitiesByTypeCommand(_documentService.CurrentDocument, entityType);
        _undoRedoService.Execute(command);

        // Remove matching entities from collection
        var entitiesToRemove = Entities.Where(e => e.Entity.GetType() == entityType).ToList();
        foreach (var entity in entitiesToRemove)
        {
            Entities.Remove(entity);
        }

        _selectionService.ClearSelection();
        RefreshAfterEdit();

        StatusText = $"Deleted {command.DeletedCount} {entityType.Name} entities";
    }

    /// <summary>
    /// Gets the entity types present in the current document for the Delete by Type menu.
    /// </summary>
    public IEnumerable<Type> GetEntityTypes()
    {
        if (_documentService.CurrentDocument.Document == null)
            return Enumerable.Empty<Type>();

        return _documentService.CurrentDocument.ModelSpaceEntities
            .Select(e => e.GetType())
            .Distinct()
            .OrderBy(t => t.Name);
    }

    private void RefreshAfterEdit()
    {
        EntityCount = Entities.Count;
        EntitiesChanged?.Invoke(this, EventArgs.Empty);
        RenderRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnUndoRedoStateChanged(object? sender, EventArgs e)
    {
        CanUndo = _undoRedoService.CanUndo;
        CanRedo = _undoRedoService.CanRedo;
        UndoDescription = _undoRedoService.UndoDescription;
        RedoDescription = _undoRedoService.RedoDescription;

        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    public void UpdateMousePosition(double x, double y)
    {
        MouseX = x;
        MouseY = y;
    }

    private void OnDocumentLoaded(object? sender, DocumentLoadedEventArgs e)
    {
        WindowTitle = $"mPrismaMaps - {Path.GetFileName(e.FilePath)}";
        StatusText = $"Loaded {e.EntityCount:N0} entities";
        EntityCount = e.EntityCount;

        // Invalidate bounding box cache for new document
        BoundingBoxHelper.InvalidateCache();

        // Build entity models in a batch
        var models = _documentService.CurrentDocument.ModelSpaceEntities
            .Select(ent => new EntityModel(ent))
            .ToList();

        // Build lookup dictionary
        _entityLookup = new Dictionary<Entity, EntityModel>(models.Count);
        foreach (var model in models)
            _entityLookup[model.Entity] = model;

        // Suppress EntityViewer refresh during bulk load
        EntityViewer.SuppressRefresh = true;
        Entities.Clear();
        foreach (var model in models)
            Entities.Add(model);
        EntityViewer.SuppressRefresh = false;

        // Update entity viewer
        EntityViewer.SetEntities(Entities);

        // Update grid spacing based on document size
        UpdateGridSpacingFromExtents();

        // Auto-scale unit text height based on document extents
        UpdateUnitTextHeightFromExtents();

        // Rebuild walkway graph from loaded entities
        _walkwayService.RebuildGraph(Entities);

        // Reset to select mode
        SetSelectMode();

        SaveFileCommand.NotifyCanExecuteChanged();
        SaveFileAsCommand.NotifyCanExecuteChanged();
        DeleteHiddenEntitiesCommand.NotifyCanExecuteChanged();
        DeleteEntitiesOutsideViewportCommand.NotifyCanExecuteChanged();

        RenderRequested?.Invoke(this, EventArgs.Empty);
        ZoomToFitRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnDocumentClosed(object? sender, EventArgs e)
    {
        WindowTitle = "mPrismaMaps - DWG Viewer";
        StatusText = "Ready";
        EntityCount = 0;
        Entities.Clear();
        _entityLookup.Clear();
        BoundingBoxHelper.InvalidateCache();
        EntityViewer.SetEntities(Entities);

        SaveFileCommand.NotifyCanExecuteChanged();
        SaveFileAsCommand.NotifyCanExecuteChanged();
        DeleteHiddenEntitiesCommand.NotifyCanExecuteChanged();
        DeleteEntitiesOutsideViewportCommand.NotifyCanExecuteChanged();

        RenderRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        SelectedCount = e.SelectedEntities.Count;
        DeleteSelectedCommand.NotifyCanExecuteChanged();
        CopyCommand.NotifyCanExecuteChanged();
        LockSelectedCommand.NotifyCanExecuteChanged();

        // Update path highlighting for selected unit numbers
        UpdatePathHighlightsForSelection(e.SelectedEntities);
    }

    private void UpdatePathHighlightsForSelection(IReadOnlyCollection<EntityModel> selectedEntities)
    {
        if (selectedEntities.Count == 1 &&
            selectedEntities.First().Entity is MText mtext &&
            mtext.Layer?.Name == CadDocumentModel.UnitNumbersLayerName)
        {
            var highlights = _walkwayService.GetPathHighlightsForUnit(
                mtext.InsertPoint.X, mtext.InsertPoint.Y);
            HighlightedPathHandles = highlights;
        }
        else
        {
            HighlightedPathHandles = null;
        }
    }

    /// <summary>
    /// Refreshes the entity collection from the document. Called after undo/redo operations.
    /// </summary>
    public void RefreshEntities()
    {
        BoundingBoxHelper.InvalidateCache();

        var models = _documentService.CurrentDocument.ModelSpaceEntities
            .Select(ent => new EntityModel(ent))
            .ToList();

        _entityLookup = new Dictionary<Entity, EntityModel>(models.Count);
        foreach (var model in models)
            _entityLookup[model.Entity] = model;

        EntityViewer.SuppressRefresh = true;
        Entities.Clear();
        foreach (var model in models)
            Entities.Add(model);
        EntityViewer.SuppressRefresh = false;

        EntityCount = Entities.Count;
        EntityViewer.Refresh();
        _walkwayService.RebuildGraph(Entities);
        RenderRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Handles the completion of a drawing operation and creates the corresponding entity.
    /// </summary>
    public void OnDrawingCompleted(DrawingCompletedEventArgs e)
    {
        if (e.Mode == DrawingMode.PlaceUnitNumber)
        {
            HandlePlaceUnitNumber(e);
            return;
        }

        if (e.Mode == DrawingMode.DrawFairway)
        {
            HandleFairwayPlacement(e);
            return;
        }

        if (e.Points.Count < 2)
            return;

        // Handle non-entity-creating tools
        if (e.Mode == DrawingMode.ZoomToArea)
        {
            var minX = Math.Min(e.Points[0].X, e.Points[1].X);
            var minY = Math.Min(e.Points[0].Y, e.Points[1].Y);
            var maxX = Math.Max(e.Points[0].X, e.Points[1].X);
            var maxY = Math.Max(e.Points[0].Y, e.Points[1].Y);
            ZoomToAreaRequested?.Invoke(this, new ZoomToAreaEventArgs(minX, minY, maxX, maxY));
            SetSelectMode();
            return;
        }

        // Ensure document exists (create if needed for drawing without loading a file)
        _documentService.CurrentDocument.EnsureDocumentExists();

        // Get or create the User Drawings layer
        var userLayer = _documentService.CurrentDocument.GetOrCreateUserDrawingsLayer();
        if (userLayer == null)
            return;

        Entity? entity = null;

        switch (e.Mode)
        {
            case DrawingMode.DrawLine:
                if (e.Points.Count >= 2)
                {
                    entity = new Line
                    {
                        StartPoint = new CSMath.XYZ(e.Points[0].X, e.Points[0].Y, 0),
                        EndPoint = new CSMath.XYZ(e.Points[1].X, e.Points[1].Y, 0),
                        Layer = userLayer
                    };
                }
                break;

            case DrawingMode.DrawPolyline:
            case DrawingMode.DrawPolygon:
                if (e.Points.Count >= 2)
                {
                    var polyline = new LwPolyline
                    {
                        IsClosed = e.IsClosed,
                        Layer = userLayer
                    };

                    foreach (var point in e.Points)
                    {
                        polyline.Vertices.Add(new LwPolyline.Vertex(new CSMath.XY(point.X, point.Y)));
                    }

                    entity = polyline;
                }
                break;
        }

        if (entity != null)
        {
            var command = new AddEntityCommand(_documentService.CurrentDocument, entity);
            _undoRedoService.Execute(command);

            // Add to entities collection and lookup
            var entityModel = new EntityModel(entity);
            _entityLookup[entity] = entityModel;
            Entities.Add(entityModel);
            EntityCount = Entities.Count;

            StatusText = $"Created {entity.GetType().Name} on layer '{CadDocumentModel.UserDrawingsLayerName}'";

            // Refresh layers panel to show new layer if created
            LayerPanel.RefreshLayers();

            // Trigger render
            EntitiesChanged?.Invoke(this, EventArgs.Empty);
            RenderRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void HandlePlaceUnitNumber(DrawingCompletedEventArgs e)
    {
        if (e.Points.Count < 1 || string.IsNullOrEmpty(e.Text))
            return;

        _documentService.CurrentDocument.EnsureDocumentExists();

        var unitLayer = _documentService.CurrentDocument.GetOrCreateUnitNumbersLayer();
        if (unitLayer == null)
            return;

        var mtext = new MText
        {
            InsertPoint = new CSMath.XYZ(e.Points[0].X, e.Points[0].Y, 0),
            Value = e.Text,
            Height = UnitTextHeight,
            Layer = unitLayer
        };

        var command = new AddEntityCommand(_documentService.CurrentDocument, mtext, $"Place unit number '{e.Text}'");
        _undoRedoService.Execute(command);

        var entityModel = new EntityModel(mtext);
        _entityLookup[mtext] = entityModel;
        Entities.Add(entityModel);
        EntityCount = Entities.Count;

        if (UnitAutoIncrement)
        {
            UnitNextNumber++;
        }

        LayerPanel.RefreshLayers();

        StatusText = $"Placed unit number '{e.Text}' on layer '{CadDocumentModel.UnitNumbersLayerName}'";
        DrawingStatusText = $"Place Unit Number: Click to place '{UnitNumberPrefix}{UnitNextNumber.ToString("D3")}'";

        EntitiesChanged?.Invoke(this, EventArgs.Empty);
        RenderRequested?.Invoke(this, EventArgs.Empty);
    }

    public double ComputeWalkwayNodeRadius()
    {
        var extents = _documentService.CurrentDocument.GetExtents();
        if (extents.IsValid)
        {
            double maxDim = Math.Max(extents.Width, extents.Height);
            return Math.Max(maxDim * 0.003, 0.5);
        }
        return 2.0;
    }

    private void HandleFairwayPlacement(DrawingCompletedEventArgs e)
    {
        if (e.Points.Count < 1)
            return;

        _documentService.CurrentDocument.EnsureDocumentExists();

        var nodePoint = e.Points[^1]; // Last point is the current node
        double? prevX = null, prevY = null;

        if (e.Points.Count >= 2)
        {
            prevX = e.Points[0].X;
            prevY = e.Points[0].Y;
        }

        double nodeRadius = ComputeWalkwayNodeRadius();

        var command = new AddWalkwaySegmentCommand(
            _documentService.CurrentDocument,
            nodePoint.X, nodePoint.Y,
            nodeRadius,
            e.SnappedToHandle,
            prevX, prevY,
            e.PreviousNodeHandle);

        _undoRedoService.Execute(command);

        // Add created entities to our collection
        var doc = _documentService.CurrentDocument;
        if (doc.Document != null)
        {
            foreach (var entity in doc.ModelSpaceEntities)
            {
                if (!_entityLookup.ContainsKey(entity))
                {
                    var model = new EntityModel(entity);
                    _entityLookup[entity] = model;
                    Entities.Add(model);
                }
            }
        }

        EntityCount = Entities.Count;
        LayerPanel.RefreshLayers();
        _walkwayService.RebuildGraph(Entities);

        StatusText = "Added walkway segment";
        EntitiesChanged?.Invoke(this, EventArgs.Empty);
        RenderRequested?.Invoke(this, EventArgs.Empty);
    }

    public void HandleToggleEntrance(ulong handle)
    {
        var doc = _documentService.CurrentDocument;
        if (doc.Document == null)
            return;

        var entity = doc.ModelSpaceEntities
            .OfType<Circle>()
            .FirstOrDefault(c => c.Handle == handle);

        if (entity == null)
            return;

        var command = new ToggleEntranceCommand(entity);
        _undoRedoService.Execute(command);

        _walkwayService.RebuildGraph(Entities);

        bool isEntrance = entity.Color.Index == 3;
        StatusText = isEntrance ? "Node set as entrance" : "Node set as regular";
        RenderRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Returns the existing walkway node positions for FairwayTool snapping.
    /// </summary>
    public List<(ulong handle, double x, double y)> GetWalkwayNodes()
    {
        var nodes = new List<(ulong, double, double)>();
        foreach (var em in Entities)
        {
            if (em.Entity is Circle circle and not Arc &&
                circle.Layer?.Name == CadDocumentModel.WalkwaysLayerName)
            {
                nodes.Add((circle.Handle, circle.Center.X, circle.Center.Y));
            }
        }
        return nodes;
    }

    public double GetWalkwaySnapDistance()
    {
        var extents = _documentService.CurrentDocument.GetExtents();
        if (extents.IsValid)
        {
            double maxDim = Math.Max(extents.Width, extents.Height);
            return Math.Max(maxDim * 0.01, 2.0);
        }
        return 5.0;
    }

    public void EditUnitNumber(MText entity)
    {
        var args = new EditUnitNumberRequestedEventArgs(entity, entity.Value);
        EditUnitNumberRequested?.Invoke(this, args);

        if (!args.Cancelled && args.NewValue != entity.Value)
        {
            var command = new EditUnitNumberCommand(entity, entity.Value, args.NewValue);
            _undoRedoService.Execute(command);
            RenderRequested?.Invoke(this, EventArgs.Empty);
            StatusText = $"Edited unit number '{entity.Value}' -> '{args.NewValue}'";
        }
    }

    /// <summary>
    /// Updates the grid spacing based on the loaded document's extents.
    /// </summary>
    public void UpdateGridSpacingFromExtents()
    {
        var extents = _documentService.CurrentDocument.GetExtents();
        if (extents.IsValid)
        {
            double maxDimension = Math.Max(extents.Width, extents.Height);
            double autoSpacing = GridSnapSettings.CalculateAutoGridSpacing(maxDimension);
            GridSpacing = autoSpacing;
        }
    }

    private void UpdateUnitTextHeightFromExtents()
    {
        var extents = _documentService.CurrentDocument.GetExtents();
        if (extents.IsValid)
        {
            double maxDim = Math.Max(extents.Width, extents.Height);
            UnitTextHeight = Math.Max(maxDim * 0.005, 1.0);
        }
    }

    /// <summary>
    /// Requests the view to zoom to the specified entity.
    /// </summary>
    public void ZoomToEntity(EntityModel entity)
    {
        if (entity?.Entity != null)
        {
            ZoomToEntityRequested?.Invoke(this, new ZoomToEntityEventArgs(entity.Entity));
        }
    }
}

/// <summary>
/// Event args for the ZoomToEntity request.
/// </summary>
public class ZoomToEntityEventArgs : EventArgs
{
    public Entity Entity { get; }

    public ZoomToEntityEventArgs(Entity entity)
    {
        Entity = entity;
    }
}

public class ZoomToAreaEventArgs : EventArgs
{
    public double MinX { get; }
    public double MinY { get; }
    public double MaxX { get; }
    public double MaxY { get; }

    public ZoomToAreaEventArgs(double minX, double minY, double maxX, double maxY)
    {
        MinX = minX;
        MinY = minY;
        MaxX = maxX;
        MaxY = maxY;
    }
}

public class EditUnitNumberRequestedEventArgs : EventArgs
{
    public MText Entity { get; }
    public string CurrentValue { get; }
    public string NewValue { get; set; }
    public bool Cancelled { get; set; } = true;

    public EditUnitNumberRequestedEventArgs(MText entity, string currentValue)
    {
        Entity = entity;
        CurrentValue = currentValue;
        NewValue = currentValue;
    }
}
