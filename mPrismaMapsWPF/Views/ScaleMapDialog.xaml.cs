using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace mPrismaMapsWPF.Views;

public partial class ScaleMapDialog : Window
{
    public double ScaleFactor { get; private set; } = 1.0;

    public ScaleMapDialog()
    {
        InitializeComponent();
        FactorTextBox.SelectAll();
        FactorTextBox.Focus();
    }

    private void PresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string tagValue)
        {
            FactorTextBox.Text = tagValue;
        }
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (double.TryParse(FactorTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double factor) && factor > 0)
        {
            ScaleFactor = factor;
            DialogResult = true;
        }
        else
        {
            MessageBox.Show("Please enter a valid positive number.", "Invalid Input",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            FactorTextBox.SelectAll();
            FactorTextBox.Focus();
        }
    }
}
