using System.Windows;
using System.Windows.Controls;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections.Specialized;
using SOFT2027.Presentation.ViewModels;

namespace SOFT2027.UI.Views;

public partial class LookupManagementView : UserControl
{
    private bool _activationRequested;
    private LookupManagementViewModel? _observedViewModel;
    public LookupManagementView()
    {
        InitializeComponent();
        Loaded += async (_, _) => await ActivateViewAsync();
        IsVisibleChanged += async (_, _) =>
        {
            if (IsVisible) await ActivateViewAsync();
            else _activationRequested = false;
        };
        DataContextChanged += async (_, e) =>
        {
            _activationRequested = false;
            if (_observedViewModel != null)
            {
                _observedViewModel.Items.CollectionChanged -= Items_CollectionChanged;
                _observedViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }
            _observedViewModel = e.NewValue as LookupManagementViewModel;
            if (_observedViewModel != null)
            {
                _observedViewModel.Items.CollectionChanged += Items_CollectionChanged;
                _observedViewModel.PropertyChanged += ViewModel_PropertyChanged;
            }
            UpdateEmptyState();
            if (IsVisible) await ActivateViewAsync();
        };
        Unloaded += (_, _) =>
        {
            if (_observedViewModel != null)
            {
                _observedViewModel.Items.CollectionChanged -= Items_CollectionChanged;
                _observedViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }
        };
    }

    private async Task ActivateViewAsync()
    {
        if (!IsVisible || _activationRequested || DataContext is not LookupManagementViewModel vm) return;
        _activationRequested = true;
        LoadStateText.Text = $"Loading {vm.Kind.ToLowerInvariant()}…";
        LoadingStateBorder.Visibility = Visibility.Visible;
        try { if (vm.LoadCommand.CanExecute(null)) await vm.LoadCommand.ExecuteAsync(null); }
        finally { _activationRequested = false; UpdateEmptyState(); }
    }

    private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateEmptyState();

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LookupManagementViewModel.IsBusy) ||
            e.PropertyName == nameof(LookupManagementViewModel.Kind))
        {
            UpdateEmptyState();
        }
        else if (e.PropertyName == nameof(LookupManagementViewModel.IsAddFormOpen))
        {
            if (_observedViewModel?.IsAddFormOpen == true)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    NewItemNameBox.Focus();
                    NewItemNameBox.SelectAll();
                });
            }
        }
    }

    private void UpdateEmptyState()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(UpdateEmptyState);
            return;
        }

        if (_observedViewModel == null) return;
        var busy = _activationRequested || _observedViewModel.IsBusy;
        var empty = !busy && _observedViewModel.Items.Count == 0;

        LoadStateText.Text = $"Loading {_observedViewModel.Kind.ToLowerInvariant()}…";
        LoadingStateBorder.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;

        EmptyStateText.Text = $"No {_observedViewModel.Kind.ToLowerInvariant()} yet. Choose Add to create one.";
        EmptyStateBorder.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
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
