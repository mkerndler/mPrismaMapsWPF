using System.Collections.ObjectModel;
using ACadSharp;
using ACadSharp.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using mPrismaMapsWPF.Commands;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Services;

namespace mPrismaMapsWPF.ViewModels;

public partial class PropertiesPanelViewModel : ObservableObject
{
    private readonly ISelectionService _selectionService;
    private readonly IDocumentService _documentService;
    private readonly IUndoRedoService _undoRedoService;

    public PropertiesPanelViewModel(
        ISelectionService selectionService,
        IDocumentService documentService,
        IUndoRedoService undoRedoService)
    {
        _selectionService = selectionService;
        _documentService = documentService;
        _undoRedoService = undoRedoService;
        _selectionService.SelectionChanged += OnSelectionChanged;

        // Initialize color items
        foreach (var colorItem in ColorItem.StandardColors)
        {
            AvailableColors.Add(colorItem);
        }
    }

    public ObservableCollection<PropertyItem> Properties { get; } = new();

    [ObservableProperty]
    private string _selectionSummary = "No selection";

    [ObservableProperty]
    private bool _hasSelection;

    [ObservableProperty]
    private string? _selectedLayer;

    [ObservableProperty]
    private string? _selectedColor;

    [ObservableProperty]
    private ColorItem? _selectedColorItem;

    public ObservableCollection<string> AvailableLayers { get; } = new();
    public ObservableCollection<ColorItem> AvailableColors { get; } = new();

    public event EventHandler? PropertiesUpdated;

    [RelayCommand]
    private void ApplyLayerChange()
    {
        if (string.IsNullOrEmpty(SelectedLayer) || !HasSelection)
            return;

        var targetLayer = _documentService.CurrentDocument.Layers
            .FirstOrDefault(l => l.Name == SelectedLayer);

        if (targetLayer == null)
            return;

        var command = new ChangeEntityLayerCommand(
            _documentService.CurrentDocument,
            _selectionService.SelectedEntities,
            targetLayer);

        _undoRedoService.Execute(command);
        RefreshProperties();
        PropertiesUpdated?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void ApplyColorChange()
    {
        if (SelectedColorItem == null || !HasSelection)
            return;

        var command = new ChangeEntityColorCommand(
            _documentService.CurrentDocument,
            _selectionService.SelectedEntities,
            SelectedColorItem.AcadColor);

        _undoRedoService.Execute(command);
        RefreshProperties();
        PropertiesUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        RefreshProperties();
    }

    private void RefreshProperties()
    {
        Properties.Clear();
        AvailableLayers.Clear();

        foreach (var layer in _documentService.CurrentDocument.Layers)
        {
            AvailableLayers.Add(layer.Name);
        }

        var selected = _selectionService.SelectedEntities;
        HasSelection = selected.Count > 0;

        if (selected.Count == 0)
        {
            SelectionSummary = "No selection";
            SelectedLayer = null;
            SelectedColor = null;
            SelectedColorItem = null;
            return;
        }

        if (selected.Count == 1)
        {
            var entity = selected.First();
            SelectionSummary = entity.DisplayName;
            SelectedLayer = entity.LayerName;
            SelectedColor = entity.GetProperty("Color");

            // Set color item for single selection
            var color = entity.Entity.Color;
            if (color.IsByLayer)
            {
                SelectedColorItem = AvailableColors.FirstOrDefault(c => c.IsByLayer);
            }
            else if (color.IsByBlock)
            {
                SelectedColorItem = AvailableColors.FirstOrDefault(c => c.IsByBlock);
            }
            else
            {
                SelectedColorItem = AvailableColors.FirstOrDefault(c => c.AciIndex == color.Index);
            }

            AddCommonProperties(entity);
            AddEntitySpecificProperties(entity);
        }
        else
        {
            SelectionSummary = $"{selected.Count} entities selected";

            var layers = selected.Select(e => e.LayerName).Distinct().ToList();
            SelectedLayer = layers.Count == 1 ? layers[0] : "(varies)";

            var colors = selected.Select(e => e.GetProperty("Color")).Distinct().ToList();
            SelectedColor = colors.Count == 1 ? colors[0] : "(varies)";

            // For multiple selection, only set color item if all have same color
            if (colors.Count == 1)
            {
                var firstEntity = selected.First().Entity;
                var color = firstEntity.Color;
                if (color.IsByLayer)
                {
                    SelectedColorItem = AvailableColors.FirstOrDefault(c => c.IsByLayer);
                }
                else if (color.IsByBlock)
                {
                    SelectedColorItem = AvailableColors.FirstOrDefault(c => c.IsByBlock);
                }
                else
                {
                    SelectedColorItem = AvailableColors.FirstOrDefault(c => c.AciIndex == color.Index);
                }
            }
            else
            {
                SelectedColorItem = null;
            }

            Properties.Add(new PropertyItem("Count", selected.Count.ToString()));
            Properties.Add(new PropertyItem("Types", string.Join(", ", selected.Select(e => e.TypeName).Distinct())));
            Properties.Add(new PropertyItem("Layer", SelectedLayer ?? ""));
            Properties.Add(new PropertyItem("Color", SelectedColor ?? ""));
        }
    }

    private void AddCommonProperties(EntityModel entity)
    {
        Properties.Add(new PropertyItem("Type", entity.TypeName));
        Properties.Add(new PropertyItem("Handle", entity.Handle.ToString("X")));
        Properties.Add(new PropertyItem("Layer", entity.LayerName));
        Properties.Add(new PropertyItem("Color", entity.GetProperty("Color") ?? ""));
    }

    private void AddEntitySpecificProperties(EntityModel entityModel)
    {
        var entity = entityModel.Entity;

        switch (entity)
        {
            case Line line:
                Properties.Add(new PropertyItem("Start X", line.StartPoint.X.ToString("F4")));
                Properties.Add(new PropertyItem("Start Y", line.StartPoint.Y.ToString("F4")));
                Properties.Add(new PropertyItem("End X", line.EndPoint.X.ToString("F4")));
                Properties.Add(new PropertyItem("End Y", line.EndPoint.Y.ToString("F4")));
                Properties.Add(new PropertyItem("Length", entityModel.GetProperty("Length") ?? ""));
                break;

            case Arc arc:
                Properties.Add(new PropertyItem("Center X", arc.Center.X.ToString("F4")));
                Properties.Add(new PropertyItem("Center Y", arc.Center.Y.ToString("F4")));
                Properties.Add(new PropertyItem("Radius", arc.Radius.ToString("F4")));
                Properties.Add(new PropertyItem("Start Angle", (arc.StartAngle * 180 / Math.PI).ToString("F2") + "deg"));
                Properties.Add(new PropertyItem("End Angle", (arc.EndAngle * 180 / Math.PI).ToString("F2") + "deg"));
                break;

            case Circle circle:
                Properties.Add(new PropertyItem("Center X", circle.Center.X.ToString("F4")));
                Properties.Add(new PropertyItem("Center Y", circle.Center.Y.ToString("F4")));
                Properties.Add(new PropertyItem("Radius", circle.Radius.ToString("F4")));
                Properties.Add(new PropertyItem("Diameter", (circle.Radius * 2).ToString("F4")));
                Properties.Add(new PropertyItem("Circumference", (2 * Math.PI * circle.Radius).ToString("F4")));
                Properties.Add(new PropertyItem("Area", (Math.PI * circle.Radius * circle.Radius).ToString("F4")));
                break;


            case TextEntity text:
                Properties.Add(new PropertyItem("Text", text.Value));
                Properties.Add(new PropertyItem("Height", text.Height.ToString("F4")));
                Properties.Add(new PropertyItem("Position X", text.InsertPoint.X.ToString("F4")));
                Properties.Add(new PropertyItem("Position Y", text.InsertPoint.Y.ToString("F4")));
                Properties.Add(new PropertyItem("Rotation", (text.Rotation * 180 / Math.PI).ToString("F2") + "deg"));
                break;

            case MText mtext:
                Properties.Add(new PropertyItem("Text", mtext.Value));
                Properties.Add(new PropertyItem("Height", mtext.Height.ToString("F4")));
                Properties.Add(new PropertyItem("Position X", mtext.InsertPoint.X.ToString("F4")));
                Properties.Add(new PropertyItem("Position Y", mtext.InsertPoint.Y.ToString("F4")));
                Properties.Add(new PropertyItem("Width", mtext.RectangleWidth.ToString("F4")));
                break;

            case LwPolyline polyline:
                Properties.Add(new PropertyItem("Vertices", polyline.Vertices.Count.ToString()));
                Properties.Add(new PropertyItem("Closed", polyline.IsClosed ? "Yes" : "No"));
                break;

            case Insert insert:
                Properties.Add(new PropertyItem("Block Name", insert.Block?.Name ?? ""));
                Properties.Add(new PropertyItem("Position X", insert.InsertPoint.X.ToString("F4")));
                Properties.Add(new PropertyItem("Position Y", insert.InsertPoint.Y.ToString("F4")));
                Properties.Add(new PropertyItem("Scale X", insert.XScale.ToString("F4")));
                Properties.Add(new PropertyItem("Scale Y", insert.YScale.ToString("F4")));
                Properties.Add(new PropertyItem("Rotation", (insert.Rotation * 180 / Math.PI).ToString("F2") + "deg"));
                break;

            case Ellipse ellipse:
                Properties.Add(new PropertyItem("Center X", ellipse.Center.X.ToString("F4")));
                Properties.Add(new PropertyItem("Center Y", ellipse.Center.Y.ToString("F4")));
                Properties.Add(new PropertyItem("Radius Ratio", ellipse.RadiusRatio.ToString("F4")));
                break;
        }
    }
}

public class PropertyItem
{
    public string Name { get; }
    public string Value { get; }

    public PropertyItem(string name, string value)
    {
        Name = name;
        Value = value;
    }
}
