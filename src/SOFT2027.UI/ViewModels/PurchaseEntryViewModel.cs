using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using SOFT2027.Core.Entities;
using SOFT2027.Core.Interfaces;
using SOFT2027.Presentation.Commands;

namespace SOFT2027.Presentation.ViewModels;

public sealed class PurchaseEntryViewModel : ViewModelBase
{
    private readonly ITransactionCoordinator _transactions;
    private readonly ISupplierRepository _suppliers;
    private readonly IProductRepository _products;
    private readonly ISupplierPricingHistoryRepository _pricing;
    private readonly IAdminSession? _adminSession;
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

    public ObservableCollection<Supplier> Suppliers { get; } = new();
    public ObservableCollection<Product> Products { get; } = new();
    public ObservableCollection<SalesLineItemViewModel> Items { get; } = new();
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

    public RelayCommand AddItemCommand { get; }
    public RelayCommand RemoveItemCommand { get; }
    public RelayCommand UsePreviousPriceCommand { get; }
    public RelayCommand UseDefaultPriceCommand { get; }
    public RelayCommand EnterNewPriceCommand { get; }
    public AsyncRelayCommand LoadCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }

    public PurchaseEntryViewModel(ITransactionCoordinator transactions, ISupplierRepository suppliers, IProductRepository products, ISupplierPricingHistoryRepository pricing)
        : this(transactions, suppliers, products, pricing, null)
    {
    }

    public PurchaseEntryViewModel(ITransactionCoordinator transactions, ISupplierRepository suppliers, IProductRepository products, ISupplierPricingHistoryRepository pricing, IAdminSession? adminSession)
    {
        _transactions = transactions ?? throw new ArgumentNullException(nameof(transactions));
        _suppliers = suppliers ?? throw new ArgumentNullException(nameof(suppliers));
        _products = products ?? throw new ArgumentNullException(nameof(products));
        _pricing = pricing ?? throw new ArgumentNullException(nameof(pricing));
        _adminSession = adminSession;
        AddItemCommand = new RelayCommand(AddItem);
        RemoveItemCommand = new RelayCommand(param => RemoveItem(param as SalesLineItemViewModel));
        UsePreviousPriceCommand = new RelayCommand(() => ApplyMemoryPrice("previous"), () => _latestPrice.HasValue);
        UseDefaultPriceCommand = new RelayCommand(() => ApplyMemoryPrice("default"), () => SelectedProduct != null);
        EnterNewPriceCommand = new RelayCommand(() => { PricingSource = "New price"; Announce("Enter the negotiated rate in Unit Price. It will be remembered when this bill is saved."); });
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy);
    }

    private decimal? _latestPrice;

    private async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            ClearErrors();
            Suppliers.Clear();
            foreach (var row in await _suppliers.GetAllAsync()) if (row.Status == "Active") Suppliers.Add(row);
            Products.Clear();
            foreach (var row in await _products.GetAllAsync()) if (row.Status == "Active") Products.Add(row);
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
            _latestPrice = prior.PurchasePrice;
            UnitPrice = prior.PurchasePrice;
            PurchasePercentage = prior.PurchasePercentage;
            DiscountPercentage = prior.DiscountPercentage;
            PricingSource = "Previous supplier rate";
            PricingMessage = $"Last rate {prior.PurchasePrice:N2} on {prior.TransactionDate:d} · {prior.InvoiceNumber} · {prior.Quantity:N2} units";
            UsePreviousPriceCommand.RaiseCanExecuteChanged();
        }
        catch (Exception ex) { PricingMessage = "Could not retrieve pricing history. You can enter the rate manually."; SetError(ex.Message); }
    }

    private void ApplyMemoryPrice(string choice)
    {
        if (SelectedProduct == null) return;
        if (choice == "previous" && _latestPrice.HasValue)
        {
            UnitPrice = _latestPrice.Value;
            PricingSource = "Previous supplier rate";
        }
        else
        {
            UnitPrice = SelectedProduct.BasePurchasePrice;
            PurchasePercentage = SelectedProduct.PurchasePercentage;
            PricingSource = "Product default";
        }
    }

    private void AddItem()
    {
        if (SelectedProduct == null) { SetError("Choose a product before adding it to the bill."); return; }
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
                    Quantity = item.Quantity,
                    HistoricalPurchasePrice = item.UnitPrice,
                    PurchasePercentage = item.SellingPercentage,
                    DiscountPercentage = item.DiscountPercentage,
                    LineTotal = item.LineTotal
                }).ToList()
            };
            await _transactions.PostPurchaseAsync(purchase);
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
}
