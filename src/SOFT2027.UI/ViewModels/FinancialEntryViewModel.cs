using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using SOFT2027.Core.Entities;
using SOFT2027.Core.Interfaces;
using SOFT2027.Presentation.Commands;

namespace SOFT2027.Presentation.ViewModels;

public sealed class FinancialEntryViewModel : ViewModelBase
{
    private readonly string _kind;
    private readonly IFinancialCoordinator _financial;
    private readonly ICustomerRepository _customers;
    private readonly ISupplierRepository _suppliers;
    private readonly IExpenseRepository _expenses;
    private readonly IAdminSession _session;
    private Customer? _selectedCustomer;
    private Supplier? _selectedSupplier;
    private DateTime _entryDate = DateTime.Today;
    private decimal _amount;
    private string _paymentMode = "Cash";
    private string _expenseCategory = string.Empty;
    private string _description = string.Empty;
    private string _referenceDetails = string.Empty;
    private bool _isBusy;

    public string Kind => _kind;
    public string Title => _kind switch { "Receipt" => "Record a Customer Receipt", "Payment" => "Record a Supplier Payment", _ => "Record an Expense" };
    public string SaveButtonText => _kind switch { "Receipt" => "Save Receipt", "Payment" => "Save Supplier Payment", _ => "Save Expense" };
    public string HelpText => _kind switch { "Receipt" => "Record money received from a customer. The customer balance and cash or bank book update together.", "Payment" => "Record money paid to a supplier. The supplier balance and cash or bank book update together.", _ => "Record a business expense and choose whether it was paid from cash or bank." };
    public ObservableCollection<Customer> Customers { get; } = new();
    public ObservableCollection<Supplier> Suppliers { get; } = new();
    public string[] PaymentModes { get; } = ["Cash", "Bank", "Cheque", "Online transfer"];
    public string[] ExpenseSources { get; } = ["Cash", "Bank"];
    public string[] PaymentOptions => _kind == "Expense" ? ExpenseSources : PaymentModes;

    public Customer? SelectedCustomer { get => _selectedCustomer; set => SetProperty(ref _selectedCustomer, value); }
    public Supplier? SelectedSupplier { get => _selectedSupplier; set => SetProperty(ref _selectedSupplier, value); }
    public DateTime EntryDate { get => _entryDate; set => SetProperty(ref _entryDate, value); }
    public decimal Amount { get => _amount; set => SetProperty(ref _amount, value); }
    public string PaymentMode { get => _paymentMode; set => SetProperty(ref _paymentMode, value); }
    public string ExpenseCategory { get => _expenseCategory; set => SetProperty(ref _expenseCategory, value); }
    public string Description { get => _description; set => SetProperty(ref _description, value); }
    public string ReferenceDetails { get => _referenceDetails; set => SetProperty(ref _referenceDetails, value); }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public AsyncRelayCommand LoadCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }

    public FinancialEntryViewModel(string kind, IFinancialCoordinator financial, ICustomerRepository customers,
        ISupplierRepository suppliers, IExpenseRepository expenses, IAdminSession session)
    {
        _kind = kind is "Receipt" or "Payment" or "Expense" ? kind : throw new ArgumentOutOfRangeException(nameof(kind));
        _financial = financial ?? throw new ArgumentNullException(nameof(financial));
        _customers = customers ?? throw new ArgumentNullException(nameof(customers));
        _suppliers = suppliers ?? throw new ArgumentNullException(nameof(suppliers));
        _expenses = expenses ?? throw new ArgumentNullException(nameof(expenses));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy);
    }

    private async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            ClearErrors();
            if (_kind == "Receipt")
            {
                Customers.Clear();
                foreach (var customer in await _customers.GetAllAsync()) if (customer.Status == "Active") Customers.Add(customer);
            }
            else if (_kind == "Payment")
            {
                Suppliers.Clear();
                foreach (var supplier in await _suppliers.GetAllAsync()) if (supplier.Status == "Active") Suppliers.Add(supplier);
            }
            Announce($"{_kind} form is ready.");
        }
        catch (Exception ex) { SetError($"Could not load the form. {ex.Message}"); }
        finally { IsBusy = false; }
    }

    private async Task SaveAsync()
    {
        if (Amount <= 0) { SetError("Enter an amount greater than zero."); return; }
        if (_kind == "Receipt" && SelectedCustomer == null) { SetError("Choose the customer who paid you."); return; }
        if (_kind == "Payment" && SelectedSupplier == null) { SetError("Choose the supplier you paid."); return; }
        if (_kind == "Expense" && string.IsNullOrWhiteSpace(ExpenseCategory)) { SetError("Enter an expense category, such as Fuel or Rent."); return; }
        if (_kind == "Expense" && string.IsNullOrWhiteSpace(Description)) { SetError("Describe what the expense was for."); return; }
        if (_session.AdminId <= 0) { SetError("Sign in again before recording financial activity."); return; }

        try
        {
            IsBusy = true;
            ClearErrors();
            var now = DateTime.Now;
            if (_kind == "Receipt")
            {
                await _financial.PostReceiptAsync(new Receipt
                {
                    ReceiptNumber = $"REC-{now:yyyyMMddHHmmssfff}", CustomerId = SelectedCustomer!.CustomerId,
                    ReceiptDate = EntryDate, Amount = Amount, PaymentMode = PaymentMode,
                    ReferenceDetails = ReferenceDetails, Remarks = Description, AdminId = _session.AdminId
                });
            }
            else if (_kind == "Payment")
            {
                await _financial.PostPaymentAsync(new Payment
                {
                    PaymentNumber = $"PAY-{now:yyyyMMddHHmmssfff}", SupplierId = SelectedSupplier!.SupplierId,
                    PaymentDate = EntryDate, Amount = Amount, PaymentMode = PaymentMode,
                    ReferenceDetails = ReferenceDetails, Remarks = Description, AdminId = _session.AdminId
                });
            }
            else
            {
                await _financial.PostExpenseAsync(new Expense
                {
                    ExpenseDate = EntryDate, ExpenseCategory = ExpenseCategory.Trim(), Amount = Amount,
                    Description = Description.Trim(), PaymentSource = PaymentMode, AdminId = _session.AdminId
                });
            }
            Announce($"{_kind} saved. Related account and cash or bank records were updated.");
            Amount = 0;
            Description = string.Empty;
            ReferenceDetails = string.Empty;
            ExpenseCategory = string.Empty;
            SelectedCustomer = null;
            SelectedSupplier = null;
            EntryDate = DateTime.Today;
        }
        catch (Exception ex) { SetError($"The {_kind.ToLowerInvariant()} was not saved. {ex.Message}"); }
        finally { IsBusy = false; }
    }
}
