using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using SOFT2027.Presentation.ViewModels;

namespace SOFT2027.UI.Views;

public partial class AdministrationView : UserControl
{
    private bool _activationRequested;
    public AdministrationView()
    {
        InitializeComponent();
        Loaded += async (_, _) => await ActivateViewAsync();
        IsVisibleChanged += async (_, _) =>
        {
            if (IsVisible) await ActivateViewAsync();
            else _activationRequested = false;
        };
        Unloaded += (_, _) => { if (DataContext is AdministrationViewModel vm) vm.PropertyChanged -= Vm_PropertyChanged; };
        DataContextChanged += async (_, e) =>
        {
            if (e.OldValue is AdministrationViewModel oldVm) oldVm.PropertyChanged -= Vm_PropertyChanged;
            if (e.NewValue is AdministrationViewModel newVm) newVm.PropertyChanged += Vm_PropertyChanged;
            _activationRequested = false;
            if (IsVisible) await ActivateViewAsync();
        };
    }

    private async System.Threading.Tasks.Task ActivateViewAsync()
    {
        if (!IsVisible || _activationRequested || DataContext is not AdministrationViewModel vm) return;
        _activationRequested = true;
        try
        {
            await vm.LoadUsersCommand.ExecuteAsync(null);
            if (VisualAndLogicalDescendants(this).OfType<TabControl>().FirstOrDefault()?.SelectedIndex == 1)
                await LoadAuditForTabAsync(vm);
        }
        finally { _activationRequested = false; }
    }
    private bool _isSyncingPassword;

    private void NewPasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_isSyncingPassword) return;
        _isSyncingPassword = true;
        try
        {
            PasswordTextBox.Text = NewPasswordBox.Password;
            if (DataContext is AdministrationViewModel vm) vm.Password = NewPasswordBox.Password;
        }
        finally { _isSyncingPassword = false; }
    }

    private void PasswordTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isSyncingPassword) return;
        _isSyncingPassword = true;
        try
        {
            NewPasswordBox.Password = PasswordTextBox.Text;
            if (DataContext is AdministrationViewModel vm) vm.Password = PasswordTextBox.Text;
        }
        finally { _isSyncingPassword = false; }
    }

    private void PasswordVisibilityToggle_Click(object sender, RoutedEventArgs e)
    {
        var isVisible = PasswordVisibilityToggle.IsChecked == true;
        PasswordVisibilityToggle.Content = isVisible ? "Hide" : "Show";
        if (isVisible)
        {
            NewPasswordBox.Visibility = Visibility.Collapsed;
            PasswordTextBox.Visibility = Visibility.Visible;
            PasswordTextBox.Focus();
            PasswordTextBox.CaretIndex = PasswordTextBox.Text.Length;
        }
        else
        {
            PasswordTextBox.Visibility = Visibility.Collapsed;
            NewPasswordBox.Visibility = Visibility.Visible;
            NewPasswordBox.Focus();
        }
    }

    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AdministrationViewModel.Password) && DataContext is AdministrationViewModel vm && vm.Password.Length == 0)
        {
            _isSyncingPassword = true;
            NewPasswordBox.Clear();
            PasswordTextBox.Clear();
            _isSyncingPassword = false;
        }
    }
    private async void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || e.Source != sender || DataContext is not AdministrationViewModel vm || sender is not TabControl tabs) return;
        if (tabs.SelectedIndex == 1 && !_activationRequested) await LoadAuditForTabAsync(vm);
    }

    private async System.Threading.Tasks.Task LoadAuditForTabAsync(AdministrationViewModel vm)
    {
        ActivityLoadStateText.Text = "Loading activity…";
        ActivityLoadStateText.Visibility = Visibility.Visible;
        try { await vm.LoadAuditCommand.ExecuteAsync(null); }
        finally
        {
            var empty = vm.AuditEntries.Count == 0;
            ActivityLoadStateText.Text = empty ? "No activity has been recorded yet." : "";
            ActivityLoadStateText.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private static System.Collections.Generic.IEnumerable<DependencyObject> VisualAndLogicalDescendants(DependencyObject root)
    {
        var pending = new System.Collections.Generic.Stack<DependencyObject>();
        var seen = new System.Collections.Generic.HashSet<DependencyObject>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!seen.Add(current)) continue;
            yield return current;
            if (current is System.Windows.Media.Visual || current is System.Windows.Media.Media3D.Visual3D)
                for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(current); i++) pending.Push(System.Windows.Media.VisualTreeHelper.GetChild(current, i));
            foreach (var child in LogicalTreeHelper.GetChildren(current).OfType<DependencyObject>()) pending.Push(child);
        }
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
