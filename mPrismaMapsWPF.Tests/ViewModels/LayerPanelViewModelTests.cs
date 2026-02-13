using ACadSharp;
using ACadSharp.Tables;
using FluentAssertions;
using Moq;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Services;
using mPrismaMapsWPF.ViewModels;

namespace mPrismaMapsWPF.Tests.ViewModels;

public class LayerPanelViewModelTests
{
    private readonly Mock<IDocumentService> _docService;
    private readonly Mock<IUndoRedoService> _undoRedoService;
    private readonly CadDocumentModel _document;
    private readonly LayerPanelViewModel _viewModel;

    public LayerPanelViewModelTests()
    {
        _docService = new Mock<IDocumentService>();
        _undoRedoService = new Mock<IUndoRedoService>();
        _document = new CadDocumentModel();
        _document.Load(new CadDocument(), "test.dwg");
        _docService.Setup(d => d.CurrentDocument).Returns(_document);

        _viewModel = new LayerPanelViewModel(_docService.Object, _undoRedoService.Object);
    }

    private LayerModel AddLayer(string name, short colorIndex = 7)
    {
        var layer = new Layer(name) { Color = new ACadSharp.Color(colorIndex) };
        _document.Document!.Layers.Add(layer);
        var layerModel = new LayerModel(layer);
        _viewModel.Layers.Add(layerModel);
        return layerModel;
    }

    [Fact]
    public void ShowAllLayers_SetsAllVisible()
    {
        var layer1 = AddLayer("Layer1");
        var layer2 = AddLayer("Layer2");
        layer1.IsVisible = false;
        layer2.IsVisible = false;

        _viewModel.ShowAllLayersCommand.Execute(null);

        layer1.IsVisible.Should().BeTrue();
        layer2.IsVisible.Should().BeTrue();
    }

    [Fact]
    public void HideAllLayers_SetsAllInvisible()
    {
        var layer1 = AddLayer("Layer1");
        var layer2 = AddLayer("Layer2");

        _viewModel.HideAllLayersCommand.Execute(null);

        layer1.IsVisible.Should().BeFalse();
        layer2.IsVisible.Should().BeFalse();
    }

    [Fact]
    public void IsolateSelectedLayer_ShowsOnlySelected()
    {
        var layer1 = AddLayer("Layer1");
        var layer2 = AddLayer("Layer2");
        _viewModel.SelectedLayer = layer1;

        _viewModel.IsolateSelectedLayerCommand.Execute(null);

        layer1.IsVisible.Should().BeTrue();
        layer2.IsVisible.Should().BeFalse();
    }

    [Fact]
    public void ToggleSelectedLayersVisibility_TogglesAll()
    {
        var layer1 = AddLayer("Layer1");
        var layer2 = AddLayer("Layer2");
        _viewModel.SelectedLayers.Add(layer1);
        _viewModel.SelectedLayers.Add(layer2);

        // All visible → hide all
        _viewModel.ToggleSelectedLayersVisibilityCommand.Execute(null);

        layer1.IsVisible.Should().BeFalse();
        layer2.IsVisible.Should().BeFalse();
    }

    [Fact]
    public void ToggleSelectedLayersLock_TogglesBetweenLockedAndUnlocked()
    {
        var layer1 = AddLayer("Layer1");
        var layer2 = AddLayer("Layer2");
        _viewModel.SelectedLayers.Add(layer1);
        _viewModel.SelectedLayers.Add(layer2);

        _viewModel.ToggleSelectedLayersLockCommand.Execute(null);

        layer1.IsLocked.Should().BeTrue();
        layer2.IsLocked.Should().BeTrue();
    }

    [Fact]
    public void LayerVisibilityChanged_FiresOnShow()
    {
        AddLayer("Layer1");
        bool fired = false;
        _viewModel.LayerVisibilityChanged += (_, _) => fired = true;

        _viewModel.ShowAllLayersCommand.Execute(null);

        fired.Should().BeTrue();
    }

    [Fact]
    public void RefreshLayers_PopulatesFromDocument()
    {
        // Add layers to document
        _document.Document!.Layers.Add(new Layer("Custom1"));
        _document.Document!.Layers.Add(new Layer("Custom2"));

        _viewModel.RefreshLayers();

        // Document always has "0" layer + our 2 = at least 2
        _viewModel.Layers.Should().HaveCountGreaterThanOrEqualTo(2);
    }
}
