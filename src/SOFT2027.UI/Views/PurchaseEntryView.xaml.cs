using System;
using System.Threading.Tasks;
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

    private async void PreviewBill_Click(object sender, RoutedEventArgs e) => await ShowBillPreviewAsync();

    private async void PreviewLatestBill_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is PurchaseEntryViewModel viewModel && !string.IsNullOrWhiteSpace(viewModel.LastSavedDocumentNumber))
            viewModel.DocumentLookupNumber = viewModel.LastSavedDocumentNumber;
        await ShowBillPreviewAsync();
    }

    private async Task ShowBillPreviewAsync()
    {
        if (DataContext is not PurchaseEntryViewModel viewModel) return;
        try
        {
            var document = await viewModel.LoadDocumentForPreviewAsync(viewModel.DocumentLookupNumber);
            var preview = new TransactionDocumentPreviewWindow(document) { Owner = Window.GetWindow(this) };
            preview.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show(Window.GetWindow(this), ex.Message, "Purchase bill preview", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
