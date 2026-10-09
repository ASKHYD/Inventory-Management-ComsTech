using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SOFT2027.Presentation.ViewModels;

namespace SOFT2027.UI.Views;

public partial class BackupView : UserControl
{
    public BackupView() => InitializeComponent();

    private async void CreateBackup_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not BackupViewModel viewModel) return;
        var dialog = new SaveFileDialog
        {
            Title = "Save a SOFT2027 backup",
            Filter = "SOFT2027 database backup (*.db)|*.db",
            DefaultExt = ".db",
            AddExtension = true,
            FileName = $"SOFT2027-{DateTime.Now:yyyyMMdd-HHmm}.db",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true)
            await viewModel.CreateBackupAsync(dialog.FileName);
    }

    private async void RestoreBackup_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not BackupViewModel viewModel) return;
        var dialog = new OpenFileDialog
        {
            Title = "Choose a SOFT2027 backup to restore",
            Filter = "SOFT2027 database backup (*.db)|*.db|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;

        var answer = MessageBox.Show(Window.GetWindow(this),
            "Restore replaces current business records with the selected backup. A safety copy of the current database will be made first. Continue?",
            "Restore SOFT2027 backup", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (answer == MessageBoxResult.Yes && File.Exists(dialog.FileName))
            await viewModel.RestoreBackupAsync(dialog.FileName);
    }
}
