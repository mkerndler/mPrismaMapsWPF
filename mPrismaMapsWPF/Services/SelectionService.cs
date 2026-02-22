using mPrismaMapsWPF.Models;

namespace mPrismaMapsWPF.Services;

public class SelectionService : ISelectionService
{
    private readonly HashSet<EntityModel> _selectedEntities = new();

    public IReadOnlyCollection<EntityModel> SelectedEntities => _selectedEntities;

    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;

    public void Select(EntityModel entity, bool addToSelection = false)
    {
        if (entity.IsLocked)
            return;

        var added = new List<EntityModel>();
        var removed = new List<EntityModel>();

        if (!addToSelection)
        {
            foreach (var e in _selectedEntities.ToList())
            {
                e.IsSelected = false;
                removed.Add(e);
            }
            _selectedEntities.Clear();
        }

        if (_selectedEntities.Add(entity))
        {
            entity.IsSelected = true;
            added.Add(entity);
        }

        if (added.Count > 0 || removed.Count > 0)
        {
            RaiseSelectionChanged(added, removed);
        }
    }

    public void SelectMultiple(IEnumerable<EntityModel> entities, bool addToSelection = false)
    {
        var added = new List<EntityModel>();
        var removed = new List<EntityModel>();

        if (!addToSelection)
        {
            foreach (var e in _selectedEntities.ToList())
            {
                e.IsSelected = false;
                removed.Add(e);
            }
            _selectedEntities.Clear();
        }

        foreach (var entity in entities)
        {
            if (entity.IsLocked)
                continue;

            if (_selectedEntities.Add(entity))
            {
                entity.IsSelected = true;
                added.Add(entity);
            }
        }

        if (added.Count > 0 || removed.Count > 0)
        {
            RaiseSelectionChanged(added, removed);
        }
    }

    public void Deselect(EntityModel entity)
    {
        if (_selectedEntities.Remove(entity))
        {
            entity.IsSelected = false;
            RaiseSelectionChanged([], [entity]);
        }
    }

    public void ClearSelection()
    {
        if (_selectedEntities.Count == 0)
            return;

        var removed = _selectedEntities.ToList();
        foreach (var entity in removed)
        {
            entity.IsSelected = false;
        }
        _selectedEntities.Clear();

        RaiseSelectionChanged([], removed);
    }

    public void ToggleSelection(EntityModel entity)
    {
        if (entity.IsLocked)
            return;

        if (_selectedEntities.Contains(entity))
        {
            Deselect(entity);
        }
        else
        {
            Select(entity, addToSelection: true);
        }
    }

    private void RaiseSelectionChanged(IReadOnlyCollection<EntityModel> added, IReadOnlyCollection<EntityModel> removed)
    {
        SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(
            _selectedEntities.ToList(),
            added,
            removed));
    }
}
