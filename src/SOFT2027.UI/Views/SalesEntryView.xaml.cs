using System;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;
using SOFT2027.Presentation.ViewModels;

namespace SOFT2027.UI.Views
{
    /// <summary>
    /// Interaction logic for SalesEntryView.xaml
    /// </summary>
    public partial class SalesEntryView : UserControl
    {
        public SalesEntryView()
        {
            InitializeComponent();
        }

        private void View_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is SalesEntryViewModel viewModel && viewModel.LoadInitialDataCommand.CanExecute(null))
                viewModel.LoadInitialDataCommand.Execute(null);
        }

        private async void PreviewInvoice_Click(object sender, RoutedEventArgs e) => await ShowInvoicePreviewAsync();

        private async void PreviewLatestInvoice_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SalesEntryViewModel viewModel && !string.IsNullOrWhiteSpace(viewModel.LastSavedDocumentNumber))
                viewModel.DocumentLookupNumber = viewModel.LastSavedDocumentNumber;
            await ShowInvoicePreviewAsync();
        }

        private async void PreviewRecentInvoiceSheet_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not SalesEntryViewModel viewModel) return;
            try
            {
                var documents = await viewModel.LoadRecentInvoiceSheetAsync();
                new TransactionDocumentPreviewWindow(documents) { Owner = Window.GetWindow(this) }.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(Window.GetWindow(this), ex.Message, "Cash memo sheet", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async Task ShowInvoicePreviewAsync()
        {
            if (DataContext is not SalesEntryViewModel viewModel) return;
            try
            {
                var document = await viewModel.LoadDocumentForPreviewAsync(viewModel.DocumentLookupNumber);
                var preview = new TransactionDocumentPreviewWindow(document) { Owner = Window.GetWindow(this) };
                preview.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(Window.GetWindow(this), ex.Message, "Invoice preview", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void EnterNewPrice_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            UnitPriceInput.Focus();
            UnitPriceInput.SelectAll();
        }

    }
}
