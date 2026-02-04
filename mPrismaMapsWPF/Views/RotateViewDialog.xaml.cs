using System.Windows;
using System.Windows.Controls;

namespace mPrismaMapsWPF.Views;

public partial class RotateViewDialog : Window
{
    public double Angle { get; private set; }

    public RotateViewDialog(double currentAngle = 0)
    {
        InitializeComponent();
        Angle = currentAngle;
        AngleTextBox.Text = currentAngle.ToString("F1");
        AngleTextBox.SelectAll();
        AngleTextBox.Focus();
    }

    private void PresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string tagValue)
        {
            if (double.TryParse(tagValue, out double angle))
            {
                AngleTextBox.Text = angle.ToString();
            }
        }
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (double.TryParse(AngleTextBox.Text, out double angle))
        {
            // Normalize angle to -180 to 180 range for display, but allow any value
            Angle = angle;
            DialogResult = true;
        }
        else
        {
            MessageBox.Show("Please enter a valid number.", "Invalid Input",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            AngleTextBox.SelectAll();
            AngleTextBox.Focus();
        }
    }
}
