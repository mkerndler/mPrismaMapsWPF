using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Services;

namespace mPrismaMapsWPF.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IDocumentService _documentService;
    private readonly ISelectionService _selectionService;

    public MainWindowViewModel(IDocumentService documentService, ISelectionService selectionService)
    {
        _documentService = documentService;
        _selectionService = selectionService;

        _documentService.DocumentLoaded += OnDocumentLoaded;
        _documentService.DocumentClosed += OnDocumentClosed;
        _selectionService.SelectionChanged += OnSelectionChanged;

        LayerPanel = new LayerPanelViewModel(_documentService);
        PropertiesPanel = new PropertiesPanelViewModel(_selectionService, _documentService);
    }

    public LayerPanelViewModel LayerPanel { get; }
    public PropertiesPanelViewModel PropertiesPanel { get; }

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

    public ObservableCollection<EntityModel> Entities { get; } = new();
    public CadDocumentModel Document => _documentService.CurrentDocument;

    public event EventHandler? ZoomToFitRequested;
    public event EventHandler? RenderRequested;

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
            await LoadFileAsync(dialog.FileName);
        }
    }

    private async Task LoadFileAsync(string filePath)
    {
        IsLoading = true;
        StatusText = "Loading...";
        LoadProgress = 0;

        var progress = new Progress<int>(p => LoadProgress = p);

        bool success = await _documentService.OpenAsync(filePath, progress);

        IsLoading = false;

        if (!success)
        {
            StatusText = "Failed to load file";
            MessageBox.Show($"Failed to open file: {filePath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
        IsPanMode = true;
        IsSelectMode = false;
    }

    [RelayCommand]
    private void SetSelectMode()
    {
        IsPanMode = false;
        IsSelectMode = true;
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

        Entities.Clear();
        foreach (var entity in _documentService.CurrentDocument.ModelSpaceEntities)
        {
            Entities.Add(new EntityModel(entity));
        }

        SaveFileCommand.NotifyCanExecuteChanged();
        SaveFileAsCommand.NotifyCanExecuteChanged();

        RenderRequested?.Invoke(this, EventArgs.Empty);
        ZoomToFitRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnDocumentClosed(object? sender, EventArgs e)
    {
        WindowTitle = "mPrismaMaps - DWG Viewer";
        StatusText = "Ready";
        EntityCount = 0;
        Entities.Clear();

        SaveFileCommand.NotifyCanExecuteChanged();
        SaveFileAsCommand.NotifyCanExecuteChanged();

        RenderRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        SelectedCount = e.SelectedEntities.Count;
    }
}
