using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using SOFT2027.Core.Entities;
using SOFT2027.Core.Interfaces;
using SOFT2027.Presentation.Commands;

namespace SOFT2027.Presentation.ViewModels;

public sealed class ReturnLineViewModel : ViewModelBase
{
    private decimal _quantity;
    public int ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public decimal OriginalQuantity { get; init; }
    public decimal Rate { get; init; }
    public decimal Quantity { get => _quantity; set { if (SetProperty(ref _quantity, value)) OnPropertyChanged(nameof(LineTotal)); } }
    public decimal LineTotal => Quantity * Rate;
}

public sealed class ReturnEntryViewModel : ViewModelBase
{
    private readonly bool _saleReturn;
    private readonly ISalesRepository _sales;
    private readonly IPurchaseRepository _purchases;
    private readonly IProductRepository _products;
    private readonly ITransactionCoordinator _transactions;
    private readonly IAdminSession _session;
    private string _invoiceNumber = string.Empty;
    private string _remarks = string.Empty;

    public ReturnEntryViewModel(bool saleReturn, ISalesRepository sales, IPurchaseRepository purchases, IProductRepository products, ITransactionCoordinator transactions, IAdminSession session)
    {
        _saleReturn = saleReturn; _sales = sales; _purchases = purchases; _products = products; _transactions = transactions; _session = session;
        LoadInvoiceCommand = new AsyncRelayCommand(LoadInvoiceAsync);
        SaveReturnCommand = new AsyncRelayCommand(SaveAsync);
    }

    public string Title => _saleReturn ? "Sales Returns" : "Purchase Returns";
    public string HelpText => _saleReturn ? "Enter the original sales invoice. Only quantities on that invoice can be returned; stock and the customer account update together." : "Enter the original purchase invoice. Returned quantities reduce stock and the supplier balance together.";
    public string InvoiceNumber { get => _invoiceNumber; set => SetProperty(ref _invoiceNumber, value); }
    public string Remarks { get => _remarks; set => SetProperty(ref _remarks, value); }
    public ObservableCollection<ReturnLineViewModel> Lines { get; } = new();
    public AsyncRelayCommand LoadInvoiceCommand { get; }
    public AsyncRelayCommand SaveReturnCommand { get; }

    private async Task LoadInvoiceAsync()
    {
        try
        {
            ClearErrors(); Lines.Clear();
            if (string.IsNullOrWhiteSpace(InvoiceNumber)) throw new InvalidOperationException("Enter the original invoice number first.");
            if (_saleReturn)
            {
                var sale = await _sales.GetByInvoiceNumberAsync(InvoiceNumber.Trim()) ?? throw new InvalidOperationException("Sales invoice not found.");
                foreach (var group in sale.Items.GroupBy(item => item.ProductId))
                {
                    var item = group.ToArray(); var quantity = item.Sum(row => row.Quantity);
                    var rate = quantity > 0 ? item.Sum(row => row.LineTotal) / quantity : 0m;
                    var product = await _products.GetByIdAsync(group.Key);
                    Lines.Add(new ReturnLineViewModel { ProductId = group.Key, ProductName = product?.ProductName ?? $"Product {group.Key}", OriginalQuantity = quantity, Rate = rate });
                }
            }
            else
            {
                var purchase = await _purchases.GetByInvoiceNumberAsync(InvoiceNumber.Trim()) ?? throw new InvalidOperationException("Purchase invoice not found.");
                foreach (var group in purchase.Items.GroupBy(item => item.ProductId))
                {
                    var item = group.ToArray(); var quantity = item.Sum(row => row.Quantity);
                    var rate = quantity > 0 ? item.Sum(row => row.LineTotal) / quantity : 0m;
                    var product = await _products.GetByIdAsync(group.Key);
                    Lines.Add(new ReturnLineViewModel { ProductId = group.Key, ProductName = product?.ProductName ?? $"Product {group.Key}", OriginalQuantity = quantity, Rate = rate });
                }
            }
            if (Lines.Count == 0) throw new InvalidOperationException("This invoice has no returnable items.");
            Announce($"Loaded {Lines.Count} invoice item(s). Enter the quantity being returned.");
        }
        catch (Exception ex) { SetError(ex.Message); }
    }

    private async Task SaveAsync()
    {
        try
        {
            ClearErrors();
            var lines = Lines.Where(line => line.Quantity > 0).ToArray();
            if (lines.Length == 0) throw new InvalidOperationException("Enter a return quantity for at least one item.");
            if (lines.Any(line => line.Quantity > line.OriginalQuantity)) throw new InvalidOperationException("A return quantity cannot exceed the original invoice quantity.");
            var code = _saleReturn ? "SR" : "PR";
            var request = new ReturnRequest
            {
                IsSaleReturn = _saleReturn,
                OriginalInvoiceNumber = InvoiceNumber.Trim(),
                ReturnNumber = $"{code}-{DateTime.Now:yyyyMMdd-HHmmssfff}",
                ReturnDate = DateTime.Today,
                AdminId = _session.AdminId > 0 ? _session.AdminId : throw new InvalidOperationException("Sign in again before saving this return."),
                Remarks = Remarks.Trim(),
                Items = lines.Select(line => new ReturnRequestLine { ProductId = line.ProductId, Quantity = line.Quantity, RefundRate = line.Rate }).ToList()
            };
            await _transactions.PostReturnAsync(request);
            Announce($"Return {request.ReturnNumber} saved. Stock and account balances are updated.");
            Lines.Clear(); InvoiceNumber = string.Empty; Remarks = string.Empty;
        }
        catch (Exception ex) { SetError(ex.Message); }
    }
}
