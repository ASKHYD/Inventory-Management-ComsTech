using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Globalization;
using SOFT2027.Core.Entities;
using SOFT2027.Core.Interfaces;
using SOFT2027.Presentation.Commands;

namespace SOFT2027.Presentation.ViewModels
{
    public class ReportsDashboardViewModel : ViewModelBase
    {
        private readonly IReportRepository _reportRepository;
        private readonly ICustomerRepository? _customers;
        private readonly ISupplierRepository? _suppliers;
        private readonly IProductRepository? _products;
        private readonly IBusinessLookupRepository? _lookups;

        private DateTime _startDate = DateTime.Today.AddDays(-30);
        private DateTime _endDate = DateTime.Today;
        private int? _selectedCustomerId;
        private ProfitAndLossSummary? _profitAndLoss;
        private StockValuation? _stockValuation;
        private CustomerStatement? _customerStatement;
        private bool _isLoading;
        private OperationalReport? _operationalReport;
        private ReportChoice? _selectedReport;
        private Customer? _filterCustomer;
        private Supplier? _filterSupplier;
        private Product? _filterProduct;
        private LookupItem? _filterCategory;
        private string? _filterExpenseCategory;

        public ObservableCollection<ReportChoice> ReportChoices { get; } = new();
        public ObservableCollection<Customer> Customers { get; } = new();
        public ObservableCollection<Supplier> Suppliers { get; } = new();
        public ObservableCollection<Product> Products { get; } = new();
        public ObservableCollection<LookupItem> Categories { get; } = new();
        public ObservableCollection<string> ExpenseCategories { get; } = new();
        public ObservableCollection<DashboardActivityItem> DashboardRecentActivity { get; } = new();
        public ObservableCollection<string> DashboardTrends { get; } = new();
        public bool HasDashboardTrends => DashboardTrends.Count > 0;
        public decimal PeriodSales { get; private set; }
        public decimal PeriodPurchases { get; private set; }
        public decimal TodaySales { get; private set; }
        public decimal TodayPurchases { get; private set; }
        public decimal OutstandingReceivables { get; private set; }
        public decimal OutstandingPayables { get; private set; }
        public int LowStockItemCount { get; private set; }
        public OperationalReport? OperationalReport { get => _operationalReport; set => SetProperty(ref _operationalReport, value); }
        public ReportChoice? SelectedReport { get => _selectedReport; set => SetProperty(ref _selectedReport, value); }
        public Customer? FilterCustomer { get => _filterCustomer; set => SetProperty(ref _filterCustomer, value); }
        public Supplier? FilterSupplier { get => _filterSupplier; set => SetProperty(ref _filterSupplier, value); }
        public Product? FilterProduct { get => _filterProduct; set => SetProperty(ref _filterProduct, value); }
        public LookupItem? FilterCategory { get => _filterCategory; set => SetProperty(ref _filterCategory, value); }
        public string? FilterExpenseCategory { get => _filterExpenseCategory; set => SetProperty(ref _filterExpenseCategory, value); }

        public DateTime StartDate
        {
            get => _startDate;
            set => SetProperty(ref _startDate, value);
        }

        public DateTime EndDate
        {
            get => _endDate;
            set => SetProperty(ref _endDate, value);
        }

        public int? SelectedCustomerId
        {
            get => _selectedCustomerId;
            set => SetProperty(ref _selectedCustomerId, value);
        }

        public ProfitAndLossSummary? ProfitAndLoss
        {
            get => _profitAndLoss;
            set => SetProperty(ref _profitAndLoss, value);
        }

        public StockValuation? StockValuation
        {
            get => _stockValuation;
            set => SetProperty(ref _stockValuation, value);
        }

        public CustomerStatement? CustomerStatement
        {
            get => _customerStatement;
            set => SetProperty(ref _customerStatement, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public AsyncRelayCommand LoadReportsCommand { get; }
        public AsyncRelayCommand LoadCustomerStatementCommand { get; }
        public AsyncRelayCommand GenerateOperationalReportCommand { get; }

        public ReportsDashboardViewModel(IReportRepository reportRepository)
            : this(reportRepository, null, null, null, null) { }

        public ReportsDashboardViewModel(IReportRepository reportRepository, ICustomerRepository? customers, ISupplierRepository? suppliers, IProductRepository? products, IBusinessLookupRepository? lookups)
        {
            _reportRepository = reportRepository ?? throw new ArgumentNullException(nameof(reportRepository));
            _customers = customers; _suppliers = suppliers; _products = products; _lookups = lookups;
            foreach (var choice in ReportChoice.All) ReportChoices.Add(choice);
            SelectedReport = ReportChoices[0];

            LoadReportsCommand = new AsyncRelayCommand(LoadReportsAsync);
            LoadCustomerStatementCommand = new AsyncRelayCommand(LoadCustomerStatementAsync);
            GenerateOperationalReportCommand = new AsyncRelayCommand(GenerateOperationalReportAsync);
        }

        public async Task InitializeReportFiltersAsync()
        {
            if (_customers != null) { Customers.Clear(); foreach (var item in await _customers.GetAllAsync()) Customers.Add(item); }
            if (_suppliers != null) { Suppliers.Clear(); foreach (var item in await _suppliers.GetAllAsync()) Suppliers.Add(item); }
            if (_products != null) { Products.Clear(); foreach (var item in await _products.GetAllAsync()) Products.Add(item); }
            if (_lookups != null)
            {
                Categories.Clear(); foreach (var item in await _lookups.GetAllAsync("Categories")) Categories.Add(item);
                ExpenseCategories.Clear();
            }
            ExpenseCategories.Clear(); foreach (var item in await _reportRepository.GetExpenseCategoriesAsync()) ExpenseCategories.Add(item);
        }

        public async Task GenerateOperationalReportAsync()
        {
            if (SelectedReport == null) { SetError("Choose a report."); return; }
            try
            {
                IsLoading = true; ClearErrors();
                OperationalReport = await _reportRepository.GetOperationalReportAsync(new OperationalReportRequest
                {
                    ReportKey = SelectedReport.Key, StartDate = StartDate, EndDate = EndDate,
                    CustomerId = FilterCustomer?.CustomerId, SupplierId = FilterSupplier?.SupplierId,
                    ProductId = FilterProduct?.ProductId, CategoryId = FilterCategory?.Id,
                    ExpenseCategory = FilterExpenseCategory
                });
                Announce($"{OperationalReport.Title}: {OperationalReport.Rows.Count} rows");
            }
            catch (Exception ex) { OperationalReport = null; SetError(ex.Message); }
            finally { IsLoading = false; }
        }

        public async Task LoadReportsAsync()
        {
            try
            {
                IsLoading = true;
                ClearErrors();

                ProfitAndLoss = await _reportRepository.GetProfitAndLossSummaryAsync(StartDate, EndDate);
                StockValuation = await _reportRepository.GetStockValuationAsync();
                await LoadDashboardSummaryAsync();

                Announce("Reports loaded successfully");
            }
            catch (Exception ex)
            {
                SetError(ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadDashboardSummaryAsync()
        {
            var today = DateTime.Today;
            var reports = await Task.WhenAll(
                QueryAsync("SalesByDate", StartDate, EndDate),
                QueryAsync("PurchasesByDate", StartDate, EndDate),
                QueryAsync("SalesByDate", today, today),
                QueryAsync("PurchasesByDate", today, today),
                QueryAsync("OutstandingReceivables", StartDate, EndDate),
                QueryAsync("OutstandingPayables", StartDate, EndDate),
                QueryAsync("LowStock", StartDate, EndDate),
                QueryAsync("ReceiptsPayments", StartDate, EndDate),
                QueryAsync("StockAdjustments", StartDate, EndDate));
            PeriodSales = Sum(reports[0], "NetTotal");
            PeriodPurchases = Sum(reports[1], "NetTotal");
            TodaySales = Sum(reports[2], "NetTotal");
            TodayPurchases = Sum(reports[3], "NetTotal");
            OutstandingReceivables = Sum(reports[4], "OutstandingBalance");
            OutstandingPayables = Sum(reports[5], "OutstandingBalance");
            LowStockItemCount = reports[6].Rows.Count;
            OnPropertyChanged(nameof(PeriodSales)); OnPropertyChanged(nameof(PeriodPurchases));
            OnPropertyChanged(nameof(TodaySales)); OnPropertyChanged(nameof(TodayPurchases));
            OnPropertyChanged(nameof(OutstandingReceivables)); OnPropertyChanged(nameof(OutstandingPayables));
            OnPropertyChanged(nameof(LowStockItemCount));

            var trends = new SortedDictionary<DateTime, (decimal Sales, decimal Purchases)>();
            AccumulateTrend(reports[0], "SaleDate", "NetTotal", isSales: true, trends);
            AccumulateTrend(reports[1], "PurchaseDate", "NetTotal", isSales: false, trends);
            DashboardTrends.Clear();
            foreach (var item in trends) DashboardTrends.Add($"{item.Key:d} · Sales PKR {item.Value.Sales:N2} · Purchases PKR {item.Value.Purchases:N2}");
            OnPropertyChanged(nameof(HasDashboardTrends));

            var activity = new List<(DateTime Date, DashboardActivityItem Item)>();
            CollectActivity(reports[0], "InvoiceNumber", "SaleDate", "CustomerName", "NetTotal", "Sale", "Sales", activity);
            CollectActivity(reports[1], "InvoiceNumber", "PurchaseDate", "SupplierName", "NetTotal", "Purchase", "Purchases", activity);
            CollectActivity(reports[7], "ReferenceNumber", "TransactionDate", "PartyName", "Amount", "TransactionType", "", activity);
            CollectStockActivity(reports[8], activity);
            DashboardRecentActivity.Clear();
            foreach (var item in activity.OrderByDescending(item => item.Date).Take(12)) DashboardRecentActivity.Add(item.Item);
            if (DashboardRecentActivity.Count == 0) DashboardRecentActivity.Add(new DashboardActivityItem("No sales, purchases, receipts, payments, or stock adjustments in this period.", ""));
        }

        private Task<OperationalReport> QueryAsync(string key, DateTime start, DateTime end) =>
            _reportRepository.GetOperationalReportAsync(new OperationalReportRequest { ReportKey = key, StartDate = start, EndDate = end });

        private static decimal Sum(OperationalReport report, string key) => report.Rows.Sum(row =>
        {
            if (!row.Values.TryGetValue(key, out var value) || value == null) return 0m;
            try { return Convert.ToDecimal(value, CultureInfo.InvariantCulture); }
            catch { return 0m; }
        });

        private static void AccumulateTrend(OperationalReport report, string dateKey, string amountKey, bool isSales, SortedDictionary<DateTime, (decimal Sales, decimal Purchases)> values)
        {
            foreach (var row in report.Rows)
            {
                if (!row.Values.TryGetValue(dateKey, out var dateValue) || dateValue == null || !DateTime.TryParse(Convert.ToString(dateValue, CultureInfo.CurrentCulture), CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out var date)) continue;
                var amount = row.Values.TryGetValue(amountKey, out var amountValue) && amountValue != null ? Convert.ToDecimal(amountValue, CultureInfo.InvariantCulture) : 0m;
                values.TryGetValue(date.Date, out var current);
                values[date.Date] = isSales ? (current.Sales + amount, current.Purchases) : (current.Sales, current.Purchases + amount);
            }
        }

        private static void CollectActivity(OperationalReport report, string reference, string date, string party, string amount, string label, string module, List<(DateTime Date, DashboardActivityItem Item)> output)
        {
            foreach (var row in report.Rows.Take(5))
            {
                var values = row.Values;
                string Get(string key) => values.TryGetValue(key, out var value) && value != null ? Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty : string.Empty;
                if (!DateTime.TryParse(Get(date), CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out var timestamp)) continue;
                var type = Get(label);
                var destination = module.Length > 0 ? module : type == "Receipt" ? "Receipts" : "Payments";
                output.Add((timestamp, new DashboardActivityItem($"{timestamp:g} · {type} {Get(reference)} · {Get(party)} · PKR {Get(amount)}", destination)));
            }
        }

        private static void CollectStockActivity(OperationalReport report, List<(DateTime Date, DashboardActivityItem Item)> output)
        {
            foreach (var row in report.Rows.Take(5))
            {
                var values = row.Values;
                string Get(string key) => values.TryGetValue(key, out var value) && value != null ? Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty : string.Empty;
                if (!DateTime.TryParse(Get("MovementDate"), CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out var timestamp)) continue;
                output.Add((timestamp, new DashboardActivityItem($"{timestamp:g} · Stock adjustment {Get("ReferenceNumber")} · {Get("ProductName")} · +{Get("QuantityIn")} / -{Get("QuantityOut")} · {Get("Remarks")}", "Inventory")));
            }
        }

        public async Task LoadCustomerStatementAsync()
        {
            if (!SelectedCustomerId.HasValue)
            {
                SetError("Validation Error: Please select a customer.");
                return;
            }

            try
            {
                IsLoading = true;
                ClearErrors();

                CustomerStatement = await _reportRepository.GetCustomerStatementAsync(SelectedCustomerId.Value, StartDate, EndDate);
                Announce("Customer statement loaded");
            }
            catch (Exception ex)
            {
                SetError(ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    public sealed record ReportChoice(string Key, string Name)
    {
        public override string ToString() => Name;
        public static IReadOnlyList<ReportChoice> All { get; } = new[]
        {
            new ReportChoice("SalesByDate", "Sales by date range"), new("SalesByInvoice", "Sales by invoice"), new("SalesByCustomer", "Sales by customer"), new("SalesByProduct", "Sales by product"), new("SalesReturns", "Sales returns"), new("SalesDiscounts", "Discounts and net sales"), new("SalesPaymentStatus", "Sales payment status"),
            new("PurchasesByDate", "Purchases by date range"), new("PurchasesByInvoice", "Purchases by bill"), new("PurchasesBySupplier", "Purchases by supplier"), new("PurchasesByProduct", "Purchases by product"), new("PurchaseReturns", "Purchase returns"), new("PurchaseDiscounts", "Purchase discounts"), new("PurchasesPaymentStatus", "Purchase payment status"),
            new("CurrentStock", "Current stock"), new("StockValuation", "Stock valuation"), new("StockMovements", "Stock movement history"), new("StockAdjustments", "Stock adjustments"), new("LowStock", "Low-stock items"), new("StockMovementByProduct", "Product-wise stock movement"),
            new("Customers", "Customer list and balances"), new("CustomerStatement", "Customer statements"), new("OutstandingReceivables", "Outstanding receivables"), new("CustomerSalesHistory", "Customer sales history"), new("CustomerPriceHistory", "Customer/product price history"), new("CustomerPriceChanges", "Customer/product price changes"),
            new("Suppliers", "Supplier list and balances"), new("SupplierStatement", "Supplier statements"), new("OutstandingPayables", "Outstanding payables"), new("SupplierPurchaseHistory", "Supplier purchase history"), new("SupplierPriceHistory", "Supplier/product price history"), new("SupplierPriceChanges", "Supplier/product price changes"),
            new("ProfitAndLoss", "Profit and loss"), new("ExpensesByDate", "Expenses by date range"), new("ExpensesByCategory", "Expenses by category"), new("CashActivity", "Cash account activity"), new("CashBalance", "Cash account balance"), new("BankActivity", "Bank account activity"), new("BankBalance", "Bank account balance"), new("ReceiptsPayments", "Receipts and payments"), new("ReceivablesPayables", "Outstanding receivables and payables"),
            new("ProductPriceList", "Product default price list"), new("ProductProfitability", "Product profitability")
        };
    }

    public sealed record DashboardActivityItem(string Text, string Module);
}
