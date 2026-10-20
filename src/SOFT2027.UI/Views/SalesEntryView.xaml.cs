using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SOFT2027.Presentation.ViewModels;

namespace SOFT2027.UI.Views
{
    /// <summary>
    /// Interaction logic for SalesEntryView.xaml
    /// </summary>
    public partial class SalesEntryView : UserControl
    {
        private bool _loadRequestedForActivation;

        public SalesEntryView()
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

        private async Task LoadForActivationAsync()
        {
            if (!IsVisible || _loadRequestedForActivation || DataContext is not SalesEntryViewModel viewModel) return;
            _loadRequestedForActivation = true;
            if (viewModel.LoadInitialDataCommand.CanExecute(null)) await viewModel.LoadInitialDataCommand.ExecuteAsync(null);
        }

        private async void PreviewInvoice_Click(object sender, RoutedEventArgs e) => await ShowInvoicePreviewAsync();

        private async void SaveAndPrintInvoice_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not SalesEntryViewModel viewModel) return;
            var previousNumber = viewModel.LastSavedDocumentNumber;
            await viewModel.SaveSaleAsync();
            if (!string.IsNullOrWhiteSpace(viewModel.LastSavedDocumentNumber)
                && viewModel.LastSavedDocumentNumber != previousNumber)
            {
                viewModel.DocumentLookupNumber = viewModel.LastSavedDocumentNumber;
                await ShowInvoicePreviewAsync();
            }
        }

        private async void PrintThermalSlip_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not SalesEntryViewModel viewModel) return;
            var previousNumber = viewModel.LastSavedDocumentNumber;
            await viewModel.SaveSaleAsync();
            if (!string.IsNullOrWhiteSpace(viewModel.LastSavedDocumentNumber)
                && viewModel.LastSavedDocumentNumber != previousNumber)
            {
                viewModel.DocumentLookupNumber = viewModel.LastSavedDocumentNumber;
                await ShowThermalPreviewAsync();
            }
        }

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

        private async Task ShowThermalPreviewAsync()
        {
            if (DataContext is not SalesEntryViewModel viewModel) return;
            try
            {
                var document = await viewModel.LoadDocumentForPreviewAsync(viewModel.DocumentLookupNumber);
                var preview = new TransactionDocumentPreviewWindow(document, isThermal: true) { Owner = Window.GetWindow(this) };
                preview.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(Window.GetWindow(this), ex.Message, "Thermal receipt", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void EnterNewPrice_Click(object sender, RoutedEventArgs e)
        {
            UnitPriceInput.Focus();
            UnitPriceInput.SelectAll();
        }

        private void SalesEntryView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not SalesEntryViewModel viewModel) return;

            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            var isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            var isAlt = (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;
            var isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

            // Esc: Close open modals
            if (key == Key.Escape)
            {
                if (viewModel.IsOverSellWarningOpen)
                {
                    viewModel.CancelOverSellLineCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
                if (viewModel.IsCreditLimitWarningOpen)
                {
                    viewModel.CancelCreditBillCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
                if (viewModel.IsHotkeysModalOpen)
                {
                    viewModel.CloseHotkeysModalCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
            }

            // Customer Selection: F1 or Ctrl+1 or Alt+C
            if (key == Key.F1 || (isCtrl && key == Key.D1) || (isAlt && key == Key.C))
            {
                FocusControl(CustomerSelector);
                e.Handled = true;
                return;
            }

            // Product Selection: F2 or Ctrl+2 or Alt+P
            if (key == Key.F2 || (isCtrl && key == Key.D2) || (isAlt && key == Key.P))
            {
                FocusControl(ProductSelector);
                e.Handled = true;
                return;
            }

            // Add Item: F3 or Ctrl+3 or Alt+A
            if (key == Key.F3 || (isCtrl && key == Key.D3) || (isAlt && key == Key.A))
            {
                if (viewModel.AddItemCommand.CanExecute(null))
                    viewModel.AddItemCommand.Execute(null);
                FocusControl(ProductSelector);
                e.Handled = true;
                return;
            }

            // Save & Print Invoice: F4 or Ctrl+S or Ctrl+Enter
            if (key == Key.F4 || (isCtrl && !isShift && key == Key.S) || (isCtrl && key == Key.Enter))
            {
                SaveAndPrintInvoice_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            // Save Credit Bill: F5 or Ctrl+Shift+S or Alt+B
            if (key == Key.F5 || (isCtrl && isShift && key == Key.S) || (isAlt && key == Key.B))
            {
                if (viewModel.SaveCreditBillCommand.CanExecute(null))
                    viewModel.SaveCreditBillCommand.Execute(null);
                e.Handled = true;
                return;
            }

            // New Bill: F12 or Ctrl+N
            if (key == Key.F12 || (isCtrl && key == Key.N))
            {
                if (viewModel.NewBillCommand.CanExecute(null))
                    viewModel.NewBillCommand.Execute(null);
                FocusControl(CustomerSelector);
                e.Handled = true;
                return;
            }

            // Hotkeys Cheat Sheet: Alt+H or Ctrl+H
            if ((isAlt && key == Key.H) || (isCtrl && key == Key.H))
            {
                if (viewModel.OpenHotkeysModalCommand.CanExecute(null))
                    viewModel.OpenHotkeysModalCommand.Execute(null);
                e.Handled = true;
                return;
            }

            // Tab / Enter Chaining
            if (key == Key.Enter)
            {
                var focused = Keyboard.FocusedElement as DependencyObject;
                if (focused != null)
                {
                    if (IsDescendantOf(focused, CustomerSelector))
                    {
                        FocusControl(ProductSelector);
                        e.Handled = true;
                        return;
                    }
                    if (IsDescendantOf(focused, ProductSelector))
                    {
                        FocusControl(QuantityInput);
                        e.Handled = true;
                        return;
                    }
                    if (IsDescendantOf(focused, QuantityInput))
                    {
                        FocusControl(UnitPriceInput);
                        e.Handled = true;
                        return;
                    }
                    if (IsDescendantOf(focused, UnitPriceInput))
                    {
                        FocusControl(DiscountInput);
                        e.Handled = true;
                        return;
                    }
                    if (IsDescendantOf(focused, DiscountInput))
                    {
                        if (viewModel.AddItemCommand.CanExecute(null))
                            viewModel.AddItemCommand.Execute(null);
                        FocusControl(ProductSelector);
                        e.Handled = true;
                        return;
                    }
                }
            }
        }

        private static bool IsDescendantOf(DependencyObject child, DependencyObject parent)
        {
            if (child == parent) return true;
            DependencyObject? current = child;
            while (current != null)
            {
                if (current == parent) return true;
                current = VisualTreeHelper.GetParent(current);
            }
            return false;
        }

        private static void FocusControl(FrameworkElement element)
        {
            element.Focus();
            if (element is ComboBox cb)
            {
                cb.IsDropDownOpen = false;
                var tb = cb.Template?.FindName("PART_EditableTextBox", cb) as TextBox;
                if (tb != null)
                {
                    tb.Focus();
                    tb.SelectAll();
                }
            }
            else if (element is TextBox tb)
            {
                tb.Focus();
                tb.SelectAll();
            }
        }
    }
}
