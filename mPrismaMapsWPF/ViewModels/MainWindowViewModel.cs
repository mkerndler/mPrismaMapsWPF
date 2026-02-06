using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using ACadSharp.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using mPrismaMapsWPF.Commands;
using mPrismaMapsWPF.Drawing;
using mPrismaMapsWPF.Helpers;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Services;
using WpfPoint = System.Windows.Point;

namespace mPrismaMapsWPF.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IDocumentService _documentService;
    private readonly ISelectionService _selectionService;
    private readonly IUndoRedoService _undoRedoService;

    public MainWindowViewModel(
        IDocumentService documentService,
        ISelectionService selectionService,
        IUndoRedoService undoRedoService)
    {
        _documentService = documentService;
        _selectionService = selectionService;
        _undoRedoService = undoRedoService;

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
                var scanResults = await _documentService.ScanEntityTypesAsync(dialog.FileName);

                IsLoading = false;

                // Raise event for view to show dialog
                SelectEntityTypesRequested?.Invoke(this,
                    new SelectEntityTypesEventArgs(dialog.FileName, scanResults));
            }
            catch (Exception)
            {
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

        IsLoading = true;
        StatusText = "Saving...";

        bool success = await _documentService.SaveAsync();

        IsLoading = false;
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
        _selectionService.SelectMultiple(Entities);
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
        var selectedEntities = _selectionService.SelectedEntities.ToList();
        if (selectedEntities.Count == 0)
            return;

        var command = new DeleteEntitiesCommand(_documentService.CurrentDocument, selectedEntities);
        _undoRedoService.Execute(command);

        // Clear selection and remove from entities collection
        _selectionService.ClearSelection();

        foreach (var entityModel in selectedEntities)
        {
            _entityLookup.Remove(entityModel.Entity);
            Entities.Remove(entityModel);
        }

        RefreshAfterEdit();
    }

    private bool CanDeleteSelected() => _selectionService.SelectedEntities.Count > 0;

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
        RenderRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Handles the completion of a drawing operation and creates the corresponding entity.
    /// </summary>
    public void OnDrawingCompleted(DrawingCompletedEventArgs e)
    {
        if (e.Points.Count < 2)
            return;

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
