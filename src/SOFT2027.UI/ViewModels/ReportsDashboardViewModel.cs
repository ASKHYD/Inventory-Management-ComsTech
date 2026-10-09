using System;
using System.Threading.Tasks;
using SOFT2027.Core.Entities;
using SOFT2027.Core.Interfaces;
using SOFT2027.Presentation.Commands;

namespace SOFT2027.Presentation.ViewModels
{
    public class ReportsDashboardViewModel : ViewModelBase
    {
        private readonly IReportRepository _reportRepository;

        private DateTime _startDate = DateTime.Today.AddDays(-30);
        private DateTime _endDate = DateTime.Today;
        private int? _selectedCustomerId;
        private ProfitAndLossSummary? _profitAndLoss;
        private StockValuation? _stockValuation;
        private CustomerStatement? _customerStatement;
        private bool _isLoading;

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

        public ReportsDashboardViewModel(IReportRepository reportRepository)
        {
            _reportRepository = reportRepository ?? throw new ArgumentNullException(nameof(reportRepository));

            LoadReportsCommand = new AsyncRelayCommand(LoadReportsAsync);
            LoadCustomerStatementCommand = new AsyncRelayCommand(LoadCustomerStatementAsync);
        }

        public async Task LoadReportsAsync()
        {
            try
            {
                IsLoading = true;
                ClearErrors();

                ProfitAndLoss = await _reportRepository.GetProfitAndLossSummaryAsync(StartDate, EndDate);
                StockValuation = await _reportRepository.GetStockValuationAsync();

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
}
