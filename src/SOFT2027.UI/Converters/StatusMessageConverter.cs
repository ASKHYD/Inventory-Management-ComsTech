using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SOFT2027.UI.Converters;

/// <summary>Presentation-only status decoration and semantic color selection.</summary>
public sealed class StatusMessageConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string rawMessage || string.IsNullOrWhiteSpace(rawMessage)) return null;
        var message = Services.ExceptionTranslator.Translate(rawMessage);
        var forcedError = string.Equals(parameter as string, "Error", StringComparison.OrdinalIgnoreCase);
        var category = forcedError ? "Error" : Category(message);
        if (string.Equals(parameter as string, "Color", StringComparison.OrdinalIgnoreCase))
        {
            var key = category switch { "Error" => "RedBrush", "Success" => "GreenBrush", _ => "MutedBrush" };
            return Application.Current.TryFindResource(key);
        }
        var icon = category switch { "Error" => "⚠ ", "Success" => "✓ ", _ => "ℹ " };
        return icon + message;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;

    private static string Category(string message)
    {
        if (message.StartsWith("Error", StringComparison.OrdinalIgnoreCase) || message.Contains("failed", StringComparison.OrdinalIgnoreCase) || message.Contains("failure", StringComparison.OrdinalIgnoreCase)) return "Error";
        if (new[] { "saved", "added", "created", "updated", "restored", "completed", "reset", "deactivated", "activated" }.Any(word => message.Contains(word, StringComparison.OrdinalIgnoreCase))) return "Success";
        return "Info";
    }
}

/// <summary>Marks the navigation button matching the currently displayed view model.</summary>
public sealed class NavigationActiveConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null || parameter is not string page) return false;
        var type = value.GetType().Name;
        var title = value.GetType().GetProperty("Title")?.GetValue(value)?.ToString() ?? string.Empty;
        var kind = value.GetType().GetProperty("Kind")?.GetValue(value)?.ToString() ?? string.Empty;
        return page switch
        {
            "Sales" => type == "SalesEntryViewModel",
            "Purchases" => type == "PurchaseEntryViewModel",
            "SalesReturns" => type == "ReturnEntryViewModel" && title == "Sales Returns",
            "PurchaseReturns" => type == "ReturnEntryViewModel" && title == "Purchase Returns",
            "Inventory" => type == "InventoryViewModel",
            "Products" or "Customers" or "Suppliers" => type == "MasterDataViewModel" && title == page,
            "Lookups" => type == "LookupManagementViewModel",
            "Receipts" => type == "FinancialEntryViewModel" && kind == "Receipt",
            "Payments" => type == "FinancialEntryViewModel" && kind == "Payment",
            "Expenses" => type == "FinancialEntryViewModel" && kind == "Expense",
            "CashBank" => type == "CashBankViewModel",
            "CustomerLedger" => type == "LedgerViewModel" && title == "Customer Ledger",
            "SupplierLedger" => type == "LedgerViewModel" && title == "Supplier Ledger",
            "Reports" => type == "ReportsDashboardViewModel",
            "Backup" => type == "BackupViewModel",
            "Administration" => type == "AdministrationViewModel",
            _ => false
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
