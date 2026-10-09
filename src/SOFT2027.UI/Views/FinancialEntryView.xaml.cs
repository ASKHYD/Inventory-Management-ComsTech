using System.Windows;
using System.Windows.Controls;
using SOFT2027.Presentation.ViewModels;

namespace SOFT2027.UI.Views;

public partial class FinancialEntryView : UserControl
{
    public FinancialEntryView() => InitializeComponent();

    private void View_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is FinancialEntryViewModel viewModel && viewModel.LoadCommand.CanExecute(null))
            viewModel.LoadCommand.Execute(null);
    }
}
