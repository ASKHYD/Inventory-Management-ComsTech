using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using SOFT2027.Core.Entities;

namespace SOFT2027.Core.Interfaces
{
    public interface IReportRepository
    {
        Task<CustomerStatement> GetCustomerStatementAsync(int customerId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<ProfitAndLossSummary> GetProfitAndLossSummaryAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<StockValuation> GetStockValuationAsync(CancellationToken cancellationToken = default);
        Task<StockValuation> GetStockValuationAsync(DateTime asOfDate, CancellationToken cancellationToken = default);
        Task<OperationalReport> GetOperationalReportAsync(OperationalReportRequest request, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<string>> GetExpenseCategoriesAsync(CancellationToken cancellationToken = default);
    }
}
