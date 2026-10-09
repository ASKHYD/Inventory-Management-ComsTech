using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using SOFT2027.Presentation.ViewModels;

namespace SOFT2027.UI.Views;

public partial class AdministrationView : UserControl
{
    public AdministrationView()
    {
        InitializeComponent();
        Loaded += async (_, _) => { if (DataContext is AdministrationViewModel vm) { vm.PropertyChanged += Vm_PropertyChanged; await vm.LoadUsersAsync(); } };
        Unloaded += (_, _) => { if (DataContext is AdministrationViewModel vm) vm.PropertyChanged -= Vm_PropertyChanged; };
    }
    private void NewPasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is AdministrationViewModel vm && sender is PasswordBox box) vm.Password = box.Password;
    }
    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AdministrationViewModel.Password) && DataContext is AdministrationViewModel vm && vm.Password.Length == 0) NewPasswordBox.Clear();
    }
    private async void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || e.Source != sender || DataContext is not AdministrationViewModel vm || sender is not TabControl tabs) return;
        if (tabs.SelectedIndex == 1) await vm.LoadAuditAsync();
    }
    private void UsersGrid_AutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        if (e.PropertyName == "AdminId") { e.Cancel = true; return; }
        e.Column.Header = Regex.Replace(e.PropertyName, "([a-z])([A-Z])", "$1 $2");
    }
    private void AuditGrid_AutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        if (e.PropertyName == "AuditId" || e.PropertyName == "AdminId") { e.Cancel = true; return; }
        e.Column.Header = Regex.Replace(e.PropertyName, "([a-z])([A-Z])", "$1 $2");
    }
}
