using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using ACadSharp.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using mPrismaMapsWPF.Commands;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Services;

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

    [ObservableProperty]
    private bool _canUndo;

    [ObservableProperty]
    private bool _canRedo;

    [ObservableProperty]
    private string? _undoDescription;

    [ObservableProperty]
    private string? _redoDescription;

    public ObservableCollection<EntityModel> Entities { get; } = new();
    public CadDocumentModel Document => _documentService.CurrentDocument;

    public IUndoRedoService UndoRedoService => _undoRedoService;

    public event EventHandler? ZoomToFitRequested;
    public event EventHandler? RenderRequested;
    public event EventHandler? EntitiesChanged;
    public event EventHandler<SelectEntityTypesEventArgs>? SelectEntityTypesRequested;

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
        DeleteSelectedCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Refreshes the entity collection from the document. Called after undo/redo operations.
    /// </summary>
    public void RefreshEntities()
    {
        Entities.Clear();
        foreach (var entity in _documentService.CurrentDocument.ModelSpaceEntities)
        {
            Entities.Add(new EntityModel(entity));
        }
        EntityCount = Entities.Count;
        RenderRequested?.Invoke(this, EventArgs.Empty);
    }
}
