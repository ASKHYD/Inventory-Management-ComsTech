using System.Windows;
using System.Windows.Controls;
using SOFT2027.Presentation.ViewModels;

namespace SOFT2027.UI.Views;

public partial class CashBankView : UserControl
{
    public CashBankView() => InitializeComponent();

    private void View_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is CashBankViewModel viewModel && viewModel.LoadCommand.CanExecute(null))
            viewModel.LoadCommand.Execute(null);
    }
}
