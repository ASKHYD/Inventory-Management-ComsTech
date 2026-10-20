using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using SOFT2027.Core.Entities;
using SOFT2027.Core.Interfaces;
using SOFT2027.Presentation.Commands;
using SOFT2027.UI.Views;

namespace SOFT2027.Presentation.ViewModels;

public sealed class PurchaseEntryViewModel : ViewModelBase
{
    private readonly ITransactionCoordinator _transactions;
    private readonly ISupplierRepository _suppliers;
    private readonly IProductRepository _products;
    private readonly ISupplierPricingHistoryRepository _pricing;
    private readonly IAdminSession? _adminSession;
    private readonly IPurchaseRepository? _purchaseRepository;
    private Supplier? _selectedSupplier;
    private Product? _selectedProduct;
    private decimal _quantity = 1m;
    private decimal _unitPrice;
    private decimal _purchasePercentage;
    private decimal _discountPercentage;
    private decimal _paidAmount;
    private decimal _subTotal;
    private decimal _discountAmount;
    private decimal _netTotal;
    private decimal _remainingBalance;
    private bool _isBusy;
    private string _pricingMessage = "Choose a supplier and a product to see their previous price.";
    private string _pricingSource = "Product default";
    private string _paymentSource = "Cash";
    private int _pricingLookupVersion;
    private string _documentLookupNumber = string.Empty;

    public ObservableCollection<Supplier> Suppliers { get; } = new();
    public ObservableCollection<Product> Products { get; } = new();
    public ObservableCollection<SalesLineItemViewModel> Items { get; } = new();
    public ObservableCollection<TransactionDocumentHistoryItem> SavedBills { get; } = new();
    private TransactionDocumentHistoryItem? _selectedSavedBill;
    public TransactionDocumentHistoryItem? SelectedSavedBill
    {
        get => _selectedSavedBill;
        set
        {
            if (SetProperty(ref _selectedSavedBill, value) && value != null)
                DocumentLookupNumber = value.Number;
        }
    }
    public Supplier? SelectedSupplier
    {
        get => _selectedSupplier;
        set
        {
            if (SetProperty(ref _selectedSupplier, value)) _ = LoadPairPricingAsync();
        }
    }
    public Product? SelectedProduct
    {
        get => _selectedProduct;
        set
        {
            if (SetProperty(ref _selectedProduct, value))
            {
                if (value == null) { UnitPrice = 0; PurchasePercentage = 0; UseDefaultPriceCommand?.RaiseCanExecuteChanged(); }
                else { UnitPrice = value.BasePurchasePrice; PurchasePercentage = value.PurchasePercentage; _ = LoadPairPricingAsync(); }
            }
        }
    }
    public decimal Quantity { get => _quantity; set => SetProperty(ref _quantity, value); }
    public decimal UnitPrice { get => _unitPrice; set => SetProperty(ref _unitPrice, value); }
    public decimal PurchasePercentage { get => _purchasePercentage; set => SetProperty(ref _purchasePercentage, value); }
    public decimal DiscountPercentage { get => _discountPercentage; set => SetProperty(ref _discountPercentage, value); }
    public decimal PaidAmount { get => _paidAmount; set { if (SetProperty(ref _paidAmount, value)) RecalculateTotals(); } }
    public decimal SubTotal { get => _subTotal; private set => SetProperty(ref _subTotal, value); }
    public decimal DiscountAmount { get => _discountAmount; private set => SetProperty(ref _discountAmount, value); }
    public decimal NetTotal { get => _netTotal; private set => SetProperty(ref _netTotal, value); }
    public decimal RemainingBalance { get => _remainingBalance; private set => SetProperty(ref _remainingBalance, value); }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string PricingMessage { get => _pricingMessage; private set => SetProperty(ref _pricingMessage, value); }
    public string PricingSource { get => _pricingSource; private set => SetProperty(ref _pricingSource, value); }
    public string PaymentSource { get => _paymentSource; set => SetProperty(ref _paymentSource, value); }
    public string[] PaymentSources { get; } = ["Cash", "Bank"];
    public string DocumentLookupNumber { get => _documentLookupNumber; set => SetProperty(ref _documentLookupNumber, value); }
    public string? LastSavedDocumentNumber { get; private set; }

    public RelayCommand AddItemCommand { get; }
    public RelayCommand RemoveItemCommand { get; }
    public RelayCommand UsePreviousPriceCommand { get; }
    public RelayCommand UseDefaultPriceCommand { get; }
    public RelayCommand EnterNewPriceCommand { get; }
    public AsyncRelayCommand LoadCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public RelayCommand NewBillCommand { get; }

    public PurchaseEntryViewModel(ITransactionCoordinator transactions, ISupplierRepository suppliers, IProductRepository products, ISupplierPricingHistoryRepository pricing)
        : this(transactions, suppliers, products, pricing, null)
    {
    }

    public PurchaseEntryViewModel(ITransactionCoordinator transactions, ISupplierRepository suppliers, IProductRepository products, ISupplierPricingHistoryRepository pricing, IAdminSession? adminSession)
        : this(transactions, suppliers, products, pricing, adminSession, null)
    {
    }

    public PurchaseEntryViewModel(ITransactionCoordinator transactions, ISupplierRepository suppliers, IProductRepository products, ISupplierPricingHistoryRepository pricing, IAdminSession? adminSession, IPurchaseRepository? purchaseRepository)
    {
        _transactions = transactions ?? throw new ArgumentNullException(nameof(transactions));
        _suppliers = suppliers ?? throw new ArgumentNullException(nameof(suppliers));
        _products = products ?? throw new ArgumentNullException(nameof(products));
        _pricing = pricing ?? throw new ArgumentNullException(nameof(pricing));
        _adminSession = adminSession;
        _purchaseRepository = purchaseRepository;
        AddItemCommand = new RelayCommand(AddItem);
        RemoveItemCommand = new RelayCommand(param => RemoveItem(param as SalesLineItemViewModel));
        UsePreviousPriceCommand = new RelayCommand(() => ApplyMemoryPrice("previous"), () => _latestPrice != null);
        UseDefaultPriceCommand = new RelayCommand(() => ApplyMemoryPrice("default"), () => SelectedProduct != null);
        EnterNewPriceCommand = new RelayCommand(() => { _pricingLookupVersion++; PricingSource = "New price"; Announce("Enter the negotiated rate in Unit Price. It will be remembered when this bill is saved."); });
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy);
        NewBillCommand = new RelayCommand(ClearBill);
    }

    public void ClearBill()
    {
        foreach (var item in Items) item.LineTotalChanged -= RecalculateTotals;
        Items.Clear();
        SelectedSupplier = null;
        SelectedProduct = null;
        Quantity = 1;
        UnitPrice = 0;
        PurchasePercentage = 0;
        DiscountPercentage = 0;
        PaidAmount = 0;
        PaymentSource = "Cash";
        RecalculateTotals();
        ClearErrors();
        Announce("Purchase bill cleared. Ready for new bill.");
    }

    private SupplierProductPricingHistory? _latestPrice;

    private async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            ClearErrors();
            Suppliers.Clear();
            var allSuppliers = await _suppliers.GetAllAsync();
            foreach (var row in allSuppliers) if (row.Status == "Active") Suppliers.Add(row);
            Products.Clear();
            foreach (var row in await _products.GetAllAsync()) if (row.Status == "Active") Products.Add(row);
            await LoadSavedBillHistoryAsync(allSuppliers);
            if (Suppliers.Count == 0 || Products.Count == 0)
            {
                var missing = new System.Collections.Generic.List<string>();
                if (Suppliers.Count == 0) missing.Add("a supplier");
                if (Products.Count == 0) missing.Add("a product");
                Announce($"To start a purchase, add {string.Join(" and ", missing)} in Master Data.");
            }
            else Announce("Suppliers and products are ready. Choose a supplier to begin.");
        }
        catch (Exception ex) { SetError($"Could not load purchase lists. {ex.Message}"); }
        finally { IsBusy = false; }
    }

    private async Task LoadSavedBillHistoryAsync(IReadOnlyList<Supplier> suppliers)
    {
        SavedBills.Clear();
        if (_purchaseRepository == null) return;
        var entries = new System.Collections.Generic.List<(Purchase Purchase, string Party)>();
        foreach (var supplier in suppliers)
            foreach (var purchase in await _purchaseRepository.GetBySupplierIdAsync(supplier.SupplierId))
                if (!string.Equals(purchase.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)) entries.Add((purchase, supplier.SupplierName));
        foreach (var entry in entries.OrderByDescending(x => x.Purchase.PurchaseDate))
            SavedBills.Add(new TransactionDocumentHistoryItem
            {
                Number = entry.Purchase.InvoiceNumber,
                Date = entry.Purchase.PurchaseDate,
                Total = entry.Purchase.NetTotal,
                DisplayText = $"{entry.Purchase.InvoiceNumber}  ·  {entry.Purchase.PurchaseDate:d}  ·  {entry.Party}  ·  {entry.Purchase.NetTotal:N2}"
            });
    }

    private async Task LoadPairPricingAsync()
    {
        var version = ++_pricingLookupVersion;
            var supplier = SelectedSupplier;
            var product = SelectedProduct;
            _latestPrice = null;
            UsePreviousPriceCommand.RaiseCanExecuteChanged();
            UseDefaultPriceCommand.RaiseCanExecuteChanged();
        if (product == null) return;
        UnitPrice = product.BasePurchasePrice;
        PurchasePercentage = product.PurchasePercentage;
        DiscountPercentage = 0m;
        PricingSource = "Product default";
        if (supplier == null) { PricingMessage = "Select a supplier to check their previous rate for this product."; return; }
        try
        {
            var prior = await _pricing.GetLatestAsync(supplier.SupplierId, product.ProductId);
            if (version != _pricingLookupVersion || supplier != SelectedSupplier || product != SelectedProduct) return;
            if (prior == null)
            {
                PricingMessage = "No earlier purchase found for this pair. The product default rate is shown.";
                return;
            }
            _latestPrice = prior;
            UnitPrice = prior.PurchasePrice;
            PurchasePercentage = prior.PurchasePercentage;
            DiscountPercentage = prior.DiscountPercentage;
            PricingSource = "Previous supplier rate";
            PricingMessage = $"Last rate {prior.PurchasePrice:N2} · purchase % {prior.PurchasePercentage:N2} · discount {prior.DiscountPercentage:N2}% · {prior.TransactionDate:d} · {prior.InvoiceNumber} · {prior.Quantity:N2} units";
            UsePreviousPriceCommand.RaiseCanExecuteChanged();
        }
        catch (Exception ex) { PricingMessage = "Could not retrieve pricing history. You can enter the rate manually."; SetError(ex.Message); }
    }

    private void ApplyMemoryPrice(string choice)
    {
        if (SelectedProduct == null) return;
        _pricingLookupVersion++;
        if (choice == "previous" && _latestPrice != null)
        {
            UnitPrice = _latestPrice.PurchasePrice;
            PurchasePercentage = _latestPrice.PurchasePercentage;
            DiscountPercentage = _latestPrice.DiscountPercentage;
            PricingSource = "Previous supplier rate";
        }
        else
        {
            UnitPrice = SelectedProduct.BasePurchasePrice;
            PurchasePercentage = SelectedProduct.PurchasePercentage;
            DiscountPercentage = 0m;
            PricingSource = "Product default";
        }
    }

    private void AddItem()
    {
        if (SelectedProduct == null) { SetError("Choose a product from the dropdown list before adding it to the bill. Typed text alone does not select a product."); return; }
        if (Quantity <= 0 || UnitPrice < 0 || DiscountPercentage is < 0 or > 100)
        { SetError("Quantity must be above zero. Price must be zero or higher, and discount must be between 0 and 100%."); return; }
        var item = new SalesLineItemViewModel
        {
            Product = SelectedProduct,
            Quantity = Quantity,
            UnitPrice = UnitPrice,
            CostPrice = UnitPrice,
            SellingPercentage = PurchasePercentage,
            DiscountPercentage = DiscountPercentage
        };
        item.LineTotalChanged += RecalculateTotals;
        Items.Add(item);
        RecalculateTotals();
        Announce($"{SelectedProduct.ProductName} added to the purchase.");
        SelectedProduct = null;
        Quantity = 1;
        UnitPrice = 0;
        PurchasePercentage = 0;
        DiscountPercentage = 0;
    }

    private void RemoveItem(SalesLineItemViewModel? item)
    {
        if (item == null || !Items.Remove(item)) return;
        item.LineTotalChanged -= RecalculateTotals;
        RecalculateTotals();
        Announce($"{item.ProductName} removed from the purchase.");
    }

    private void RecalculateTotals()
    {
        SubTotal = Items.Sum(item => item.Quantity * item.UnitPrice);
        DiscountAmount = Items.Sum(item => item.Quantity * item.UnitPrice * item.DiscountPercentage / 100m);
        NetTotal = SubTotal - DiscountAmount;
        RemainingBalance = Math.Max(0, NetTotal - PaidAmount);
    }

    private async Task SaveAsync()
    {
        if (SelectedSupplier == null || Items.Count == 0) { SetError("Choose a supplier and add at least one product before saving."); return; }
        if (PaidAmount < 0 || PaidAmount > NetTotal) { SetError("Amount paid must be between zero and the bill total."); return; }
        if (Items.Any(item => item.Quantity <= 0 || item.UnitPrice < 0 || item.DiscountPercentage is < 0 or > 100))
        { SetError("Review each row: quantity must be above zero, price cannot be negative, and discount must be between 0 and 100%."); return; }
        try
        {
            IsBusy = true;
            ClearErrors();
            var now = DateTime.Now;
            var purchase = new Purchase
            {
                InvoiceNumber = $"PUR-{now:yyyyMMddHHmmssfff}",
                SupplierId = SelectedSupplier.SupplierId,
                PurchaseDate = now,
                SubTotal = SubTotal,
                DiscountAmount = DiscountAmount,
                NetTotal = NetTotal,
                PaidAmount = PaidAmount,
                PaymentSource = PaymentSource,
                RemainingBalance = RemainingBalance,
                Status = "Received",
                AdminId = _adminSession?.AdminId ?? 1,
                CreatedAt = now,
                Items = Items.Select(item => new PurchaseItem
                {
                    ProductId = item.ProductId,
                    ProductCodeAtPurchase = item.Product?.ProductCode ?? string.Empty,
                    ProductNameAtPurchase = item.Product?.ProductName ?? item.ProductName,
                    Quantity = item.Quantity,
                    HistoricalPurchasePrice = item.UnitPrice,
                    PurchasePercentage = item.SellingPercentage,
                    DiscountPercentage = item.DiscountPercentage,
                    LineTotal = item.LineTotal
                }).ToList()
            };
            await _transactions.PostPurchaseAsync(purchase);
            LastSavedDocumentNumber = purchase.InvoiceNumber;
            DocumentLookupNumber = purchase.InvoiceNumber;
            Announce($"Purchase {purchase.InvoiceNumber} saved. Stock and supplier balance were updated.");
            foreach (var item in Items) item.LineTotalChanged -= RecalculateTotals;
            Items.Clear();
            SelectedSupplier = null;
            PaidAmount = 0;
            PaymentSource = "Cash";
            RecalculateTotals();
        }
        catch (Exception ex) { SetError($"The purchase was not saved. {ex.Message}"); }
        finally { IsBusy = false; }
    }

    public async Task<TransactionDocumentData> LoadDocumentForPreviewAsync(string invoiceNumber)
    {
        if (_purchaseRepository == null) throw new InvalidOperationException("Saved purchase lookup is not available.");
        if (string.IsNullOrWhiteSpace(invoiceNumber)) throw new InvalidOperationException("Enter a saved purchase bill number.");
        var purchase = await _purchaseRepository.GetByInvoiceNumberAsync(invoiceNumber.Trim())
            ?? throw new InvalidOperationException($"Purchase bill {invoiceNumber.Trim()} was not found.");
        var supplier = await _suppliers.GetByIdAsync(purchase.SupplierId);
        if (supplier == null) throw new InvalidOperationException("The supplier for this bill could not be found.");
        var lines = new System.Collections.Generic.List<TransactionDocumentLine>();
        foreach (var item in purchase.Items)
        {
            var product = await _products.GetByIdAsync(item.ProductId);
            lines.Add(new TransactionDocumentLine
            {
                ProductCode = string.IsNullOrWhiteSpace(item.ProductCodeAtPurchase) ? product?.ProductCode ?? item.ProductId.ToString() : item.ProductCodeAtPurchase,
                ProductName = string.IsNullOrWhiteSpace(item.ProductNameAtPurchase) ? product?.ProductName ?? "Product no longer available" : item.ProductNameAtPurchase,
                Quantity = item.Quantity,
                UnitPrice = item.HistoricalPurchasePrice,
                DiscountPercentage = item.DiscountPercentage,
                LineTotal = item.LineTotal
            });
        }
        return new TransactionDocumentData
        {
            Title = "Purchase Bill", Number = purchase.InvoiceNumber, Date = purchase.PurchaseDate,
            PartyLabel = "Supplier", PartyName = supplier.SupplierName, PartyCode = supplier.SupplierCode, ContactPerson = supplier.ContactPerson, Phone = supplier.Phone, Address = supplier.Address,
            Lines = lines, SubTotal = purchase.SubTotal, Discount = purchase.DiscountAmount, Total = purchase.NetTotal,
            Paid = purchase.PaidAmount, Balance = purchase.RemainingBalance, PaymentSource = purchase.PaymentSource, Remarks = purchase.Remarks
        };
    }
}
