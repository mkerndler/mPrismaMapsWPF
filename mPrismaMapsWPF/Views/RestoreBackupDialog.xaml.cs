using System.Windows;
using System.Windows.Controls;
using mPrismaMapsWPF.Services;

namespace mPrismaMapsWPF.Views;

public partial class RestoreBackupDialog : Window
{
    public BackupInfo? SelectedBackup { get; private set; }
    public string Server { get; private set; } = "";
    public string Username { get; private set; } = "";
    public string Password { get; private set; } = "";

    public RestoreBackupDialog(List<BackupInfo> backups)
    {
        InitializeComponent();

        BackupsListBox.ItemsSource = backups;
        ServerComboBox.SelectedIndex = 0;

        if (backups.Count > 0)
            BackupsListBox.SelectedIndex = 0;
    }

    private void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (BackupsListBox.SelectedItem is not BackupInfo backup)
        {
            MessageBox.Show("Please select a backup to restore.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
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

        SelectedBackup = backup;
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
