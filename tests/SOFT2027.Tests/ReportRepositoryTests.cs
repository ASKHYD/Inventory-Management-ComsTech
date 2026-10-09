using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using SOFT2027.Core.Entities;
using SOFT2027.Core.Interfaces;
using SOFT2027.Data.Repositories;
using SOFT2027.Data.Services;
using Xunit;

namespace SOFT2027.Tests
{
    public sealed class ReportRepositoryTests : IAsyncLifetime
    {
        private readonly string _testDbPath;
        private readonly ISqliteConnectionFactory _factory;
        private readonly IDatabaseInitializer _initializer;
        private readonly IReportRepository _sut;

        private int _testAdminId;

        public ReportRepositoryTests()
        {
            _testDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"test_rep_{Guid.NewGuid():N}.db");
            var schemaPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "docs", "DATABASE_SCHEMA.sql"));

            _factory = new SqliteConnectionFactory(_testDbPath);
            _initializer = new DatabaseInitializer(_factory, schemaPath);
            _sut = new ReportRepository(_factory);
        }

        public async Task InitializeAsync()
        {
            await _initializer.InitializeAsync();
            await SeedBaseDataAsync();
        }

        public Task DisposeAsync()
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(_testDbPath))
            {
                try
                {
                    File.Delete(_testDbPath);
                }
                catch
                {
                    // Ignore transient file lock on test teardown
                }
            }
            return Task.CompletedTask;
        }

        private async Task SeedBaseDataAsync()
        {
            using var connection = _factory.CreateConnection();
            await ((SqliteConnection)connection).OpenAsync();

            _testAdminId = await connection.ExecuteScalarAsync<int>(
                "INSERT INTO AdminUsers (Username, PasswordHash, FullName) VALUES ('admin_rep', 'hash', 'Admin Report'); SELECT last_insert_rowid();");
        }

        [Fact]
        public async Task GetCustomerStatementAsync_ShouldCalculateOpeningBalance_DebitsCredits_AndClosingBalanceCorrectly()
        {
            // Arrange
            using var connection = _factory.CreateConnection();
            await ((SqliteConnection)connection).OpenAsync();

            var customerId = await connection.ExecuteScalarAsync<int>(
                @"INSERT INTO Customers (CustomerCode, CustomerName, OpeningBalance, OutstandingBalance)
                  VALUES ('CUST-001', 'Alpha Supermarket', 500.0, 950.0);
                  SELECT last_insert_rowid();");

            // Prior to StartDate (2026-01-15):
            // 2026-01-05: Debit 1000.0, RunningBalance = 1500.0 (500 + 1000)
            await connection.ExecuteAsync(
                @"INSERT INTO CustomerLedger (CustomerId, TransactionDate, TransactionType, ReferenceNumber, Description, Debit, Credit, RunningBalance)
                  VALUES (@CustomerId, '2026-01-05 10:00:00', 'SALE', 'INV-001', 'Prior Sale', 1000.0, 0.0, 1500.0);",
                new { CustomerId = customerId });

            // 2026-01-10: Credit 700.0, RunningBalance = 800.0 (1500 - 700) -> Opening balance for statement starting 2026-01-15 is 800.0
            await connection.ExecuteAsync(
                @"INSERT INTO CustomerLedger (CustomerId, TransactionDate, TransactionType, ReferenceNumber, Description, Debit, Credit, RunningBalance)
                  VALUES (@CustomerId, '2026-01-10 14:00:00', 'RECEIPT', 'REC-001', 'Prior Receipt', 0.0, 700.0, 800.0);",
                new { CustomerId = customerId });

            // Within Statement Date Range (2026-01-15 to 2026-01-31):
            // 2026-01-20: Debit 600.0, RunningBalance = 1400.0
            await connection.ExecuteAsync(
                @"INSERT INTO CustomerLedger (CustomerId, TransactionDate, TransactionType, ReferenceNumber, Description, Debit, Credit, RunningBalance)
                  VALUES (@CustomerId, '2026-01-20 11:00:00', 'SALE', 'INV-002', 'Range Sale', 600.0, 0.0, 1400.0);",
                new { CustomerId = customerId });

            // 2026-01-25: Credit 450.0, RunningBalance = 950.0
            await connection.ExecuteAsync(
                @"INSERT INTO CustomerLedger (CustomerId, TransactionDate, TransactionType, ReferenceNumber, Description, Debit, Credit, RunningBalance)
                  VALUES (@CustomerId, '2026-01-25 15:30:00', 'RECEIPT', 'REC-002', 'Range Receipt', 0.0, 450.0, 950.0);",
                new { CustomerId = customerId });

            var startDate = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);
            var endDate = new DateTime(2026, 1, 31, 23, 59, 59, DateTimeKind.Utc);

            // Act
            var statement = await _sut.GetCustomerStatementAsync(customerId, startDate, endDate, CancellationToken.None);

            // Assert
            statement.Should().NotBeNull();
            statement.CustomerId.Should().Be(customerId);
            statement.CustomerCode.Should().Be("CUST-001");
            statement.CustomerName.Should().Be("Alpha Supermarket");
            statement.StartDate.Should().Be(startDate);
            statement.EndDate.Should().Be(endDate);
            statement.OpeningBalance.Should().Be(800.00m);
            statement.TotalDebits.Should().Be(600.00m);
            statement.TotalCredits.Should().Be(450.00m);
            statement.ClosingBalance.Should().Be(950.00m); // 800 + 600 - 450 = 950
            statement.Transactions.Should().HaveCount(2);

            statement.Transactions[0].ReferenceNumber.Should().Be("INV-002");
            statement.Transactions[0].Debit.Should().Be(600.00m);
            statement.Transactions[0].Credit.Should().Be(0.00m);
            statement.Transactions[0].RunningBalance.Should().Be(1400.00m);

            statement.Transactions[1].ReferenceNumber.Should().Be("REC-002");
            statement.Transactions[1].Debit.Should().Be(0.00m);
            statement.Transactions[1].Credit.Should().Be(450.00m);
            statement.Transactions[1].RunningBalance.Should().Be(950.00m);
        }

        [Fact]
        public async Task GetProfitAndLossSummaryAsync_ShouldCalculateRevenue_HistoricalCOGS_GrossProfit_Expenses_AndNetProfit()
        {
            // Arrange
            using var connection = _factory.CreateConnection();
            await ((SqliteConnection)connection).OpenAsync();

            var customerId = await connection.ExecuteScalarAsync<int>(
                "INSERT INTO Customers (CustomerCode, CustomerName, OpeningBalance, OutstandingBalance) VALUES ('C-PL', 'PL Customer', 0.0, 0.0); SELECT last_insert_rowid();");

            var prod1Id = await connection.ExecuteScalarAsync<int>(
                "INSERT INTO Products (ProductCode, ProductName, BasePurchasePrice, DefaultSalePrice, CurrentStock) VALUES ('P-01', 'Product 1', 60.0, 100.0, 100.0); SELECT last_insert_rowid();");

            var prod2Id = await connection.ExecuteScalarAsync<int>(
                "INSERT INTO Products (ProductCode, ProductName, BasePurchasePrice, DefaultSalePrice, CurrentStock) VALUES ('P-02', 'Product 2', 70.0, 100.0, 100.0); SELECT last_insert_rowid();");

            // Sale 1: 2026-01-10: 10 units @ 100 sale, 60 cost. Revenue = 1000, COGS = 600, Gross Profit = 400
            var sale1Id = await connection.ExecuteScalarAsync<int>(
                @"INSERT INTO Sales (InvoiceNumber, CustomerId, SaleDate, SubTotal, DiscountAmount, NetTotal, PaidAmount, RemainingBalance, PaymentType, Status, AdminId)
                  VALUES ('INV-PL-01', @CustomerId, '2026-01-10 10:00:00', 1000.0, 0.0, 1000.0, 1000.0, 0.0, 'Cash', 'Completed', @AdminId);
                  SELECT last_insert_rowid();",
                new { CustomerId = customerId, AdminId = _testAdminId });

            await connection.ExecuteAsync(
                @"INSERT INTO SaleItems (SaleId, ProductId, Quantity, HistoricalSalePrice, HistoricalCostPrice, LineTotal)
                  VALUES (@SaleId, @ProductId, 10.0, 100.0, 60.0, 1000.0);",
                new { SaleId = sale1Id, ProductId = prod1Id });

            // Sale 2: 2026-01-20: 5 units @ 100 sale, 70 cost. Revenue = 500, COGS = 350, Gross Profit = 150
            var sale2Id = await connection.ExecuteScalarAsync<int>(
                @"INSERT INTO Sales (InvoiceNumber, CustomerId, SaleDate, SubTotal, DiscountAmount, NetTotal, PaidAmount, RemainingBalance, PaymentType, Status, AdminId)
                  VALUES ('INV-PL-02', @CustomerId, '2026-01-20 12:00:00', 500.0, 0.0, 500.0, 500.0, 0.0, 'Cash', 'Completed', @AdminId);
                  SELECT last_insert_rowid();",
                new { CustomerId = customerId, AdminId = _testAdminId });

            await connection.ExecuteAsync(
                @"INSERT INTO SaleItems (SaleId, ProductId, Quantity, HistoricalSalePrice, HistoricalCostPrice, LineTotal)
                  VALUES (@SaleId, @ProductId, 5.0, 100.0, 70.0, 500.0);",
                new { SaleId = sale2Id, ProductId = prod2Id });

            // Expenses: Rent = 200, Utilities = 50 -> Total Expenses = 250
            await connection.ExecuteAsync(
                @"INSERT INTO Expenses (ExpenseDate, ExpenseCategory, Amount, Description, PaymentSource, AdminId)
                  VALUES ('2026-01-12 09:00:00', 'Rent', 200.0, 'Warehouse Rent', 'Cash', @AdminId);",
                new { AdminId = _testAdminId });

            await connection.ExecuteAsync(
                @"INSERT INTO Expenses (ExpenseDate, ExpenseCategory, Amount, Description, PaymentSource, AdminId)
                  VALUES ('2026-01-22 14:00:00', 'Utilities', 50.0, 'Electricity', 'Cash', @AdminId);",
                new { AdminId = _testAdminId });

            var startDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var endDate = new DateTime(2026, 1, 31, 23, 59, 59, DateTimeKind.Utc);

            // Act
            var pnl = await _sut.GetProfitAndLossSummaryAsync(startDate, endDate, CancellationToken.None);

            // Assert
            pnl.Should().NotBeNull();
            pnl.StartDate.Should().Be(startDate);
            pnl.EndDate.Should().Be(endDate);
            pnl.TotalRevenue.Should().Be(1500.00m);
            pnl.CostOfGoodsSold.Should().Be(950.00m); // 600 + 350
            pnl.GrossProfit.Should().Be(550.00m); // 1500 - 950
            pnl.TotalExpenses.Should().Be(250.00m); // 200 + 50
            pnl.NetProfit.Should().Be(300.00m); // 550 - 250
            pnl.GrossMarginPercentage.Should().Be(36.67m); // (550 / 1500) * 100 = 36.67%
            pnl.NetMarginPercentage.Should().Be(20.00m); // (300 / 1500) * 100 = 20.00%
            pnl.ExpensesByCategory.Should().HaveCount(2);
            pnl.ExpensesByCategory.Should().Contain(e => e.CategoryName == "Rent" && e.TotalAmount == 200.00m);
            pnl.ExpensesByCategory.Should().Contain(e => e.CategoryName == "Utilities" && e.TotalAmount == 50.00m);
        }

        [Fact]
        public async Task GetStockValuationAsync_ShouldCalculateCurrentStock_UnitCost_AndTotalValuation()
        {
            // Arrange
            using var connection = _factory.CreateConnection();
            await ((SqliteConnection)connection).OpenAsync();

            await connection.ExecuteAsync(
                @"INSERT INTO Products (ProductCode, ProductName, BasePurchasePrice, DefaultSalePrice, CurrentStock)
                  VALUES ('P-VAL-01', 'Cooking Oil 1L', 20.0, 30.0, 50.0);");

            await connection.ExecuteAsync(
                @"INSERT INTO Products (ProductCode, ProductName, BasePurchasePrice, DefaultSalePrice, CurrentStock)
                  VALUES ('P-VAL-02', 'Basmati Rice 5kg', 15.0, 25.0, 100.0);");

            // Act
            var valuation = await _sut.GetStockValuationAsync(CancellationToken.None);

            // Assert
            valuation.Should().NotBeNull();
            valuation.TotalValuation.Should().Be(2500.00m); // (50 * 20) + (100 * 15) = 1000 + 1500 = 2500
            valuation.TotalPotentialRetailValue.Should().Be(4000.00m); // (50 * 30) + (100 * 25) = 1500 + 2500 = 4000
            valuation.TotalItemsCount.Should().Be(2);
            valuation.Items.Should().HaveCount(2);

            var item1 = valuation.Items.Find(i => i.ProductCode == "P-VAL-01");
            item1.Should().NotBeNull();
            item1!.ProductName.Should().Be("Cooking Oil 1L");
            item1.CurrentStock.Should().Be(50.00m);
            item1.UnitCost.Should().Be(20.00m);
            item1.TotalValuation.Should().Be(1000.00m);
            item1.DefaultSalePrice.Should().Be(30.00m);
            item1.PotentialRetailValue.Should().Be(1500.00m);

            var item2 = valuation.Items.Find(i => i.ProductCode == "P-VAL-02");
            item2.Should().NotBeNull();
            item2!.ProductName.Should().Be("Basmati Rice 5kg");
            item2.CurrentStock.Should().Be(100.00m);
            item2.UnitCost.Should().Be(15.00m);
            item2.TotalValuation.Should().Be(1500.00m);
            item2.DefaultSalePrice.Should().Be(25.00m);
            item2.PotentialRetailValue.Should().Be(2500.00m);
        }

        [Fact]
        public async Task GetProfitAndLossSummaryAsync_ShouldMaintainHistoricalImmutability_WhenMasterPricesChangeLater()
        {
            // Arrange
            using var connection = _factory.CreateConnection();
            await ((SqliteConnection)connection).OpenAsync();

            var customerId = await connection.ExecuteScalarAsync<int>(
                "INSERT INTO Customers (CustomerCode, CustomerName, OpeningBalance, OutstandingBalance) VALUES ('C-IMMUT', 'Immutability Customer', 0.0, 0.0); SELECT last_insert_rowid();");

            var productId = await connection.ExecuteScalarAsync<int>(
                "INSERT INTO Products (ProductCode, ProductName, BasePurchasePrice, DefaultSalePrice, CurrentStock) VALUES ('P-IMMUT', 'Immutability Product', 60.0, 100.0, 100.0); SELECT last_insert_rowid();");

            // Transaction occurred in January 2026 with snapshot cost = 60.0 and sale = 100.0
            var saleId = await connection.ExecuteScalarAsync<int>(
                @"INSERT INTO Sales (InvoiceNumber, CustomerId, SaleDate, SubTotal, DiscountAmount, NetTotal, PaidAmount, RemainingBalance, PaymentType, Status, AdminId)
                  VALUES ('INV-IMMUT-01', @CustomerId, '2026-01-10 10:00:00', 1000.0, 0.0, 1000.0, 1000.0, 0.0, 'Cash', 'Completed', @AdminId);
                  SELECT last_insert_rowid();",
                new { CustomerId = customerId, AdminId = _testAdminId });

            await connection.ExecuteAsync(
                @"INSERT INTO SaleItems (SaleId, ProductId, Quantity, HistoricalSalePrice, HistoricalCostPrice, LineTotal)
                  VALUES (@SaleId, @ProductId, 10.0, 100.0, 60.0, 1000.0);",
                new { SaleId = saleId, ProductId = productId });

            // In February 2026, master catalog prices change significantly:
            // Purchase cost jumps to 90.0 and Sale price jumps to 150.0
            await connection.ExecuteAsync(
                @"UPDATE Products 
                  SET BasePurchasePrice = 90.0, DefaultSalePrice = 150.0 
                  WHERE ProductId = @ProductId;",
                new { ProductId = productId });

            var startDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var endDate = new DateTime(2026, 1, 31, 23, 59, 59, DateTimeKind.Utc);

            // Act: Generate P&L for January 2026
            var pnl = await _sut.GetProfitAndLossSummaryAsync(startDate, endDate, CancellationToken.None);

            // Assert: Must strictly use historical snapshot prices from SaleItems, completely immune to subsequent master price modifications!
            pnl.Should().NotBeNull();
            pnl.TotalRevenue.Should().Be(1000.00m); // 10 * 100.00, NOT 10 * 150.00 (1500.00)
            pnl.CostOfGoodsSold.Should().Be(600.00m); // 10 * 60.00, NOT 10 * 90.00 (900.00)
            pnl.GrossProfit.Should().Be(400.00m); // 1000 - 600 = 400.00, NOT 1500 - 900 = 600 or 1000 - 900 = 100
        }
    }
}
