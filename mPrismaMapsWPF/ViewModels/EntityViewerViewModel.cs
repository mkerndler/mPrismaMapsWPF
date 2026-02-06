using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Services;

namespace mPrismaMapsWPF.ViewModels;

/// <summary>
/// Grouping mode for the entity viewer.
/// </summary>
public enum EntityGroupingMode
{
    None,
    ByType,
    ByLayer
}

/// <summary>
/// ViewModel for the Entity Viewer panel, managing filtering and grouping.
/// </summary>
public partial class EntityViewerViewModel : ObservableObject
{
    private readonly ISelectionService _selectionService;
    private readonly DispatcherTimer _filterDebounceTimer;
    private ObservableCollection<EntityModel> _allEntities;
    private string _filterText = string.Empty;
    private EntityGroupingMode _groupingMode = EntityGroupingMode.None;

    /// <summary>
    /// When true, suppresses automatic refresh on CollectionChanged events.
    /// Set this during bulk entity loading to avoid O(N^2) notification storms.
    /// </summary>
    public bool SuppressRefresh { get; set; }

    public EntityViewerViewModel(ISelectionService selectionService)
    {
        _selectionService = selectionService;
        _allEntities = new ObservableCollection<EntityModel>();
        FilteredEntities = new ObservableCollection<EntityModel>();
        GroupedEntities = new ObservableCollection<EntityGroupModel>();

        _filterDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _filterDebounceTimer.Tick += (_, _) =>
        {
            _filterDebounceTimer.Stop();
            ApplyFilter();
        };

        _selectionService.SelectionChanged += OnSelectionChanged;
    }

    /// <summary>
    /// The filter text for searching entities.
    /// </summary>
    public string FilterText
    {
        get => _filterText;
        set
        {
            if (SetProperty(ref _filterText, value))
            {
                // Debounce filter to avoid rebuilding on every keystroke
                _filterDebounceTimer.Stop();
                _filterDebounceTimer.Start();
            }
        }
    }

    /// <summary>
    /// The current grouping mode.
    /// </summary>
    public EntityGroupingMode GroupingMode
    {
        get => _groupingMode;
        set
        {
            if (SetProperty(ref _groupingMode, value))
            {
                OnPropertyChanged(nameof(IsGroupByNone));
                OnPropertyChanged(nameof(IsGroupByType));
                OnPropertyChanged(nameof(IsGroupByLayer));
                ApplyFilter();
            }
        }
    }

    /// <summary>
    /// Gets or sets whether grouping is set to None.
    /// </summary>
    public bool IsGroupByNone
    {
        get => GroupingMode == EntityGroupingMode.None;
        set { if (value) GroupingMode = EntityGroupingMode.None; }
    }

    /// <summary>
    /// Gets or sets whether grouping is set to Type.
    /// </summary>
    public bool IsGroupByType
    {
        get => GroupingMode == EntityGroupingMode.ByType;
        set { if (value) GroupingMode = EntityGroupingMode.ByType; }
    }

    /// <summary>
    /// Gets or sets whether grouping is set to Layer.
    /// </summary>
    public bool IsGroupByLayer
    {
        get => GroupingMode == EntityGroupingMode.ByLayer;
        set { if (value) GroupingMode = EntityGroupingMode.ByLayer; }
    }

    /// <summary>
    /// The filtered list of entities (used when not grouped).
    /// </summary>
    public ObservableCollection<EntityModel> FilteredEntities { get; }

    /// <summary>
    /// The grouped entities (used when grouped by type or layer).
    /// </summary>
    public ObservableCollection<EntityGroupModel> GroupedEntities { get; }

    /// <summary>
    /// Total count of entities (before filtering).
    /// </summary>
    [ObservableProperty]
    private int _totalCount;

    /// <summary>
    /// Count of selected entities.
    /// </summary>
    [ObservableProperty]
    private int _selectedCount;

    /// <summary>
    /// Sets the source entities collection.
    /// </summary>
    public void SetEntities(ObservableCollection<EntityModel> entities)
    {
        // Unsubscribe from old collection
        if (_allEntities != null)
        {
            _allEntities.CollectionChanged -= OnEntitiesCollectionChanged;
        }

        _allEntities = entities;
        TotalCount = _allEntities.Count;

        // Subscribe to new collection
        if (_allEntities != null)
        {
            _allEntities.CollectionChanged += OnEntitiesCollectionChanged;
        }

        ApplyFilter();
    }

    /// <summary>
    /// Refreshes the filtered/grouped view.
    /// </summary>
    public void Refresh()
    {
        TotalCount = _allEntities?.Count ?? 0;
        ApplyFilter();
    }

    private void OnEntitiesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (SuppressRefresh)
            return;

        TotalCount = _allEntities?.Count ?? 0;
        ApplyFilter();
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        SelectedCount = e.SelectedEntities.Count;
    }

    private void ApplyFilter()
    {
        if (_allEntities == null)
            return;

        // Filter entities
        var filtered = string.IsNullOrWhiteSpace(_filterText)
            ? _allEntities.ToList()
            : _allEntities.Where(e => MatchesFilter(e)).ToList();

        if (GroupingMode == EntityGroupingMode.None)
        {
            // Flat list mode
            FilteredEntities.Clear();
            foreach (var entity in filtered)
            {
                FilteredEntities.Add(entity);
            }
            GroupedEntities.Clear();
        }
        else
        {
            // Grouped mode
            FilteredEntities.Clear();
            GroupedEntities.Clear();

            var groups = GroupingMode == EntityGroupingMode.ByType
                ? filtered.GroupBy(e => e.TypeName).OrderBy(g => g.Key)
                : filtered.GroupBy(e => e.LayerName).OrderBy(g => g.Key);

            foreach (var group in groups)
            {
                var groupModel = new EntityGroupModel(group.Key);
                foreach (var entity in group)
                {
                    groupModel.Entities.Add(entity);
                }
                GroupedEntities.Add(groupModel);
            }
        }
    }

    private bool MatchesFilter(EntityModel entity)
    {
        var filter = _filterText.Trim();
        if (string.IsNullOrEmpty(filter))
            return true;

        // Case-insensitive search on display name, type name, and layer name
        return entity.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || entity.TypeName.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || entity.LayerName.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }
}
