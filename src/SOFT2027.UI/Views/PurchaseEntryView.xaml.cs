using System.Windows;
using System.Windows.Controls;
using SOFT2027.Presentation.ViewModels;

namespace SOFT2027.UI.Views;

public partial class PurchaseEntryView : UserControl
{
    public PurchaseEntryView()
    {
        InitializeComponent();
    }

    private void View_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is PurchaseEntryViewModel viewModel && viewModel.LoadCommand.CanExecute(null))
            viewModel.LoadCommand.Execute(null);
    }

    private void EnterNewPrice_Click(object sender, RoutedEventArgs e)
    {
        UnitPriceInput.Focus();
        UnitPriceInput.SelectAll();
    }
}
