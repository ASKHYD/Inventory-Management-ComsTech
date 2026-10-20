using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SOFT2027.Presentation.ViewModels;

namespace SOFT2027.UI.Views;

public partial class PurchaseEntryView : UserControl
{
    private bool _loadRequestedForActivation;

    public PurchaseEntryView()
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
        if (!IsVisible || _loadRequestedForActivation || DataContext is not PurchaseEntryViewModel viewModel) return;
        _loadRequestedForActivation = true;
        if (viewModel.LoadCommand.CanExecute(null)) await viewModel.LoadCommand.ExecuteAsync(null);
    }

    private void EnterNewPrice_Click(object sender, RoutedEventArgs e)
    {
        UnitPriceInput.Focus();
        UnitPriceInput.SelectAll();
    }

    private async void PreviewBill_Click(object sender, RoutedEventArgs e) => await ShowBillPreviewAsync();

    private async void SaveAndPrintBill_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PurchaseEntryViewModel viewModel) return;
        var previousNumber = viewModel.LastSavedDocumentNumber;
        if (viewModel.SaveCommand.CanExecute(null))
            await viewModel.SaveCommand.ExecuteAsync(null);
        if (!string.IsNullOrWhiteSpace(viewModel.LastSavedDocumentNumber)
            && viewModel.LastSavedDocumentNumber != previousNumber)
        {
            viewModel.DocumentLookupNumber = viewModel.LastSavedDocumentNumber;
            await ShowBillPreviewAsync();
        }
    }

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

    private void PurchaseEntryView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not PurchaseEntryViewModel viewModel) return;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        var isAlt = (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;

        // Supplier Selection: F1 or Ctrl+1 or Alt+S
        if (key == Key.F1 || (isCtrl && key == Key.D1) || (isAlt && key == Key.S))
        {
            FocusControl(SupplierSelector);
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

        // Save & Print Bill: F4 or Ctrl+S or Ctrl+Enter
        if (key == Key.F4 || (isCtrl && key == Key.S) || (isCtrl && key == Key.Enter))
        {
            SaveAndPrintBill_Click(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        // New Bill: F12 or Ctrl+N
        if (key == Key.F12 || (isCtrl && key == Key.N))
        {
            if (viewModel.NewBillCommand.CanExecute(null))
                viewModel.NewBillCommand.Execute(null);
            FocusControl(SupplierSelector);
            e.Handled = true;
            return;
        }

        // Tab / Enter Chaining
        if (key == Key.Enter)
        {
            var focused = Keyboard.FocusedElement as DependencyObject;
            if (focused != null)
            {
                if (IsDescendantOf(focused, SupplierSelector))
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
