using System.Windows;
using ACadSharp.Tables;
using mPrismaMapsWPF.Commands;

namespace mPrismaMapsWPF.Views;

/// <summary>
/// Dialog for deleting a layer with entity handling options.
/// </summary>
public partial class DeleteLayerDialog : Window
{
    /// <summary>
    /// The layer being deleted.
    /// </summary>
    public Layer Layer { get; }

    /// <summary>
    /// Available layers for reassignment (excluding the layer being deleted and layer "0").
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
    /// The number of entities on the layer.
    /// </summary>
    public int EntityCount { get; }

    public DeleteLayerDialog(Layer layer, IEnumerable<Layer> availableLayers, int entityCount)
    {
        InitializeComponent();

        Layer = layer;
        EntityCount = entityCount;
        AvailableLayers = availableLayers
            .Where(l => l != layer && l.Name != "0")
            .ToList();

        // Set up the message
        if (entityCount > 0)
        {
            MessageText.Text = $"The layer '{layer.Name}' contains {entityCount:N0} entities. " +
                               "Choose how to handle these entities:";
        }
        else
        {
            MessageText.Text = $"The layer '{layer.Name}' contains no entities and will be deleted.";
            DeleteEntitiesRadio.Content = "Delete this empty layer";
            ReassignEntitiesRadio.Visibility = Visibility.Collapsed;
            TargetLayerCombo.Visibility = Visibility.Collapsed;
        }

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

            if (TargetLayer == null && EntityCount > 0)
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
