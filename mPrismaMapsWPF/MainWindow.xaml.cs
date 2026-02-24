using System.Windows;
using System.Windows.Controls;
using ACadSharp.Entities;
using mPrismaMapsWPF.Commands;
using mPrismaMapsWPF.Controls;
using mPrismaMapsWPF.Drawing;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Services;
using mPrismaMapsWPF.ViewModels;
using mPrismaMapsWPF.Views;

namespace mPrismaMapsWPF;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly ISelectionService _selectionService;
    private readonly IBackupService _backupService;
    private bool _isUpdatingLayerSelection;
    private bool _isUpdatingEntitySelection;

    public MainWindow(MainWindowViewModel viewModel, ISelectionService selectionService, IBackupService backupService)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _selectionService = selectionService;
        _backupService = backupService;
        DataContext = _viewModel;

        _viewModel.ZoomToFitRequested += OnZoomToFitRequested;
        _viewModel.RenderRequested += OnRenderRequested;
        _viewModel.EntitiesChanged += OnEntitiesChanged;
        _viewModel.SelectEntityTypesRequested += OnSelectEntityTypesRequested;
        _viewModel.CenterOnOriginRequested += OnCenterOnOriginRequested;
        _viewModel.ResetViewTransformsRequested += OnResetViewTransformsRequested;
        _viewModel.RotateViewRequested += OnRotateViewRequested;
        _viewModel.DeleteOutsideViewportRequested += OnDeleteOutsideViewportRequested;
        _viewModel.ZoomToEntityRequested += OnZoomToEntityRequested;
        _viewModel.LayerPanel.LayerVisibilityChanged += OnLayerVisibilityChanged;
        _viewModel.LayerPanel.LayerLockChanged += OnLayerLockChanged;
        _viewModel.LayerPanel.DeleteLayerRequested += OnDeleteLayerRequested;
        _viewModel.LayerPanel.DeleteMultipleLayersRequested += OnDeleteMultipleLayersRequested;
        _viewModel.LayerPanel.LayersChanged += OnLayersChanged;
        _viewModel.PropertiesPanel.PropertiesUpdated += OnPropertiesUpdated;
        _viewModel.UndoRedoService.StateChanged += OnUndoRedoStateChanged;
        _selectionService.SelectionChanged += OnEntitySelectionChanged;

        _viewModel.ZoomToAreaRequested += OnZoomToAreaRequested;

        CadCanvas.CadMouseMove += OnCadMouseMove;
        CadCanvas.EntityClicked += OnEntityClicked;
        CadCanvas.EntityDoubleClicked += OnEntityDoubleClicked;
        CadCanvas.DrawingCompleted += OnDrawingCompleted;
        CadCanvas.MarqueeSelectionCompleted += OnMarqueeSelectionCompleted;
        CadCanvas.MoveCompleted += OnMoveCompleted;
        CadCanvas.TransformCompleted += OnTransformCompleted;
        CadCanvas.ToggleEntranceRequested += OnToggleEntranceRequested;

        _viewModel.EditUnitNumberRequested += OnEditUnitNumberRequested;
        _viewModel.ResizeUnitNumbersRequested += OnResizeUnitNumbersRequested;
        _viewModel.ExportMpolRequested += OnExportMpolRequested;
        _viewModel.DeployMpolRequested += OnDeployMpolRequested;
        _viewModel.RestoreBackupRequested += OnRestoreBackupRequested;
        _viewModel.ScaleMapRequested += OnScaleMapRequested;

        // Set up drawing mode binding
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        Loaded += OnLoaded;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.DrawingMode))
        {
            CadCanvas.DrawingMode = _viewModel.DrawingMode;
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.FlipX))
        {
            CadCanvas.FlipX = _viewModel.FlipX;
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.FlipY))
        {
            CadCanvas.FlipY = _viewModel.FlipY;
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.ViewRotation))
        {
            CadCanvas.ViewRotation = _viewModel.ViewRotation;
        }
        else if (e.PropertyName is nameof(MainWindowViewModel.UnitNextNumber) or
                 nameof(MainWindowViewModel.UnitNumberPrefix) or
                 nameof(MainWindowViewModel.UnitTextHeight))
        {
            CadCanvas.ConfigureUnitNumberTool(
                _viewModel.UnitNumberPrefix,
                _viewModel.UnitNextNumber,
                "D3",
                _viewModel.UnitTextHeight);
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.HighlightedPathHandles))
        {
            CadCanvas.HighlightedPathHandles = _viewModel.HighlightedPathHandles;
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.DrawingMode) &&
                 _viewModel.DrawingMode == Drawing.DrawingMode.DrawFairway)
        {
            // Configure fairway tool with existing nodes for snapping
            CadCanvas.ConfigureFairwayTool(
                _viewModel.GetWalkwayNodes(),
                _viewModel.GetWalkwaySnapDistance(),
                _viewModel.ComputeWalkwayNodeRadius());
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateCanvasBindings();
        UpdateDeleteByTypeMenu();

        // Initialize drawing-related canvas properties
        CadCanvas.GridSettings = _viewModel.GridSettings;
        CadCanvas.DrawingMode = _viewModel.DrawingMode;

        // Initialize view transform properties
        CadCanvas.FlipX = _viewModel.FlipX;
        CadCanvas.FlipY = _viewModel.FlipY;
        CadCanvas.ViewRotation = _viewModel.ViewRotation;
    }

    private void OnDrawingCompleted(object? sender, DrawingCompletedEventArgs e)
    {
        _viewModel.OnDrawingCompleted(e);
        UpdateCanvasBindings();

        // After fairway placement, update the tool's existing nodes for snapping
        if (e.Mode == Drawing.DrawingMode.DrawFairway)
        {
            CadCanvas.ConfigureFairwayTool(
                _viewModel.GetWalkwayNodes(),
                _viewModel.GetWalkwaySnapDistance(),
                _viewModel.ComputeWalkwayNodeRadius());
        }
    }

    private void OnToggleEntranceRequested(object? sender, Controls.ToggleEntranceEventArgs e)
    {
        _viewModel.HandleToggleEntrance(e.Handle);
        UpdateCanvasBindings();
    }

    private void UpdateDeleteByTypeMenu()
    {
        DeleteByTypeMenu.Items.Clear();

        var entityTypes = _viewModel.GetEntityTypes();
        if (!entityTypes.Any())
        {
            var noItemsMenuItem = new MenuItem
            {
                Header = "(No entities)",
                IsEnabled = false
            };
            DeleteByTypeMenu.Items.Add(noItemsMenuItem);
            return;
        }

        foreach (var entityType in entityTypes)
        {
            var menuItem = new MenuItem
            {
                Header = entityType.Name,
                Tag = entityType
            };
            menuItem.Click += DeleteByTypeMenuItem_Click;
            DeleteByTypeMenu.Items.Add(menuItem);
        }
    }

    private void DeleteByTypeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is Type entityType)
        {
            _viewModel.DeleteByTypeCommand.Execute(entityType);
            UpdateDeleteByTypeMenu();
        }
    }

    private void UpdateCanvasBindings()
    {
        CadCanvas.Entities = _viewModel.Document.ModelSpaceEntities;
        CadCanvas.Extents = _viewModel.Document.GetExtents();

        var selectedHandles = _viewModel.Entities
            .Where(e => e.IsSelected)
            .Select(e => e.Handle)
            .ToList();
        CadCanvas.SelectedHandles = selectedHandles;

        var hiddenLayers = _viewModel.LayerPanel.Layers
            .Where(l => !l.IsVisible)
            .Select(l => l.Name)
            .ToList();
        CadCanvas.HiddenLayers = hiddenLayers;

        var lockedLayers = _viewModel.LayerPanel.Layers
            .Where(l => l.IsLocked)
            .Select(l => l.Name)
            .ToList();
        CadCanvas.LockedLayers = lockedLayers;

        var lockedHandles = _viewModel.Entities
            .Where(e => e.IsLocked)
            .Select(e => e.Handle)
            .ToList();
        CadCanvas.LockedHandles = lockedHandles;
    }

    private void OnZoomToFitRequested(object? sender, EventArgs e)
    {
        CadCanvas.Extents = _viewModel.Document.GetExtents();
        CadCanvas.ZoomToFit();
    }

    private void OnRenderRequested(object? sender, EventArgs e)
    {
        UpdateCanvasBindings();
        CadCanvas.InvalidateCache();
        CadCanvas.Render();
    }

    private void OnLayerVisibilityChanged(object? sender, EventArgs e)
    {
        var hiddenLayers = _viewModel.LayerPanel.Layers
            .Where(l => !l.IsVisible)
            .Select(l => l.Name)
            .ToList();
        CadCanvas.HiddenLayers = hiddenLayers;
        CadCanvas.Render();
    }

    private void OnLayerLockChanged(object? sender, EventArgs e)
    {
        UpdateCanvasBindings();
        CadCanvas.Render();
    }

    private void OnCadMouseMove(object? sender, CadMouseEventArgs e)
    {
        _viewModel.UpdateMousePosition(e.X, e.Y);
    }

    private void OnEntityClicked(object? sender, CadEntityClickEventArgs e)
    {
        if (e.Entity == null)
        {
            if (!e.AddToSelection)
            {
                _selectionService.ClearSelection();
            }
        }
        else
        {
            var entityModel = _viewModel.GetEntityModel(e.Entity);
            if (entityModel != null)
            {
                if (e.AddToSelection)
                {
                    _selectionService.ToggleSelection(entityModel);
                }
                else
                {
                    _selectionService.Select(entityModel);
                }
            }
        }

        var selectedHandles = _selectionService.SelectedEntities
            .Select(e => e.Handle)
            .ToList();
        CadCanvas.SelectedHandles = selectedHandles;
        CadCanvas.Render();
    }

    private void OnZoomToAreaRequested(object? sender, ZoomToAreaEventArgs e)
    {
        CadCanvas.ZoomToRect(e.MinX, e.MinY, e.MaxX, e.MaxY);
    }

    private void OnMarqueeSelectionCompleted(object? sender, MarqueeSelectionEventArgs e)
    {
        var entityModels = new List<EntityModel>();
        foreach (var entity in e.SelectedEntities)
        {
            var model = _viewModel.GetEntityModel(entity);
            if (model != null)
                entityModels.Add(model);
        }

        if (entityModels.Count > 0)
        {
            _selectionService.SelectMultiple(entityModels, e.AddToSelection);
        }
        else if (!e.AddToSelection)
        {
            _selectionService.ClearSelection();
        }

        var selectedHandles = _selectionService.SelectedEntities
            .Select(em => em.Handle)
            .ToList();
        CadCanvas.SelectedHandles = selectedHandles;
        CadCanvas.Render();
    }

    private void OnMoveCompleted(object? sender, MoveCompletedEventArgs e)
    {
        _viewModel.OnMoveCompleted(e);
        UpdateCanvasBindings();
        CadCanvas.RebuildSpatialIndex();
    }

    private void OnTransformCompleted(object? sender, TransformCompletedEventArgs e)
    {
        _viewModel.OnTransformCompleted(e);
        UpdateCanvasBindings();
        CadCanvas.RebuildSpatialIndex();
    }

    private void OnEntitiesChanged(object? sender, EventArgs e)
    {
        UpdateCanvasBindings();
        UpdateDeleteByTypeMenu();
    }

    private void OnDeleteLayerRequested(object? sender, DeleteLayerRequestedEventArgs e)
    {
        var dialog = new DeleteLayerDialog(e.Layer, e.AvailableLayers, e.EntityCount)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            _viewModel.LayerPanel.ExecuteDeleteLayer(e.Layer, dialog.DeleteOption, dialog.TargetLayer);
            _viewModel.RefreshEntities();
            UpdateDeleteByTypeMenu();
        }
    }

    private void OnLayersChanged(object? sender, EventArgs e)
    {
        // Refresh layer panel display
        _viewModel.LayerPanel.RefreshLayers();
    }

    private void OnPropertiesUpdated(object? sender, EventArgs e)
    {
        CadCanvas.Render();
    }

    private void OnUndoRedoStateChanged(object? sender, EventArgs e)
    {
        // After undo/redo, refresh entities from document
        _viewModel.RefreshEntities();
        _viewModel.LayerPanel.RefreshLayers();
        UpdateDeleteByTypeMenu();
        CadCanvas.RebuildSpatialIndex();
    }

    private void LayersListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_isUpdatingLayerSelection)
            return;

        var selectedLayers = LayersListBox.SelectedItems.Cast<LayerModel>().ToList();
        _viewModel.LayerPanel.UpdateSelectedLayersFromUI(selectedLayers);
    }

    private void OnEntitySelectionChanged(object? sender, Services.SelectionChangedEventArgs e)
    {
        var selectedEntities = _selectionService.SelectedEntities;

        // Sync canvas selection to entity list
        if (!_isUpdatingEntitySelection)
        {
            _isUpdatingEntitySelection = true;
            try
            {
                EntityListBox.SelectedItems.Clear();
                foreach (var entity in selectedEntities)
                {
                    EntityListBox.SelectedItems.Add(entity);
                }

                // Scroll the first selected item into view
                if (selectedEntities.Count > 0)
                {
                    EntityListBox.ScrollIntoView(selectedEntities.First());
                }
            }
            finally
            {
                _isUpdatingEntitySelection = false;
            }
        }

        if (selectedEntities.Count == 0)
        {
            // Don't clear layer selection when entities are deselected
            return;
        }

        // Get unique layer names from selected entities
        var layerNames = selectedEntities
            .Select(entity => entity.LayerName)
            .Distinct()
            .ToList();

        // Update layer selection in the UI
        _isUpdatingLayerSelection = true;
        try
        {
            _viewModel.LayerPanel.SelectLayersForEntities(layerNames);

            // Update the ListBox selection to match
            LayersListBox.SelectedItems.Clear();
            foreach (var layer in _viewModel.LayerPanel.Layers.Where(l => l.IsSelected))
            {
                LayersListBox.SelectedItems.Add(layer);
            }
        }
        finally
        {
            _isUpdatingLayerSelection = false;
        }
    }

    private void OnDeleteMultipleLayersRequested(object? sender, DeleteMultipleLayersRequestedEventArgs e)
    {
        var dialog = new DeleteMultipleLayersDialog(e.Layers, e.AvailableLayers)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            // Create deletion list with the same option for all layers
            var deletions = e.Layers.Select(info =>
                (info.Layer, dialog.DeleteOption, dialog.TargetLayer)).ToList();

            _viewModel.LayerPanel.ExecuteDeleteMultipleLayers(deletions);
            _viewModel.RefreshEntities();
            UpdateDeleteByTypeMenu();
        }
    }

    private async void OnSelectEntityTypesRequested(object? sender, SelectEntityTypesEventArgs e)
    {
        var dialog = new SelectEntityTypesDialog(e.ScanResults)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            await _viewModel.LoadFileWithFilterAsync(e.FilePath, dialog.ExcludedTypes);
        }
    }

    private void OnCenterOnOriginRequested(object? sender, EventArgs e)
    {
        CadCanvas.CenterOnOrigin();
    }

    private void OnResetViewTransformsRequested(object? sender, EventArgs e)
    {
        CadCanvas.ResetViewTransforms();
    }

    private void OnRotateViewRequested(object? sender, RotateViewEventArgs e)
    {
        var dialog = new RotateViewDialog(e.CurrentAngle)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            _viewModel.ViewRotation = dialog.Angle;
        }
    }

    private void OnScaleMapRequested(object? sender, ScaleMapRequestedEventArgs e)
    {
        var dialog = new ScaleMapDialog
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            e.ScaleFactor = dialog.ScaleFactor;
            e.Confirmed = true;
        }
    }

    private void OnDeleteOutsideViewportRequested(object? sender, DeleteOutsideViewportEventArgs e)
    {
        e.ViewportBounds = CadCanvas.GetViewportBounds();
        e.Cancelled = false;
    }

    private void OnZoomToEntityRequested(object? sender, ZoomToEntityEventArgs e)
    {
        CadCanvas.ZoomToEntity(e.Entity);
    }

    private void EntityListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_isUpdatingEntitySelection)
            return;

        _isUpdatingEntitySelection = true;
        try
        {
            // Sync entity list selection to canvas
            var selectedEntities = EntityListBox.SelectedItems.Cast<EntityModel>().ToList();

            // Update selection service
            if (selectedEntities.Count == 0)
            {
                _selectionService.ClearSelection();
            }
            else
            {
                _selectionService.SelectMultiple(selectedEntities, addToSelection: false);
            }

            // Update canvas
            var selectedHandles = selectedEntities.Select(e => e.Handle).ToList();
            CadCanvas.SelectedHandles = selectedHandles;
            CadCanvas.Render();
        }
        finally
        {
            _isUpdatingEntitySelection = false;
        }
    }

    private void EntityListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Zoom to the double-clicked entity
        if (EntityListBox.SelectedItem is EntityModel entity)
        {
            _viewModel.ZoomToEntity(entity);
        }
    }

    private void ZoomToEntity_Click(object sender, RoutedEventArgs e)
    {
        if (EntityListBox.SelectedItem is EntityModel entity)
        {
            _viewModel.ZoomToEntity(entity);
        }
    }

    private void DeleteEntity_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.DeleteSelectedCommand.Execute(null);
    }

    private void ShowProperties_Click(object sender, RoutedEventArgs e)
    {
        // Scroll the properties panel into view - the properties panel is already bound to selection
        // This is a no-op since properties are already shown, but could be used to scroll/focus
    }

    private void EntityTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (_isUpdatingEntitySelection)
            return;

        // Only handle entity selection, not group selection
        if (e.NewValue is EntityModel entity)
        {
            _isUpdatingEntitySelection = true;
            try
            {
                _selectionService.Select(entity);

                var selectedHandles = new List<ulong> { entity.Handle };
                CadCanvas.SelectedHandles = selectedHandles;
                CadCanvas.Render();
            }
            finally
            {
                _isUpdatingEntitySelection = false;
            }
        }
    }

    private void EntityTreeView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Zoom to the double-clicked entity
        if (EntityTreeView.SelectedItem is EntityModel entity)
        {
            _viewModel.ZoomToEntity(entity);
        }
    }

    private void TreeViewZoomToEntity_Click(object sender, RoutedEventArgs e)
    {
        // Get the entity from the context menu's data context
        if (sender is MenuItem menuItem &&
            menuItem.Parent is ContextMenu contextMenu &&
            contextMenu.PlacementTarget is FrameworkElement element &&
            element.DataContext is EntityModel entity)
        {
            _viewModel.ZoomToEntity(entity);
        }
        else if (EntityTreeView.SelectedItem is EntityModel selectedEntity)
        {
            _viewModel.ZoomToEntity(selectedEntity);
        }
    }

    private void TreeViewDeleteEntity_Click(object sender, RoutedEventArgs e)
    {
        // Select the entity first if coming from context menu
        if (sender is MenuItem menuItem &&
            menuItem.Parent is ContextMenu contextMenu &&
            contextMenu.PlacementTarget is FrameworkElement element &&
            element.DataContext is EntityModel entity)
        {
            _selectionService.Select(entity);
        }

        _viewModel.DeleteSelectedCommand.Execute(null);
    }

    private void OnEntityDoubleClicked(object? sender, CadEntityClickEventArgs e)
    {
        if (e.Entity is MText mtext)
        {
            _viewModel.EditUnitNumber(mtext);
        }
    }

    private void OnEditUnitNumberRequested(object? sender, EditUnitNumberRequestedEventArgs e)
    {
        var dialog = new EditUnitNumberDialog(e.CurrentValue)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            e.NewValue = dialog.UnitNumberValue;
            e.Cancelled = false;
        }
    }

    private void OnResizeUnitNumbersRequested(double suggestedHeight)
    {
        var dialog = new ResizeUnitNumbersDialog(suggestedHeight) { Owner = this };
        if (dialog.ShowDialog() == true)
            _viewModel.ApplyResizeUnitNumbers(dialog.NewHeight);
    }

    private void OnExportMpolRequested(object? sender, ExportMpolRequestedEventArgs e)
    {
        var nameDialog = new ExportMpolDialog(e.StoreName)
        {
            Owner = this
        };

        if (nameDialog.ShowDialog() != true)
            return;

        e.StoreName = nameDialog.StoreName;

        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "JSON Files (*.json)|*.json",
            FilterIndex = 1,
            Title = "Export MPOL",
            FileName = nameDialog.StoreName + ".json"
        };

        if (saveDialog.ShowDialog() == true)
        {
            e.FilePath = saveDialog.FileName;
            e.Cancelled = false;
        }
    }

    private void OnDeployMpolRequested(object? sender, DeployMpolRequestedEventArgs e)
    {
        var dialog = new DeployMpolDialog(e.StoreName)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            e.StoreName = dialog.StoreName;
            e.StoreId = dialog.StoreId;
            e.Floor = dialog.Floor;
            e.Server = dialog.Server;
            e.Username = dialog.Username;
            e.Password = dialog.Password;
            e.Cancelled = false;
        }
    }

    private async void OnRestoreBackupRequested(object? sender, RestoreBackupRequestedEventArgs e)
    {
        var backups = await _backupService.ListBackupsAsync();

        if (backups.Count == 0)
        {
            MessageBox.Show("No backups found.", "Restore Backup", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new RestoreBackupDialog(backups)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            e.SelectedBackup = dialog.SelectedBackup;
            e.Server = dialog.Server;
            e.Username = dialog.Username;
            e.Password = dialog.Password;
            e.Cancelled = false;
        }
    }

    private void LayerLockIcon_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is LayerModel layer)
        {
            layer.IsLocked = !layer.IsLocked;
            e.Handled = true;
        }
    }

    private void TreeViewShowProperties_Click(object sender, RoutedEventArgs e)
    {
        // Select the entity to show its properties
        if (sender is MenuItem menuItem &&
            menuItem.Parent is ContextMenu contextMenu &&
            contextMenu.PlacementTarget is FrameworkElement element &&
            element.DataContext is EntityModel entity)
        {
            _selectionService.Select(entity);
        }
    }
}
