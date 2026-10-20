using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using SOFT2027.Core.Entities;
using SOFT2027.Core.Interfaces;
using SOFT2027.Presentation.Commands;
using SOFT2027.UI.Views;

namespace SOFT2027.Presentation.ViewModels
{
    public class SalesEntryViewModel : ViewModelBase
    {
        private readonly ITransactionCoordinator _transactionCoordinator;
        private readonly ICustomerRepository _customerRepository;
        private readonly IProductRepository _productRepository;
        private readonly ICustomerPricingHistoryRepository _pricingHistoryRepository;
        private readonly IAdminSession? _adminSession;
        private readonly ISalesRepository? _salesRepository;

        private Customer? _selectedCustomer;
        private Product? _selectedProduct;
        private decimal _quantity = 1m;
        private decimal _unitPrice;
        private decimal _costPrice;
        private decimal _sellingPercentage;
        private decimal _discountPercentage;
        private decimal _subTotal;
        private decimal _discountAmount;
        private decimal _netTotal;
        private decimal _paidAmount;
        private decimal _remainingBalance;
        private string _paymentType = "Cash";
        private string _paymentSource = "Cash";
        private string? _remarks;
        private bool _isBusy;
        private CustomerProductPricingHistory? _latestCustomerPrice;
        private int _pricingLookupVersion;
        private string _pricingMessage = "Choose a customer and product to check their previous rate.";
        private string _documentLookupNumber = string.Empty;

        public ObservableCollection<Customer> Customers { get; } = new ObservableCollection<Customer>();
        public ObservableCollection<Product> Products { get; } = new ObservableCollection<Product>();
        public ObservableCollection<SalesLineItemViewModel> Items { get; } = new ObservableCollection<SalesLineItemViewModel>();
        public ObservableCollection<TransactionDocumentHistoryItem> SavedInvoices { get; } = new();
        private TransactionDocumentHistoryItem? _selectedSavedInvoice;
        public TransactionDocumentHistoryItem? SelectedSavedInvoice
        {
            get => _selectedSavedInvoice;
            set
            {
                if (SetProperty(ref _selectedSavedInvoice, value) && value != null)
                    DocumentLookupNumber = value.Number;
            }
        }

        public Customer? SelectedCustomer
        {
            get => _selectedCustomer;
            set
            {
                if (SetProperty(ref _selectedCustomer, value))
                {
                    OnPropertyChanged(nameof(CustomerCurrentBalance));
                    OnPropertyChanged(nameof(CustomerCreditLimit));
                    OnPropertyChanged(nameof(CustomerAvailableCredit));
                    OnPropertyChanged(nameof(IsCustomerOverLimit));
                    OnPropertyChanged(nameof(IsCustomerBalanceZero));
                    _ = LoadPairPricingAsync();
                }
            }
        }

        public Product? SelectedProduct
        {
            get => _selectedProduct;
            set => SetProperty(ref _selectedProduct, value, () => OnProductSelected(value));
        }

        public decimal Quantity
        {
            get => _quantity;
            set => SetProperty(ref _quantity, value);
        }

        public decimal UnitPrice
        {
            get => _unitPrice;
            set
            {
                if (SetProperty(ref _unitPrice, value) && CostPrice > 0m)
                    SellingPercentage = ((value - CostPrice) / CostPrice) * 100m;
            }
        }

        public decimal CostPrice
        {
            get => _costPrice;
            set => SetProperty(ref _costPrice, value);
        }

        public decimal SellingPercentage
        {
            get => _sellingPercentage;
            set => SetProperty(ref _sellingPercentage, value);
        }

        public decimal DiscountPercentage
        {
            get => _discountPercentage;
            set => SetProperty(ref _discountPercentage, value);
        }

        public decimal SubTotal
        {
            get => _subTotal;
            set => SetProperty(ref _subTotal, value);
        }

        public decimal DiscountAmount
        {
            get => _discountAmount;
            set => SetProperty(ref _discountAmount, value);
        }

        public decimal NetTotal
        {
            get => _netTotal;
            set => SetProperty(ref _netTotal, value);
        }

        public decimal PaidAmount
        {
            get => _paidAmount;
            set => SetProperty(ref _paidAmount, value, RecalculateTotals);
        }

        public decimal RemainingBalance
        {
            get => _remainingBalance;
            set => SetProperty(ref _remainingBalance, value);
        }

        public string PaymentType
        {
            get => _paymentType;
            set => SetProperty(ref _paymentType, value);
        }

        public string PaymentSource
        {
            get => _paymentSource;
            set => SetProperty(ref _paymentSource, value);
        }

        public string[] PaymentSources { get; } = ["Cash", "Bank"];

        public string? Remarks
        {
            get => _remarks;
            set => SetProperty(ref _remarks, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public string PricingMessage
        {
            get => _pricingMessage;
            private set => SetProperty(ref _pricingMessage, value);
        }

        public string DocumentLookupNumber { get => _documentLookupNumber; set => SetProperty(ref _documentLookupNumber, value); }
        public string? LastSavedDocumentNumber { get; private set; }

        public decimal CustomerCurrentBalance => SelectedCustomer?.OutstandingBalance ?? 0m;
        public decimal CustomerCreditLimit => SelectedCustomer?.CreditLimit ?? 0m;
        public decimal CustomerAvailableCredit => (SelectedCustomer?.CreditLimit ?? 0m) - (SelectedCustomer?.OutstandingBalance ?? 0m);
        public bool IsCustomerOverLimit => SelectedCustomer != null && SelectedCustomer.CreditLimit > 0m && SelectedCustomer.OutstandingBalance > SelectedCustomer.CreditLimit;
        public bool IsCustomerBalanceZero => SelectedCustomer != null && SelectedCustomer.OutstandingBalance == 0m;

        public decimal AvailableStock => SelectedProduct?.CurrentStock ?? 0m;
        public bool IsOutOfStock => SelectedProduct != null && SelectedProduct.CurrentStock <= 0m;
        public bool IsLowStock => SelectedProduct != null && SelectedProduct.CurrentStock > 0m && SelectedProduct.CurrentStock <= SelectedProduct.MinimumStock;
        public string StockStatusBadgeText => SelectedProduct == null ? string.Empty : SelectedProduct.CurrentStock <= 0m ? "⛔ Out of Stock" : SelectedProduct.CurrentStock <= SelectedProduct.MinimumStock ? $"⚠ Low Stock: {SelectedProduct.CurrentStock:N0} left" : $"✓ Stock: {SelectedProduct.CurrentStock:N0}";

        private bool _isOverSellWarningOpen;
        public bool IsOverSellWarningOpen { get => _isOverSellWarningOpen; set => SetProperty(ref _isOverSellWarningOpen, value); }
        private string _overSellWarningMessage = string.Empty;
        public string OverSellWarningMessage { get => _overSellWarningMessage; set => SetProperty(ref _overSellWarningMessage, value); }
        public Product? PendingOverSellProduct { get; private set; }
        public decimal RequestedOverSellQuantity { get; private set; }

        private bool _isCreditLimitWarningOpen;
        public bool IsCreditLimitWarningOpen { get => _isCreditLimitWarningOpen; set => SetProperty(ref _isCreditLimitWarningOpen, value); }
        private string _creditLimitWarningMessage = string.Empty;
        public string CreditLimitWarningMessage { get => _creditLimitWarningMessage; set => SetProperty(ref _creditLimitWarningMessage, value); }

        private bool _isHotkeysModalOpen;
        public bool IsHotkeysModalOpen { get => _isHotkeysModalOpen; set => SetProperty(ref _isHotkeysModalOpen, value); }

        public RelayCommand AddItemCommand { get; }
        public RelayCommand RemoveItemCommand { get; }
        public AsyncRelayCommand SaveSaleCommand { get; }
        public AsyncRelayCommand LoadInitialDataCommand { get; }
        public RelayCommand UsePreviousPriceCommand { get; }
        public RelayCommand UseDefaultPriceCommand { get; }
        public RelayCommand EnterNewPriceCommand { get; }

        public RelayCommand AdjustQuantityToAvailableCommand { get; }
        public RelayCommand CancelOverSellLineCommand { get; }
        public RelayCommand AllowNegativeStockOverrideCommand { get; }
        public AsyncRelayCommand SaveCreditBillCommand { get; }
        public AsyncRelayCommand ProceedWithCreditBillCommand { get; }
        public RelayCommand CancelCreditBillCommand { get; }
        public RelayCommand NewBillCommand { get; }
        public RelayCommand OpenHotkeysModalCommand { get; }
        public RelayCommand CloseHotkeysModalCommand { get; }

        public SalesEntryViewModel(
            ITransactionCoordinator transactionCoordinator,
            ICustomerRepository customerRepository,
            IProductRepository productRepository,
            ICustomerPricingHistoryRepository pricingHistoryRepository)
            : this(transactionCoordinator, customerRepository, productRepository, pricingHistoryRepository, null)
        {
        }

        public SalesEntryViewModel(
            ITransactionCoordinator transactionCoordinator,
            ICustomerRepository customerRepository,
            IProductRepository productRepository,
            ICustomerPricingHistoryRepository pricingHistoryRepository,
            IAdminSession? adminSession)
            : this(transactionCoordinator, customerRepository, productRepository, pricingHistoryRepository, adminSession, null)
        {
        }

        public SalesEntryViewModel(
            ITransactionCoordinator transactionCoordinator,
            ICustomerRepository customerRepository,
            IProductRepository productRepository,
            ICustomerPricingHistoryRepository pricingHistoryRepository,
            IAdminSession? adminSession,
            ISalesRepository? salesRepository)
        {
            _transactionCoordinator = transactionCoordinator ?? throw new ArgumentNullException(nameof(transactionCoordinator));
            _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
            _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
            _pricingHistoryRepository = pricingHistoryRepository ?? throw new ArgumentNullException(nameof(pricingHistoryRepository));
            _adminSession = adminSession;
            _salesRepository = salesRepository;

            AddItemCommand = new RelayCommand(AddItem);
            RemoveItemCommand = new RelayCommand(param => RemoveItem(param as SalesLineItemViewModel));
            SaveSaleCommand = new AsyncRelayCommand(SaveSaleAsync);
            LoadInitialDataCommand = new AsyncRelayCommand(LoadInitialDataAsync);
            UsePreviousPriceCommand = new RelayCommand(() => ApplyCustomerPrice(usePrevious: true), () => _latestCustomerPrice != null);
            UseDefaultPriceCommand = new RelayCommand(() => ApplyCustomerPrice(usePrevious: false), () => SelectedProduct != null);
            EnterNewPriceCommand = new RelayCommand(() => { _pricingLookupVersion++; Announce("Enter the negotiated rate in Unit Price. It will be remembered when this invoice is saved."); });

            AdjustQuantityToAvailableCommand = new RelayCommand(AdjustQuantityToAvailable);
            CancelOverSellLineCommand = new RelayCommand(() => IsOverSellWarningOpen = false);
            AllowNegativeStockOverrideCommand = new RelayCommand(() => { IsOverSellWarningOpen = false; AddItemInternal(allowOverride: true); });

            SaveCreditBillCommand = new AsyncRelayCommand(SaveCreditBillAsync);
            ProceedWithCreditBillCommand = new AsyncRelayCommand(async () => { IsCreditLimitWarningOpen = false; await SaveSaleAsync(); });
            CancelCreditBillCommand = new RelayCommand(() => IsCreditLimitWarningOpen = false);

            NewBillCommand = new RelayCommand(ClearBill);
            OpenHotkeysModalCommand = new RelayCommand(() => IsHotkeysModalOpen = true);
            CloseHotkeysModalCommand = new RelayCommand(() => IsHotkeysModalOpen = false);
        }

        public void AddItem() => AddItemInternal(allowOverride: false);

        public void AddItemInternal(bool allowOverride)
        {
            if (SelectedProduct == null)
            {
                SetError("Validation Error: Choose a product from the dropdown list. Typed text alone does not select a product.");
                return;
            }

            if (Quantity <= 0 || UnitPrice < 0 || DiscountPercentage is < 0 or > 100)
            {
                SetError("Quantity must be above zero. Price must be zero or higher, and discount must be between 0 and 100%.");
                return;
            }

            if (!allowOverride && Quantity > SelectedProduct.CurrentStock)
            {
                PendingOverSellProduct = SelectedProduct;
                RequestedOverSellQuantity = Quantity;
                IsOverSellWarningOpen = true;
                OverSellWarningMessage = SelectedProduct.CurrentStock <= 0
                    ? $"Product '{SelectedProduct.ProductName}' is completely OUT OF STOCK (0 units available)."
                    : $"You requested {Quantity:N0} units of '{SelectedProduct.ProductName}', but only {SelectedProduct.CurrentStock:N0} units are currently available.";
                SetError(OverSellWarningMessage);
                return;
            }

            var gross = Quantity * UnitPrice;
            var discount = gross * (DiscountPercentage / 100m);
            var lineTotal = gross - discount;

            var item = new SalesLineItemViewModel
            {
                Product = SelectedProduct,
                Quantity = Quantity,
                UnitPrice = UnitPrice,
                CostPrice = CostPrice > 0 ? CostPrice : SelectedProduct.BasePurchasePrice,
                SellingPercentage = SellingPercentage,
                DiscountPercentage = DiscountPercentage,
                LineTotal = lineTotal
            };

            item.LineTotalChanged += RecalculateTotals;
            Items.Add(item);

            RecalculateTotals();
            ClearErrors();
            Announce($"{SelectedProduct.ProductName} added to sale.");

            SelectedProduct = null;
            Quantity = 1m;
            UnitPrice = 0m;
            CostPrice = 0m;
            SellingPercentage = 0m;
            DiscountPercentage = 0m;
            IsOverSellWarningOpen = false;
        }

        private void AdjustQuantityToAvailable()
        {
            if (PendingOverSellProduct == null) return;
            var available = Math.Max(0, PendingOverSellProduct.CurrentStock);
            IsOverSellWarningOpen = false;
            if (available > 0)
            {
                Quantity = available;
                AddItemInternal(allowOverride: false);
            }
            else
            {
                SetError($"Cannot adjust quantity: '{PendingOverSellProduct.ProductName}' has 0 available stock.");
            }
        }

        public async Task SaveCreditBillAsync()
        {
            if (SelectedCustomer == null)
            {
                SetError("Validation Error: Please select a customer before saving a credit bill.");
                return;
            }

            PaidAmount = 0m;
            RecalculateTotals();

            var newTotalDebt = SelectedCustomer.OutstandingBalance + NetTotal;
            if (SelectedCustomer.CreditLimit > 0m && newTotalDebt > SelectedCustomer.CreditLimit)
            {
                var exceededBy = newTotalDebt - SelectedCustomer.CreditLimit;
                CreditLimitWarningMessage = $"Warning: Customer '{SelectedCustomer.CustomerName}' has exceeded their credit limit by PKR {exceededBy:N2}. Do you want to proceed?";
                IsCreditLimitWarningOpen = true;
                SetError(CreditLimitWarningMessage);
                return;
            }

            await SaveSaleAsync();
        }

        public void ClearBill()
        {
            foreach (var item in Items) item.LineTotalChanged -= RecalculateTotals;
            Items.Clear();
            SelectedProduct = null;
            SelectedCustomer = null;
            PaidAmount = 0m;
            PaymentSource = "Cash";
            Remarks = null;
            IsOverSellWarningOpen = false;
            IsCreditLimitWarningOpen = false;
            IsHotkeysModalOpen = false;
            RecalculateTotals();
            ClearErrors();
            Announce("Started a new sales invoice. Ready for entry.");
        }

        public void RemoveItem(SalesLineItemViewModel? item)
        {
            if (item != null && Items.Contains(item))
            {
                item.LineTotalChanged -= RecalculateTotals;
                Items.Remove(item);
                RecalculateTotals();
                Announce($"{item.ProductName} removed from sale.");
            }
        }

        public void RecalculateTotals()
        {
            decimal subTotal = 0m;
            decimal discountTotal = 0m;
            foreach (var item in Items)
            {
                var lineGross = item.Quantity * item.UnitPrice;
                var lineDiscount = lineGross * (item.DiscountPercentage / 100m);
                subTotal += lineGross;
                discountTotal += lineDiscount;
            }

            SubTotal = subTotal;
            DiscountAmount = discountTotal;
            NetTotal = SubTotal - DiscountAmount;
            RemainingBalance = Math.Max(0m, NetTotal - PaidAmount);
        }

        public async Task SaveSaleAsync()
        {
            if (SelectedCustomer == null || Items.Count == 0)
            {
                SetError("Validation Error: Please select a customer and add line items before saving.");
                return;
            }
            if (PaidAmount < 0 || PaidAmount > NetTotal)
            {
                SetError("Amount received must be between zero and the invoice total.");
                return;
            }
            if (Items.Any(item => item.Quantity <= 0 || item.UnitPrice < 0 || item.DiscountPercentage is < 0 or > 100))
            {
                SetError("Review each row: quantity must be above zero, price cannot be negative, and discount must be between 0 and 100%.");
                return;
            }
            var outOfStock = Items.FirstOrDefault(item => item.Product == null || item.Quantity > item.Product.CurrentStock);
            if (outOfStock != null)
            {
                SetError($"There is not enough stock for {outOfStock.ProductName}. Check current stock before saving.");
                return;
            }

            try
            {
                IsBusy = true;
                ClearErrors();

                var sale = new Sale
                {
                    InvoiceNumber = $"INV-{DateTime.Now:yyyyMMddHHmmssfff}",
                    CustomerId = SelectedCustomer.CustomerId,
                    SaleDate = DateTime.Now,
                    SubTotal = SubTotal > 0 ? SubTotal : Items.Sum(i => i.Quantity * i.UnitPrice),
                    DiscountAmount = DiscountAmount,
                    NetTotal = NetTotal > 0 ? NetTotal : SubTotal - DiscountAmount,
                    PaidAmount = PaidAmount,
                    RemainingBalance = RemainingBalance,
                    PaymentType = PaidAmount <= 0 ? "Credit" : PaidAmount < NetTotal ? "Split" : "Cash",
                    PaymentSource = PaymentSource,
                    Status = "Completed",
                    AdminId = _adminSession?.AdminId ?? 1,
                    Remarks = Remarks,
                    CreatedAt = DateTime.Now
                };

                foreach (var item in Items)
                {
                    sale.Items.Add(new SaleItem
                    {
                        ProductId = item.ProductId,
                        ProductCodeAtSale = item.Product?.ProductCode ?? string.Empty,
                        ProductNameAtSale = item.Product?.ProductName ?? item.ProductName,
                        Quantity = item.Quantity,
                        HistoricalSalePrice = item.UnitPrice,
                        HistoricalCostPrice = item.CostPrice,
                        SellingPercentage = item.SellingPercentage,
                        DiscountPercentage = item.DiscountPercentage,
                        LineTotal = item.LineTotal
                    });
                }

                await _transactionCoordinator.PostSaleAsync(sale);
                LastSavedDocumentNumber = sale.InvoiceNumber;
                DocumentLookupNumber = sale.InvoiceNumber;
                Announce($"Invoice {sale.InvoiceNumber} saved. Stock, customer balance, and price memory were updated.");
                foreach (var item in Items) item.LineTotalChanged -= RecalculateTotals;
                Items.Clear();
                SelectedCustomer = null;
                PaidAmount = 0m;
                PaymentSource = "Cash";
                RecalculateTotals();
            }
            catch (Exception ex)
            {
                SetError(ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task<TransactionDocumentData> LoadDocumentForPreviewAsync(string invoiceNumber)
        {
            if (_salesRepository == null) throw new InvalidOperationException("Saved invoice lookup is not available.");
            if (string.IsNullOrWhiteSpace(invoiceNumber)) throw new InvalidOperationException("Enter a saved invoice number.");
            var sale = await _salesRepository.GetByInvoiceNumberAsync(invoiceNumber.Trim())
                ?? throw new InvalidOperationException($"Invoice {invoiceNumber.Trim()} was not found.");
            var customer = await _customerRepository.GetByIdAsync(sale.CustomerId);
            if (customer == null) throw new InvalidOperationException("The customer for this invoice could not be found.");
            var lines = new List<TransactionDocumentLine>();
            foreach (var item in sale.Items)
            {
                var product = await _productRepository.GetByIdAsync(item.ProductId);
                lines.Add(new TransactionDocumentLine
                {
                    ProductCode = string.IsNullOrWhiteSpace(item.ProductCodeAtSale) ? product?.ProductCode ?? item.ProductId.ToString() : item.ProductCodeAtSale,
                    ProductName = string.IsNullOrWhiteSpace(item.ProductNameAtSale) ? product?.ProductName ?? "Product no longer available" : item.ProductNameAtSale,
                    Quantity = item.Quantity,
                    UnitPrice = item.HistoricalSalePrice,
                    DiscountPercentage = item.DiscountPercentage,
                    LineTotal = item.LineTotal
                });
            }
            return new TransactionDocumentData
            {
                Title = "Cash Memo", Number = sale.InvoiceNumber, Date = sale.SaleDate,
                PartyLabel = "Customer", PartyName = customer.CustomerName, PartyCode = customer.CustomerCode, AreaTerritory = customer.AreaTerritory, ContactPerson = customer.ContactPerson, Phone = customer.Phone, Address = customer.Address,
                Lines = lines, SubTotal = sale.SubTotal, Discount = sale.DiscountAmount, Total = sale.NetTotal,
                Paid = sale.PaidAmount, Balance = sale.RemainingBalance, PaymentSource = sale.PaymentSource, Remarks = sale.Remarks
            };
        }

        public async Task<IReadOnlyList<TransactionDocumentData>> LoadRecentInvoiceSheetAsync()
        {
            if (SavedInvoices.Count == 0) await LoadSavedInvoiceHistoryAsync();
            var numbers = SavedInvoices.OrderByDescending(item => item.Date).Take(4).Select(item => item.Number).ToArray();
            if (numbers.Length == 0) throw new InvalidOperationException("There are no saved invoices to print.");
            var documents = new List<TransactionDocumentData>(numbers.Length);
            foreach (var number in numbers) documents.Add(await LoadDocumentForPreviewAsync(number));
            return documents;
        }

        public async Task LoadInitialDataAsync()
        {
            try
            {
                IsBusy = true;
                ClearErrors();

                Customers.Clear();
                var customers = await _customerRepository.GetAllAsync();
                foreach (var c in customers.Where(c => c.Status == "Active"))
                {
                    Customers.Add(c);
                }

                Products.Clear();
                var products = await _productRepository.GetAllAsync();
                foreach (var p in products.Where(p => p.Status == "Active"))
                {
                    Products.Add(p);
                }

                await LoadSavedInvoiceHistoryAsync();

                if (Customers.Count == 0 || Products.Count == 0)
                {
                    var missing = new List<string>();
                    if (Customers.Count == 0) missing.Add("a customer");
                    if (Products.Count == 0) missing.Add("a product");
                    Announce($"To start a sale, add {string.Join(" and ", missing)} in Master Data.");
                }
                else Announce("Customers and products are ready. Choose a customer to begin.");
            }
            catch (Exception ex)
            {
                SetError(ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadSavedInvoiceHistoryAsync()
        {
            SavedInvoices.Clear();
            if (_salesRepository == null) return;
            var entries = new List<(Sale Sale, string Party)>();
            foreach (var customer in await _customerRepository.GetAllAsync())
                foreach (var sale in await _salesRepository.GetByCustomerIdAsync(customer.CustomerId))
                    if (!string.Equals(sale.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)) entries.Add((sale, customer.CustomerName));
            foreach (var entry in entries.OrderByDescending(x => x.Sale.SaleDate))
                SavedInvoices.Add(new TransactionDocumentHistoryItem
                {
                    Number = entry.Sale.InvoiceNumber,
                    Date = entry.Sale.SaleDate,
                    Total = entry.Sale.NetTotal,
                    DisplayText = $"{entry.Sale.InvoiceNumber}  ·  {entry.Sale.SaleDate:d}  ·  {entry.Party}  ·  {entry.Sale.NetTotal:N2}"
                });
        }

        private void OnProductSelected(Product? product)
        {
            if (product == null)
            {
                UnitPrice = 0m;
                CostPrice = 0m;
                SellingPercentage = 0m;
                UseDefaultPriceCommand?.RaiseCanExecuteChanged();
                return;
            }

            CostPrice = product.BasePurchasePrice;
            UnitPrice = product.DefaultSalePrice;
            SellingPercentage = product.BasePurchasePrice > 0
                ? ((product.DefaultSalePrice - product.BasePurchasePrice) / product.BasePurchasePrice) * 100m
                : 0m;
            UseDefaultPriceCommand?.RaiseCanExecuteChanged();
            _ = LoadPairPricingAsync();
        }

        private async Task LoadPairPricingAsync()
        {
            var version = ++_pricingLookupVersion;
            var customer = SelectedCustomer;
            var product = SelectedProduct;
            _latestCustomerPrice = null;
            UsePreviousPriceCommand?.RaiseCanExecuteChanged();
            if (product == null) return;
            UnitPrice = product.DefaultSalePrice;
            CostPrice = product.BasePurchasePrice;
            SellingPercentage = product.BasePurchasePrice > 0
                ? ((product.DefaultSalePrice - product.BasePurchasePrice) / product.BasePurchasePrice) * 100m
                : 0m;
            DiscountPercentage = 0m;
            if (customer == null)
            {
                PricingMessage = "Select a customer to look up their previous rate for this product.";
                return;
            }

            try
            {
                var previous = await _pricingHistoryRepository.GetLatestAsync(customer.CustomerId, product.ProductId);
                if (version != _pricingLookupVersion || customer != SelectedCustomer || product != SelectedProduct) return;
                if (previous == null)
                {
                    PricingMessage = "No earlier sale found for this pair. The product default price is shown.";
                    return;
                }
                _latestCustomerPrice = previous;
                UnitPrice = previous.SalePrice;
                SellingPercentage = previous.SellingPercentage;
                DiscountPercentage = previous.DiscountPercentage;
                PricingMessage = $"Last price {previous.SalePrice:N2} · markup {previous.SellingPercentage:N2}% · discount {previous.DiscountPercentage:N2}% · {previous.TransactionDate:d} · {previous.InvoiceNumber} · {previous.Quantity:N2} units";
                UsePreviousPriceCommand?.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                PricingMessage = "Could not retrieve pricing history. You can enter the rate manually.";
                SetError(ex.Message);
            }
        }

        private void ApplyCustomerPrice(bool usePrevious)
        {
            if (SelectedProduct == null) return;
            // Invalidate any still-running pair lookup so an explicit operator choice cannot be overwritten.
            _pricingLookupVersion++;
            if (usePrevious && _latestCustomerPrice != null)
            {
                UnitPrice = _latestCustomerPrice.SalePrice;
                SellingPercentage = _latestCustomerPrice.SellingPercentage;
                DiscountPercentage = _latestCustomerPrice.DiscountPercentage;
                return;
            }
            CostPrice = SelectedProduct.BasePurchasePrice;
            UnitPrice = SelectedProduct.DefaultSalePrice;
            SellingPercentage = SelectedProduct.BasePurchasePrice > 0
                ? ((SelectedProduct.DefaultSalePrice - SelectedProduct.BasePurchasePrice) / SelectedProduct.BasePurchasePrice) * 100m
                : 0m;
            DiscountPercentage = 0m;
            PricingMessage = "Current product default price selected; previous discount cleared.";
        }
    }
}
