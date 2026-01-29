using System.Windows;
using ACadSharp.Tables;
using mPrismaMapsWPF.Commands;
using mPrismaMapsWPF.ViewModels;

namespace mPrismaMapsWPF.Views;

/// <summary>
/// Dialog for deleting multiple layers with entity handling options.
/// </summary>
public partial class DeleteMultipleLayersDialog : Window
{
    /// <summary>
    /// The layers being deleted.
    /// </summary>
    public IReadOnlyList<LayerDeleteInfo> LayersToDelete { get; }

    /// <summary>
    /// Available layers for reassignment (excluding layers being deleted and layer "0").
    /// </summary>
    public IReadOnlyList<Layer> AvailableLayers { get; }

    /// <summary>
    /// The selected delete option.
    /// </summary>
    public LayerDeleteOption DeleteOption { get; private set; }

    /// <summary>
    /// The target layer for reassignment (if applicable).
    /// </summary>
    public Layer? TargetLayer { get; private set; }

    /// <summary>
    /// The total number of entities across all layers.
    /// </summary>
    public int TotalEntityCount { get; }

    public DeleteMultipleLayersDialog(
        IReadOnlyList<LayerDeleteInfo> layersToDelete,
        IEnumerable<Layer> availableLayers)
    {
        InitializeComponent();

        LayersToDelete = layersToDelete;
        TotalEntityCount = layersToDelete.Sum(l => l.EntityCount);

        // Filter out layers being deleted and layer "0"
        var layerNamesToDelete = layersToDelete.Select(l => l.Layer.Name).ToHashSet();
        AvailableLayers = availableLayers
            .Where(l => !layerNamesToDelete.Contains(l.Name) && l.Name != "0")
            .ToList();

        // Set up the message
        MessageText.Text = $"You are about to delete {layersToDelete.Count} layers " +
                          $"containing a total of {TotalEntityCount:N0} entities.";

        // Populate layers list
        LayersListBox.ItemsSource = layersToDelete;

        // Populate target layer combo
        TargetLayerCombo.ItemsSource = AvailableLayers.Select(l => l.Name).ToList();
        if (AvailableLayers.Count > 0)
        {
            TargetLayerCombo.SelectedIndex = 0;
        }
        else
        {
            // No available layers for reassignment
            ReassignEntitiesRadio.IsEnabled = false;
        }

        // If no entities, simplify the message
        if (TotalEntityCount == 0)
        {
            MessageText.Text = $"You are about to delete {layersToDelete.Count} empty layers.";
            DeleteEntitiesRadio.Content = "Delete these empty layers";
            ReassignEntitiesRadio.Visibility = Visibility.Collapsed;
            TargetLayerCombo.Visibility = Visibility.Collapsed;
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (DeleteEntitiesRadio.IsChecked == true)
        {
            DeleteOption = LayerDeleteOption.DeleteEntities;
            TargetLayer = null;
        }
        else
        {
            DeleteOption = LayerDeleteOption.ReassignEntities;
            var selectedLayerName = TargetLayerCombo.SelectedItem as string;
            TargetLayer = AvailableLayers.FirstOrDefault(l => l.Name == selectedLayerName);

            if (TargetLayer == null && TotalEntityCount > 0)
            {
                MessageBox.Show("Please select a target layer for reassignment.",
                    "No Target Layer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
        }

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
