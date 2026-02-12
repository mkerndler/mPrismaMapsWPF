using System.Windows;
using System.Windows.Input;

namespace mPrismaMapsWPF.Views;

public partial class ExportMpolDialog : Window
{
    public string StoreName { get; private set; }

    public ExportMpolDialog(string defaultStoreName)
    {
        InitializeComponent();

        StoreName = defaultStoreName;
        StoreNameTextBox.Text = defaultStoreName;

        Loaded += (_, _) =>
        {
            StoreNameTextBox.Focus();
            StoreNameTextBox.SelectAll();
        };
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        StoreName = StoreNameTextBox.Text;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void StoreNameTextBox_KeyDown(object sender, KeyEventArgs e)
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
