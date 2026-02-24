using System.Windows;
using System.Windows.Input;

namespace mPrismaMapsWPF.Views;

public partial class ResizeUnitNumbersDialog : Window
{
    public double NewHeight { get; private set; }

    public ResizeUnitNumbersDialog(double currentHeight)
    {
        InitializeComponent();
        HeightTextBox.Text = currentHeight.ToString("G");
        Loaded += (_, _) => { HeightTextBox.Focus(); HeightTextBox.SelectAll(); };
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (double.TryParse(HeightTextBox.Text, out double v) && v > 0)
        {
            NewHeight = v;
            DialogResult = true;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void HeightTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) OkButton_Click(sender, e);
        else if (e.Key == Key.Escape) { DialogResult = false; Close(); }
    }
}
