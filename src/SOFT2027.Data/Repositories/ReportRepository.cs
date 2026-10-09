using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using SOFT2027.Core.Entities;
using SOFT2027.Core.Interfaces;

namespace SOFT2027.Data.Repositories
{
    public class ReportRepository : IReportRepository
    {
        private readonly ISqliteConnectionFactory _connectionFactory;

        public ReportRepository(ISqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public async Task<CustomerStatement> GetCustomerStatementAsync(int customerId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            const string customerSql = @"
                SELECT CustomerId, CustomerCode, CustomerName 
                FROM Customers 
                WHERE CustomerId = @CustomerId;";

            var customer = await connection.QuerySingleOrDefaultAsync<CustomerHeaderDto>(
                new CommandDefinition(customerSql, new { CustomerId = customerId }, cancellationToken: cancellationToken)
            ).ConfigureAwait(false);

            if (customer == null)
            {
                throw new KeyNotFoundException($"Customer with ID {customerId} was not found.");
            }

            const string openingBalanceSql = @"
                SELECT COALESCE(
                    (SELECT RunningBalance FROM CustomerLedger WHERE CustomerId = @CustomerId AND TransactionDate < @StartDate ORDER BY TransactionDate DESC, LedgerId DESC LIMIT 1),
                    (SELECT OpeningBalance FROM Customers WHERE CustomerId = @CustomerId),
                    0.0
                );";

            var openingBalance = await connection.ExecuteScalarAsync<decimal>(
                new CommandDefinition(openingBalanceSql, new { CustomerId = customerId, StartDate = startDate }, cancellationToken: cancellationToken)
            ).ConfigureAwait(false);

            var queryEndDate = endDate.TimeOfDay == TimeSpan.Zero
                ? endDate.Date.AddHours(23).AddMinutes(59).AddSeconds(59)
                : endDate;

            const string totalsSql = @"
                SELECT 
                    COALESCE(SUM(Debit), 0.0) AS TotalDebits, 
                    COALESCE(SUM(Credit), 0.0) AS TotalCredits 
                FROM CustomerLedger 
                WHERE CustomerId = @CustomerId AND TransactionDate >= @StartDate AND TransactionDate <= @EndDate;";

            var totals = await connection.QuerySingleAsync<StatementTotalsDto>(
                new CommandDefinition(totalsSql, new { CustomerId = customerId, StartDate = startDate, EndDate = queryEndDate }, cancellationToken: cancellationToken)
            ).ConfigureAwait(false);

            const string itemsSql = @"
                SELECT TransactionDate, TransactionType, ReferenceNumber, Description, Debit, Credit, RunningBalance 
                FROM CustomerLedger 
                WHERE CustomerId = @CustomerId AND TransactionDate >= @StartDate AND TransactionDate <= @EndDate 
                ORDER BY TransactionDate ASC, LedgerId ASC;";

            var items = await connection.QueryAsync<CustomerStatementItem>(
                new CommandDefinition(itemsSql, new { CustomerId = customerId, StartDate = startDate, EndDate = queryEndDate }, cancellationToken: cancellationToken)
            ).ConfigureAwait(false);

            var closingBalance = openingBalance + totals.TotalDebits - totals.TotalCredits;

            return new CustomerStatement
            {
                CustomerId = customer.CustomerId,
                CustomerCode = customer.CustomerCode,
                CustomerName = customer.CustomerName,
                StartDate = startDate,
                EndDate = endDate,
                OpeningBalance = openingBalance,
                TotalDebits = totals.TotalDebits,
                TotalCredits = totals.TotalCredits,
                ClosingBalance = closingBalance,
                Transactions = items.AsList()
            };
        }

        public async Task<ProfitAndLossSummary> GetProfitAndLossSummaryAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            var queryEndDate = endDate.TimeOfDay == TimeSpan.Zero
                ? endDate.Date.AddHours(23).AddMinutes(59).AddSeconds(59)
                : endDate;

            // 1. Net revenue: completed invoices less returns posted in the period.
            const string revenueSql = @"
                SELECT COALESCE((SELECT SUM(NetTotal) FROM Sales WHERE SaleDate >= @StartDate AND SaleDate <= @EndDate AND Status = 'Completed'), 0.0)
                     - COALESCE((SELECT SUM(TotalRefundAmount) FROM SalesReturns WHERE ReturnDate >= @StartDate AND ReturnDate <= @EndDate), 0.0);";

            var revenue = await connection.ExecuteScalarAsync<decimal>(
                new CommandDefinition(revenueSql, new { StartDate = startDate, EndDate = queryEndDate }, cancellationToken: cancellationToken)
            ).ConfigureAwait(false);

            // 2. Net COGS uses immutable sale cost snapshots less returned sale-item cost.
            const string cogsSql = @"
                SELECT COALESCE((SELECT SUM(si.HistoricalCostPrice * si.Quantity)
                    FROM SaleItems si INNER JOIN Sales s ON si.SaleId = s.SaleId
                    WHERE s.SaleDate >= @StartDate AND s.SaleDate <= @EndDate AND s.Status = 'Completed'), 0.0)
                     - COALESCE((SELECT SUM(ri.Quantity * COALESCE((
                        SELECT SUM(original.HistoricalCostPrice * original.Quantity) / SUM(original.Quantity)
                        FROM SaleItems original WHERE original.SaleId = r.OriginalSaleId AND original.ProductId = ri.ProductId
                     ), 0.0))
                        FROM SalesReturnItems ri INNER JOIN SalesReturns r ON r.ReturnId = ri.ReturnId
                        WHERE r.ReturnDate >= @StartDate AND r.ReturnDate <= @EndDate), 0.0);";

            var cogs = await connection.ExecuteScalarAsync<decimal>(
                new CommandDefinition(cogsSql, new { StartDate = startDate, EndDate = queryEndDate }, cancellationToken: cancellationToken)
            ).ConfigureAwait(false);

            // 3. Total Expenses within date range
            const string expensesSql = @"
                SELECT COALESCE(SUM(Amount), 0.0) 
                FROM Expenses 
                WHERE ExpenseDate >= @StartDate AND ExpenseDate <= @EndDate;";

            var totalExpenses = await connection.ExecuteScalarAsync<decimal>(
                new CommandDefinition(expensesSql, new { StartDate = startDate, EndDate = queryEndDate }, cancellationToken: cancellationToken)
            ).ConfigureAwait(false);

            // 4. Expenses categorized and aggregated in SQLite
            const string categoryExpensesSql = @"
                SELECT ExpenseCategory AS CategoryName, SUM(Amount) AS TotalAmount 
                FROM Expenses 
                WHERE ExpenseDate >= @StartDate AND ExpenseDate <= @EndDate 
                GROUP BY ExpenseCategory 
                ORDER BY TotalAmount DESC;";

            var expenseCategories = await connection.QueryAsync<ExpenseCategorySummary>(
                new CommandDefinition(categoryExpensesSql, new { StartDate = startDate, EndDate = queryEndDate }, cancellationToken: cancellationToken)
            ).ConfigureAwait(false);

            var grossProfit = revenue - cogs;
            var netProfit = grossProfit - totalExpenses;

            return new ProfitAndLossSummary
            {
                StartDate = startDate,
                EndDate = endDate,
                TotalRevenue = revenue,
                CostOfGoodsSold = cogs,
                GrossProfit = grossProfit,
                TotalExpenses = totalExpenses,
                NetProfit = netProfit,
                ExpensesByCategory = expenseCategories.AsList()
            };
        }

        public Task<StockValuation> GetStockValuationAsync(CancellationToken cancellationToken = default)
        {
            return GetStockValuationAsync(DateTime.UtcNow, cancellationToken);
        }

        public async Task<StockValuation> GetStockValuationAsync(DateTime asOfDate, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            const string totalsSql = @"
                SELECT 
                    COALESCE(SUM(CurrentStock * BasePurchasePrice), 0.0) AS TotalValuation,
                    COALESCE(SUM(CurrentStock * DefaultSalePrice), 0.0) AS TotalPotentialRetailValue,
                    COUNT(*) AS TotalItemsCount
                FROM Products
                WHERE Status = 'Active';";

            var totals = await connection.QuerySingleAsync<StockValuationTotalsDto>(
                new CommandDefinition(totalsSql, cancellationToken: cancellationToken)
            ).ConfigureAwait(false);

            const string itemsSql = @"
                SELECT 
                    ProductId, 
                    ProductCode, 
                    ProductName, 
                    CurrentStock, 
                    BasePurchasePrice AS UnitCost, 
                    (CurrentStock * BasePurchasePrice) AS TotalValuation, 
                    DefaultSalePrice, 
                    (CurrentStock * DefaultSalePrice) AS PotentialRetailValue 
                FROM Products 
                WHERE Status = 'Active' 
                ORDER BY ProductName ASC;";

            var items = await connection.QueryAsync<StockValuationItem>(
                new CommandDefinition(itemsSql, cancellationToken: cancellationToken)
            ).ConfigureAwait(false);

            return new StockValuation
            {
                ValuationDate = asOfDate,
                TotalValuation = totals.TotalValuation,
                TotalPotentialRetailValue = totals.TotalPotentialRetailValue,
                TotalItemsCount = totals.TotalItemsCount,
                Items = items.AsList()
            };
        }

        private sealed record CustomerHeaderDto
        {
            public int CustomerId { get; init; }
            public string CustomerCode { get; init; } = string.Empty;
            public string CustomerName { get; init; } = string.Empty;
        }

        private sealed record StatementTotalsDto
        {
            public decimal TotalDebits { get; init; }
            public decimal TotalCredits { get; init; }
        }

        private sealed record StockValuationTotalsDto
        {
            public decimal TotalValuation { get; init; }
            public decimal TotalPotentialRetailValue { get; init; }
            public int TotalItemsCount { get; init; }
        }
    }
}
