using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace mPrismaMapsWPF.Models;

/// <summary>
/// Represents a group of entities (by type or layer) in the Entity Viewer.
/// </summary>
public partial class EntityGroupModel : ObservableObject
{
    public EntityGroupModel(string name)
    {
        Name = name;
        Entities = new ObservableCollection<EntityModel>();
    }

    /// <summary>
    /// The name of the group (type name or layer name).
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The entities in this group.
    /// </summary>
    public ObservableCollection<EntityModel> Entities { get; }

    /// <summary>
    /// The number of entities in this group.
    /// </summary>
    public int Count => Entities.Count;

    /// <summary>
    /// Whether this group is expanded in the tree view.
    /// </summary>
    [ObservableProperty]
    private bool _isExpanded = true;
}
