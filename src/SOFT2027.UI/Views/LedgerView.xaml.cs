using System.Windows.Controls;
using System.Windows;
using System.Text.RegularExpressions;
using SOFT2027.Presentation.ViewModels;

namespace SOFT2027.UI.Views;

public partial class LedgerView : UserControl
{
    private bool _loadRequestedForActivation;
    public LedgerView()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadForActivationAsync();
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

    private async System.Threading.Tasks.Task LoadForActivationAsync()
    {
        if (!IsVisible || _loadRequestedForActivation || DataContext is not LedgerViewModel vm) return;
        _loadRequestedForActivation = true;
        await vm.LoadAsync();
    }
    private void LedgerGrid_AutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        if (e.PropertyName is "LedgerId" or "CustomerId" or "SupplierId") { e.Cancel = true; return; }
        e.Column.Header = e.PropertyName switch
        {
            "TransactionDate" => "Date", "TransactionType" => "Type", "ReferenceNumber" => "Reference",
            "RunningBalance" => "Balance (PKR)", _ => Regex.Replace(e.PropertyName, "([a-z])([A-Z])", "$1 $2")
        };
        if (e.PropertyName is "Debit" or "Credit" or "RunningBalance" && e.Column is DataGridTextColumn textCol)
        {
            textCol.Binding = new System.Windows.Data.Binding(e.PropertyName) { StringFormat = "PKR {0:N2}" };
        }
    }
}
