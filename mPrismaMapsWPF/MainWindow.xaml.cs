using System.Windows;
using System.Windows.Controls;
using ACadSharp.Entities;
using mPrismaMapsWPF.Commands;
using mPrismaMapsWPF.Controls;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Services;
using mPrismaMapsWPF.ViewModels;
using mPrismaMapsWPF.Views;

namespace mPrismaMapsWPF;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly ISelectionService _selectionService;
    private bool _isUpdatingLayerSelection;

    public MainWindow(MainWindowViewModel viewModel, ISelectionService selectionService)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _selectionService = selectionService;
        DataContext = _viewModel;

        _viewModel.ZoomToFitRequested += OnZoomToFitRequested;
        _viewModel.RenderRequested += OnRenderRequested;
        _viewModel.EntitiesChanged += OnEntitiesChanged;
        _viewModel.SelectEntityTypesRequested += OnSelectEntityTypesRequested;
        _viewModel.LayerPanel.LayerVisibilityChanged += OnLayerVisibilityChanged;
        _viewModel.LayerPanel.DeleteLayerRequested += OnDeleteLayerRequested;
        _viewModel.LayerPanel.DeleteMultipleLayersRequested += OnDeleteMultipleLayersRequested;
        _viewModel.LayerPanel.LayersChanged += OnLayersChanged;
        _viewModel.PropertiesPanel.PropertiesUpdated += OnPropertiesUpdated;
        _viewModel.UndoRedoService.StateChanged += OnUndoRedoStateChanged;
        _selectionService.SelectionChanged += OnEntitySelectionChanged;

        CadCanvas.CadMouseMove += OnCadMouseMove;
        CadCanvas.EntityClicked += OnEntityClicked;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateCanvasBindings();
        UpdateDeleteByTypeMenu();
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
    }

    private void OnZoomToFitRequested(object? sender, EventArgs e)
    {
        CadCanvas.Extents = _viewModel.Document.GetExtents();
        CadCanvas.ZoomToFit();
    }

    private void OnRenderRequested(object? sender, EventArgs e)
    {
        UpdateCanvasBindings();
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
            var entityModel = _viewModel.Entities.FirstOrDefault(em => em.Entity == e.Entity);
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
        // When entities are selected, also select their corresponding layers
        var selectedEntities = _selectionService.SelectedEntities;

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
}
