using ACadSharp.Entities;
using mPrismaMapsWPF.Models;

namespace mPrismaMapsWPF.Services;

public interface ISelectionService
{
    IReadOnlyCollection<EntityModel> SelectedEntities { get; }
    event EventHandler<SelectionChangedEventArgs>? SelectionChanged;

    void Select(EntityModel entity, bool addToSelection = false);
    void SelectMultiple(IEnumerable<EntityModel> entities, bool addToSelection = false);
    void Deselect(EntityModel entity);
    void ClearSelection();
    void ToggleSelection(EntityModel entity);
}

public class SelectionChangedEventArgs : EventArgs
{
    public IReadOnlyCollection<EntityModel> SelectedEntities { get; }
    public IReadOnlyCollection<EntityModel> AddedEntities { get; }
    public IReadOnlyCollection<EntityModel> RemovedEntities { get; }

    public SelectionChangedEventArgs(
        IReadOnlyCollection<EntityModel> selectedEntities,
        IReadOnlyCollection<EntityModel> addedEntities,
        IReadOnlyCollection<EntityModel> removedEntities)
    {
        SelectedEntities = selectedEntities;
        AddedEntities = addedEntities;
        RemovedEntities = removedEntities;
    }
}
