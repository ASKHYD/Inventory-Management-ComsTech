using System.Windows.Controls;
using System.Text.RegularExpressions;
using SOFT2027.Presentation.ViewModels;

namespace SOFT2027.UI.Views;

public partial class LedgerView : UserControl
{
    public LedgerView()
    {
        InitializeComponent();
        Loaded += async (_, _) => { if (DataContext is LedgerViewModel vm) await vm.LoadAsync(); };
    }
    private void LedgerGrid_AutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        if (e.PropertyName is "LedgerId" or "CustomerId" or "SupplierId") { e.Cancel = true; return; }
        e.Column.Header = e.PropertyName switch
        {
            "TransactionDate" => "Date", "TransactionType" => "Type", "ReferenceNumber" => "Reference",
            "RunningBalance" => "Balance", _ => Regex.Replace(e.PropertyName, "([a-z])([A-Z])", "$1 $2")
        };
    }
}
