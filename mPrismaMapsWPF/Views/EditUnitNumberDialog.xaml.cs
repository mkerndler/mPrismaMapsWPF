using System.Windows;
using System.Windows.Input;

namespace mPrismaMapsWPF.Views;

public partial class EditUnitNumberDialog : Window
{
    public string UnitNumberValue { get; private set; }

    public EditUnitNumberDialog(string currentValue)
    {
        InitializeComponent();

        UnitNumberValue = currentValue;
        ValueTextBox.Text = currentValue;

        Loaded += (_, _) =>
        {
            ValueTextBox.Focus();
            ValueTextBox.SelectAll();
        };
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        UnitNumberValue = ValueTextBox.Text;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ValueTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            OkButton_Click(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CancelButton_Click(sender, e);
            e.Handled = true;
        }
    }
}
