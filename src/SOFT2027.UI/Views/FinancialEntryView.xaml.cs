using System.Windows;
using System.Windows.Controls;
using SOFT2027.Presentation.ViewModels;

namespace SOFT2027.UI.Views;

public partial class FinancialEntryView : UserControl
{
    private bool _loadRequestedForActivation;
    public FinancialEntryView()
    {
        InitializeComponent();
        DataContextChanged += async (_, _) =>
        {
            _loadRequestedForActivation = false;
            if (IsVisible) await LoadForActivationAsync();
        };
        IsVisibleChanged += async (_, _) =>
        {
            if (IsVisible) await LoadForActivationAsync();
            else _loadRequestedForActivation = false;
        };
    }

    private async void View_Loaded(object sender, RoutedEventArgs e) => await LoadForActivationAsync();

    private async System.Threading.Tasks.Task LoadForActivationAsync()
    {
        if (!IsVisible || _loadRequestedForActivation || DataContext is not FinancialEntryViewModel viewModel) return;
        _loadRequestedForActivation = true;
        if (viewModel.LoadCommand.CanExecute(null)) await viewModel.LoadCommand.ExecuteAsync(null);
    }
}
