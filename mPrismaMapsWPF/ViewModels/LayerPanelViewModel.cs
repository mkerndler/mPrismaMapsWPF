using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Services;

namespace mPrismaMapsWPF.ViewModels;

public partial class LayerPanelViewModel : ObservableObject
{
    private readonly IDocumentService _documentService;

    public LayerPanelViewModel(IDocumentService documentService)
    {
        _documentService = documentService;
        _documentService.DocumentLoaded += OnDocumentLoaded;
        _documentService.DocumentClosed += OnDocumentClosed;
    }

    public ObservableCollection<LayerModel> Layers { get; } = new();

    [ObservableProperty]
    private LayerModel? _selectedLayer;

    public event EventHandler? LayerVisibilityChanged;

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

    public void OnLayerVisibilityToggled(LayerModel layer)
    {
        _documentService.CurrentDocument.IsDirty = true;
        LayerVisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnDocumentLoaded(object? sender, DocumentLoadedEventArgs e)
    {
        Layers.Clear();
        foreach (var layer in _documentService.CurrentDocument.Layers)
        {
            var layerModel = new LayerModel(layer);
            layerModel.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(LayerModel.IsVisible))
                {
                    OnLayerVisibilityToggled(layerModel);
                }
            };
            Layers.Add(layerModel);
        }
    }

    private void OnDocumentClosed(object? sender, EventArgs e)
    {
        Layers.Clear();
        SelectedLayer = null;
    }
}
