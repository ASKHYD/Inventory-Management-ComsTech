using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace SOFT2027.UI.Views;

public partial class AppTourWindow : Window, INotifyPropertyChanged
{
    private static readonly (string Title, string Body)[] Steps =
    [
        ("Welcome to your offline workspace", "SOFT2027 keeps your business records on this computer. Your work is saved in the local database, so an internet connection is not required."),
        ("Use the menu on the left", "Choose Sales Entry to create an invoice, Products to maintain your product list, or Customers and Suppliers to maintain your business contacts. The selected page opens in the main area."),
        ("Create a sale in a few steps", "Choose a customer, select a product, enter quantity and price, then add the item. Check the total and amount received before saving the invoice."),
        ("Record purchases and remember supplier prices", "Choose Purchase Entry to record a supplier bill. The last rate for that supplier and product appears automatically when one is available. Use the buttons to choose the previous rate, product default, or enter a new rate."),
        ("Record customer and supplier returns", "Open Sales Returns or Purchase Returns, enter the original invoice number, load its items, and enter each returned quantity. SOFT2027 checks the remaining quantity and updates stock and the party balance together."),
        ("Keep product and contact details tidy", "Use the search box to find a record. Select Add to create a row, edit its details, and choose Save Changes. Inactive records stay in older transactions."),
        ("Organize products", "Maintain categories, brands, and units from the Master Data menu. Assign them to products from the product list. A list item assigned to a product must be unassigned before it can be deleted."),
        ("Keep stock accurate", "Current Stock shows each product's quantity and recent movements. Use a positive adjustment to add stock or a negative adjustment to remove it, and always enter a reason."),
        ("Record receipts, payments, and expenses", "Use the Accounts menu to record money from customers, payments to suppliers, or business expenses. The related customer or supplier balance and cash or bank activity update with the entry."),
        ("Review customer and supplier accounts", "Choose Customer Ledger or Supplier Ledger, select an account, and view its dated transactions, debits, credits, and running balance."),
        ("Check your reports", "Open Reports Dashboard, choose a date range, and load the totals and stock valuation. Reports are based on the records saved in this application."),
        ("Manage access and review activity", "Administration & Activity lets an administrator add accounts, activate or deactivate access, reset passwords, and review recent activity."),
        ("Protect your records", "Create a backup before moving to a different computer or doing major maintenance. Restoring a backup replaces current records; SOFT2027 creates a safety copy of the current database first."),
        ("Replay this tour whenever you need it", "Choose Help & Tour at the bottom of the left menu to review these steps again. If something looks wrong, read the message on the page and correct the highlighted or missing information before saving.")
    ];

    private int _stepIndex;
    public event PropertyChangedEventHandler? PropertyChanged;
    public string StepTitle => Steps[_stepIndex].Title;
    public string StepBody => Steps[_stepIndex].Body;
    public string ProgressText => $"STEP {_stepIndex + 1} OF {Steps.Length}";
    public string NextButtonText => _stepIndex == Steps.Length - 1 ? "Finish" : "Next";
    public bool CanGoBack => _stepIndex > 0;

    public AppTourWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_stepIndex == 0) return;
        _stepIndex--;
        NotifyStepChanged();
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_stepIndex == Steps.Length - 1)
        {
            DialogResult = true;
            return;
        }
        _stepIndex++;
        NotifyStepChanged();
    }

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void CloseWindow_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Caption_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) return;
        DragMove();
    }

    private void NotifyStepChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StepTitle)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StepBody)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProgressText)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NextButtonText)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanGoBack)));
    }
}
