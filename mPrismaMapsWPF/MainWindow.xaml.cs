using System.Windows;
using mPrismaMapsWPF.Controls;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Services;
using mPrismaMapsWPF.ViewModels;

namespace mPrismaMapsWPF;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly ISelectionService _selectionService;

    public MainWindow(MainWindowViewModel viewModel, ISelectionService selectionService)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _selectionService = selectionService;
        DataContext = _viewModel;

        _viewModel.ZoomToFitRequested += OnZoomToFitRequested;
        _viewModel.RenderRequested += OnRenderRequested;
        _viewModel.LayerPanel.LayerVisibilityChanged += OnLayerVisibilityChanged;

        CadCanvas.CadMouseMove += OnCadMouseMove;
        CadCanvas.EntityClicked += OnEntityClicked;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateCanvasBindings();
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
}
