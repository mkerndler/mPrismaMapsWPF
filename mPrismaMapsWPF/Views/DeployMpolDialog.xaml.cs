using System.Windows;
using System.Windows.Controls;

namespace mPrismaMapsWPF.Views;

public partial class DeployMpolDialog : Window
{
    public string StoreName { get; private set; } = "";
    public string StoreId { get; private set; } = "";
    public string Floor { get; private set; } = "";
    public string Server { get; private set; } = "";
    public string Username { get; private set; } = "";
    public string Password { get; private set; } = "";

    public DeployMpolDialog(string defaultStoreName)
    {
        InitializeComponent();

        StoreNameTextBox.Text = defaultStoreName;
        ServerComboBox.SelectedIndex = 0;

        Loaded += (_, _) => StoreIdTextBox.Focus();
    }

    private void DeployButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(StoreNameTextBox.Text))
        {
            MessageBox.Show("Store Name is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(StoreIdTextBox.Text))
        {
            MessageBox.Show("Store ID is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(FloorTextBox.Text))
        {
            MessageBox.Show("Floor is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (ServerComboBox.SelectedItem == null)
        {
            MessageBox.Show("Database Server is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
        {
            MessageBox.Show("Username is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(PasswordBox.Password))
        {
            MessageBox.Show("Password is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        StoreName = StoreNameTextBox.Text.Trim();
        StoreId = StoreIdTextBox.Text.Trim();
        Floor = FloorTextBox.Text.Trim();
        Server = ((ComboBoxItem)ServerComboBox.SelectedItem).Content.ToString()!;
        Username = UsernameTextBox.Text.Trim();
        Password = PasswordBox.Password;

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
