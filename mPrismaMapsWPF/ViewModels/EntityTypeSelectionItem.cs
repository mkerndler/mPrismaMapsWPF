using CommunityToolkit.Mvvm.ComponentModel;

namespace mPrismaMapsWPF.ViewModels;

public partial class EntityTypeSelectionItem : ObservableObject
{
    public Type EntityType { get; }
    public string TypeName => EntityType.Name;
    public int Count { get; }

    [ObservableProperty]
    private bool _isSelected = true;

    public EntityTypeSelectionItem(Type entityType, int count)
    {
        EntityType = entityType;
        Count = count;
    }
}
