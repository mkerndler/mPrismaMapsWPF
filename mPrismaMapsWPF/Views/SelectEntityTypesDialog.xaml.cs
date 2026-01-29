using System.Collections.ObjectModel;
using System.Windows;
using mPrismaMapsWPF.ViewModels;

namespace mPrismaMapsWPF.Views;

public partial class SelectEntityTypesDialog : Window
{
    public ObservableCollection<EntityTypeSelectionItem> EntityTypes { get; }

    public ISet<Type> ExcludedTypes => EntityTypes
        .Where(e => !e.IsSelected)
        .Select(e => e.EntityType)
        .ToHashSet();

    public int TotalEntityCount { get; }

    public SelectEntityTypesDialog(IReadOnlyList<(Type EntityType, int Count)> scanResults)
    {
        InitializeComponent();

        EntityTypes = new ObservableCollection<EntityTypeSelectionItem>(
            scanResults.Select(r => new EntityTypeSelectionItem(r.EntityType, r.Count)));

        TotalEntityCount = scanResults.Sum(r => r.Count);

        EntityTypesListBox.ItemsSource = EntityTypes;

        foreach (var item in EntityTypes)
        {
            item.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(EntityTypeSelectionItem.IsSelected))
                {
                    UpdateSummary();
                }
            };
        }

        UpdateSummary();
    }

    private void UpdateSummary()
    {
        var selectedTypes = EntityTypes.Count(e => e.IsSelected);
        var totalTypes = EntityTypes.Count;
        var selectedEntityCount = EntityTypes.Where(e => e.IsSelected).Sum(e => e.Count);

        SummaryText.Text = $"{selectedTypes} of {totalTypes} types selected ({selectedEntityCount:N0} entities)";

        ImportButton.IsEnabled = selectedTypes > 0;
    }

    private void SelectAllButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in EntityTypes)
        {
            item.IsSelected = true;
        }
    }

    private void SelectNoneButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in EntityTypes)
        {
            item.IsSelected = false;
        }
    }

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
