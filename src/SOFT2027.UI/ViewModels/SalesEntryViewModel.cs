using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using SOFT2027.Core.Entities;
using SOFT2027.Core.Interfaces;
using SOFT2027.Presentation.Commands;

namespace SOFT2027.Presentation.ViewModels
{
    public class SalesEntryViewModel : ViewModelBase
    {
        private readonly ITransactionCoordinator _transactionCoordinator;
        private readonly ICustomerRepository _customerRepository;
        private readonly IProductRepository _productRepository;
        private readonly ICustomerPricingHistoryRepository _pricingHistoryRepository;
        private readonly IAdminSession? _adminSession;

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

        public ObservableCollection<Customer> Customers { get; } = new ObservableCollection<Customer>();
        public ObservableCollection<Product> Products { get; } = new ObservableCollection<Product>();
        public ObservableCollection<SalesLineItemViewModel> Items { get; } = new ObservableCollection<SalesLineItemViewModel>();

        public Customer? SelectedCustomer
        {
            get => _selectedCustomer;
            set
            {
                if (SetProperty(ref _selectedCustomer, value)) _ = LoadPairPricingAsync();
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
            set => SetProperty(ref _unitPrice, value);
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

        public RelayCommand AddItemCommand { get; }
        public RelayCommand RemoveItemCommand { get; }
        public AsyncRelayCommand SaveSaleCommand { get; }
        public AsyncRelayCommand LoadInitialDataCommand { get; }
        public RelayCommand UsePreviousPriceCommand { get; }
        public RelayCommand UseDefaultPriceCommand { get; }
        public RelayCommand EnterNewPriceCommand { get; }

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
        {
            _transactionCoordinator = transactionCoordinator ?? throw new ArgumentNullException(nameof(transactionCoordinator));
            _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
            _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
            _pricingHistoryRepository = pricingHistoryRepository ?? throw new ArgumentNullException(nameof(pricingHistoryRepository));
            _adminSession = adminSession;

            AddItemCommand = new RelayCommand(AddItem);
            RemoveItemCommand = new RelayCommand(param => RemoveItem(param as SalesLineItemViewModel));
            SaveSaleCommand = new AsyncRelayCommand(SaveSaleAsync);
            LoadInitialDataCommand = new AsyncRelayCommand(LoadInitialDataAsync);
            UsePreviousPriceCommand = new RelayCommand(() => ApplyCustomerPrice(usePrevious: true), () => _latestCustomerPrice != null);
            UseDefaultPriceCommand = new RelayCommand(() => ApplyCustomerPrice(usePrevious: false), () => SelectedProduct != null);
            EnterNewPriceCommand = new RelayCommand(() => Announce("Enter the negotiated rate in Unit Price. It will be remembered when this invoice is saved."));
        }

        public void AddItem()
        {
            if (SelectedProduct == null)
            {
                SetError("Validation Error: Please select a product.");
                return;
            }

            if (Quantity <= 0 || UnitPrice < 0 || DiscountPercentage is < 0 or > 100)
            {
                SetError("Quantity must be above zero. Price must be zero or higher, and discount must be between 0 and 100%.");
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
                        Quantity = item.Quantity,
                        HistoricalSalePrice = item.UnitPrice,
                        HistoricalCostPrice = item.CostPrice,
                        SellingPercentage = item.SellingPercentage,
                        DiscountPercentage = item.DiscountPercentage,
                        LineTotal = item.LineTotal
                    });
                }

                await _transactionCoordinator.PostSaleAsync(sale);
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
                PricingMessage = $"Last price {previous.SalePrice:N2} on {previous.TransactionDate:d} · {previous.InvoiceNumber} · {previous.Quantity:N2} units";
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
            if (usePrevious && _latestCustomerPrice != null)
            {
                UnitPrice = _latestCustomerPrice.SalePrice;
                SellingPercentage = _latestCustomerPrice.SellingPercentage;
                DiscountPercentage = _latestCustomerPrice.DiscountPercentage;
                return;
            }
            OnProductSelected(SelectedProduct);
            DiscountPercentage = 0m;
        }
    }
}
