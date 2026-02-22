using System.Windows;
using System.Windows.Controls;
using mPrismaMapsWPF.Models;

namespace mPrismaMapsWPF.Views;

public partial class MergeOptionsDialog : Window
{
    public MergeOptions Options { get; private set; } = new();

    public MergeOptionsDialog(string sourceFilePath, int entityCount)
    {
        InitializeComponent();
        SourceFileText.Text = $"Source: {sourceFilePath}  ({entityCount} entities)";
    }

    private void LayerStrategyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (StrategyDescText == null)
            return;

        StrategyDescText.Text = LayerStrategyCombo.SelectedIndex switch
        {
            0 => "Conflicts: existing layer properties are kept unchanged.",
            1 => "Conflicts: existing layer color is overwritten with the secondary layer's color.",
            2 => "Conflicts: secondary layer is added with a \"_merged\" suffix (no overwrite).",
            _ => string.Empty,
        };
    }

    private void MergeButton_Click(object sender, RoutedEventArgs e)
    {
        if (!double.TryParse(OffsetXTextBox.Text, out double ox))
        {
            MessageBox.Show("Please enter a valid number for X offset.", "Invalid Input",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            OffsetXTextBox.SelectAll();
            OffsetXTextBox.Focus();
            return;
        }

        if (!double.TryParse(OffsetYTextBox.Text, out double oy))
        {
            MessageBox.Show("Please enter a valid number for Y offset.", "Invalid Input",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            OffsetYTextBox.SelectAll();
            OffsetYTextBox.Focus();
            return;
        }

        var strategy = LayerStrategyCombo.SelectedIndex switch
        {
            1 => LayerConflictStrategy.KeepSecondary,
            2 => LayerConflictStrategy.RenameSecondary,
            _ => LayerConflictStrategy.KeepPrimary,
        };

        Options = new MergeOptions
        {
            LayerConflictStrategy = strategy,
            OffsetX = ox,
            OffsetY = oy,
        };

        DialogResult = true;
    }
}
