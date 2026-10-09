using System.Windows;
using System.Windows.Controls;
using System.Text.RegularExpressions;
using SOFT2027.Presentation.ViewModels;

namespace SOFT2027.UI.Views;

public partial class LookupManagementView : UserControl
{
    public LookupManagementView()
    {
        InitializeComponent();
        Loaded += async (_, _) => { if (DataContext is LookupManagementViewModel vm) await vm.LoadAsync(); };
    }
    private async void DeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not LookupManagementViewModel vm || vm.SelectedItem == null) return;
        if (MessageBox.Show($"Delete '{vm.SelectedItem.Name}'? This cannot be undone.", "Confirm deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            await vm.DeleteSelectedAsync();
    }
    private void LookupGrid_AutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        if (e.PropertyName == "Id") { e.Cancel = true; return; }
        e.Column.Header = Regex.Replace(e.PropertyName, "([a-z])([A-Z])", "$1 $2");
    }
}
