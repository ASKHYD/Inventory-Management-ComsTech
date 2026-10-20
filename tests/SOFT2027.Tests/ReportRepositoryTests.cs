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

        [Theory]
        [InlineData("SalesByDate")][InlineData("SalesByInvoice")][InlineData("SalesByCustomer")][InlineData("SalesByProduct")]
        [InlineData("SalesReturns")][InlineData("SalesDiscounts")][InlineData("SalesPaymentStatus")]
        [InlineData("PurchasesByDate")][InlineData("PurchasesByInvoice")][InlineData("PurchasesBySupplier")][InlineData("PurchasesByProduct")]
        [InlineData("PurchaseReturns")][InlineData("PurchaseDiscounts")][InlineData("PurchasesPaymentStatus")]
        [InlineData("CurrentStock")][InlineData("StockValuation")][InlineData("StockMovements")][InlineData("StockAdjustments")][InlineData("LowStock")][InlineData("StockMovementByProduct")]
        [InlineData("Customers")][InlineData("CustomerStatement")][InlineData("OutstandingReceivables")][InlineData("CustomerSalesHistory")][InlineData("CustomerPriceHistory")][InlineData("CustomerPriceChanges")]
        [InlineData("Suppliers")][InlineData("SupplierStatement")][InlineData("OutstandingPayables")][InlineData("SupplierPurchaseHistory")][InlineData("SupplierPriceHistory")][InlineData("SupplierPriceChanges")]
        [InlineData("ProfitAndLoss")][InlineData("ExpensesByDate")][InlineData("ExpensesByCategory")][InlineData("CashActivity")][InlineData("CashBalance")][InlineData("BankActivity")][InlineData("BankBalance")][InlineData("ReceiptsPayments")][InlineData("ReceivablesPayables")]
        [InlineData("ProductPriceList")][InlineData("ProductProfitability")]
        public async Task GetOperationalReportAsync_EachRegisteredReportExecutesAgainstTheRealSchema(string key)
        {
            var result = await _sut.GetOperationalReportAsync(new OperationalReportRequest
            {
                ReportKey = key,
                StartDate = new DateTime(2026, 1, 1),
                EndDate = new DateTime(2026, 1, 31)
            });

            result.Title.Should().NotBeNullOrWhiteSpace();
            result.Columns.Should().NotBeEmpty();
        }

        [Fact]
        public async Task OperationalSalesReports_UseSavedDiscountPaymentAndHistoricalPriceValues()
        {
            using var connection = _factory.CreateConnection();
            await ((SqliteConnection)connection).OpenAsync();
            var customerId = await connection.ExecuteScalarAsync<int>("INSERT INTO Customers (CustomerCode,CustomerName) VALUES ('RPT-C1','Report Customer'); SELECT last_insert_rowid();");
            var productId = await connection.ExecuteScalarAsync<int>("INSERT INTO Products (ProductCode,ProductName,BasePurchasePrice,DefaultSalePrice,CurrentStock) VALUES ('RPT-P1','Report Product',999,999,8); SELECT last_insert_rowid();");
            var saleId = await connection.ExecuteScalarAsync<int>(@"INSERT INTO Sales (InvoiceNumber,CustomerId,SaleDate,SubTotal,DiscountAmount,NetTotal,PaidAmount,RemainingBalance,PaymentType,Status,AdminId)
                VALUES ('RPT-INV-1',@CustomerId,'2026-01-10',200,20,180,100,80,'Split','Completed',@AdminId); SELECT last_insert_rowid();", new { CustomerId = customerId, AdminId = _testAdminId });
            await connection.ExecuteAsync(@"INSERT INTO SaleItems (SaleId,ProductId,Quantity,HistoricalSalePrice,HistoricalCostPrice,DiscountPercentage,LineTotal)
                VALUES (@SaleId,@ProductId,2,100,60,10,180);", new { SaleId = saleId, ProductId = productId });
            await connection.ExecuteAsync("UPDATE Products SET DefaultSalePrice=150,BasePurchasePrice=110 WHERE ProductId=@ProductId", new { ProductId = productId });

            var invoice = await _sut.GetOperationalReportAsync(new OperationalReportRequest { ReportKey = "SalesByInvoice", StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 1, 31) });
            invoice.Rows.Should().ContainSingle();
            Convert.ToDecimal(invoice.Rows[0].Values["NetTotal"]).Should().Be(180m);
            Convert.ToDecimal(invoice.Rows[0].Values["PaidAmount"]).Should().Be(100m);
            Convert.ToDecimal(invoice.Rows[0].Values["RemainingBalance"]).Should().Be(80m);

            var productReport = await _sut.GetOperationalReportAsync(new OperationalReportRequest { ReportKey = "SalesByProduct", StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 1, 31) });
            productReport.Rows.Should().ContainSingle();
            Convert.ToDecimal(productReport.Rows[0].Values["GrossSales"]).Should().Be(180m);
            Convert.ToDecimal(productReport.Rows[0].Values["Discount"]).Should().Be(20m);
        }

        [Fact]
        public async Task OperationalReports_ReconcileSalesPurchasesReturnsInventoryLedgersAndFinance()
        {
            using var connection = _factory.CreateConnection();
            await ((SqliteConnection)connection).OpenAsync();
            var customerId = await connection.ExecuteScalarAsync<int>("INSERT INTO Customers (CustomerCode,CustomerName,OutstandingBalance) VALUES ('ALL-C1','Retailer',500); SELECT last_insert_rowid();");
            var supplierId = await connection.ExecuteScalarAsync<int>("INSERT INTO Suppliers (SupplierCode,SupplierName,OutstandingBalance) VALUES ('ALL-S1','Wholesaler',250); SELECT last_insert_rowid();");
            var productId = await connection.ExecuteScalarAsync<int>("INSERT INTO Products (ProductCode,ProductName,BasePurchasePrice,PurchasePercentage,DefaultSalePrice,SellingPercentage,CurrentStock,MinimumStock) VALUES ('ALL-P1','Cooking Oil',60,4,100,66.67,8,10); SELECT last_insert_rowid();");
            var saleId = await connection.ExecuteScalarAsync<int>(@"INSERT INTO Sales (InvoiceNumber,CustomerId,SaleDate,SubTotal,DiscountAmount,NetTotal,PaidAmount,RemainingBalance,PaymentType,Status,AdminId)
                VALUES ('ALL-INV-1',@CustomerId,'2026-01-10',1000,100,900,400,500,'Split','Completed',@AdminId); SELECT last_insert_rowid();", new { CustomerId = customerId, AdminId = _testAdminId });
            await connection.ExecuteAsync(@"INSERT INTO SaleItems (SaleId,ProductId,ProductCodeAtSale,ProductNameAtSale,Quantity,HistoricalSalePrice,HistoricalCostPrice,SellingPercentage,DiscountPercentage,LineTotal)
                VALUES (@SaleId,@ProductId,'ALL-P1','Cooking Oil',10,100,60,66.67,10,900);", new { SaleId = saleId, ProductId = productId });
            var purchaseId = await connection.ExecuteScalarAsync<int>(@"INSERT INTO Purchases (InvoiceNumber,SupplierId,PurchaseDate,SubTotal,DiscountAmount,NetTotal,PaidAmount,RemainingBalance,Status,AdminId)
                VALUES ('ALL-PUR-1',@SupplierId,'2026-01-10',500,50,450,200,250,'Received',@AdminId); SELECT last_insert_rowid();", new { SupplierId = supplierId, AdminId = _testAdminId });
            await connection.ExecuteAsync(@"INSERT INTO PurchaseItems (PurchaseId,ProductId,ProductCodeAtPurchase,ProductNameAtPurchase,Quantity,HistoricalPurchasePrice,PurchasePercentage,DiscountPercentage,LineTotal)
                VALUES (@PurchaseId,@ProductId,'ALL-P1','Cooking Oil',10,50,4,10,450);", new { PurchaseId = purchaseId, ProductId = productId });
            var saleReturnId = await connection.ExecuteScalarAsync<int>(@"INSERT INTO SalesReturns (ReturnNumber,OriginalSaleId,CustomerId,ReturnDate,TotalRefundAmount,AdminId) VALUES ('ALL-SR-1',@SaleId,@CustomerId,'2026-01-11',200,@AdminId); SELECT last_insert_rowid();", new { SaleId = saleId, CustomerId = customerId, AdminId = _testAdminId });
            await connection.ExecuteAsync("INSERT INTO SalesReturnItems (ReturnId,ProductId,Quantity,RefundRate,LineTotal) VALUES (@ReturnId,@ProductId,2,100,200);", new { ReturnId = saleReturnId, ProductId = productId });
            var purchaseReturnId = await connection.ExecuteScalarAsync<int>(@"INSERT INTO PurchaseReturns (ReturnNumber,OriginalPurchaseId,SupplierId,ReturnDate,TotalRefundAmount,AdminId) VALUES ('ALL-PR-1',@PurchaseId,@SupplierId,'2026-01-11',100,@AdminId); SELECT last_insert_rowid();", new { PurchaseId = purchaseId, SupplierId = supplierId, AdminId = _testAdminId });
            await connection.ExecuteAsync("INSERT INTO PurchaseReturnItems (ReturnId,ProductId,Quantity,RefundRate,LineTotal) VALUES (@ReturnId,@ProductId,2,50,100);", new { ReturnId = purchaseReturnId, ProductId = productId });
            await connection.ExecuteAsync(@"INSERT INTO StockMovements (ProductId,MovementDate,MovementType,ReferenceNumber,QuantityIn,QuantityOut,BalanceQuantity,AdminId,Remarks)
                VALUES (@ProductId,'2026-01-10','PURCHASE','ALL-PUR-1',10,0,10,@AdminId,''),(@ProductId,'2026-01-10','SALE','ALL-INV-1',0,10,0,@AdminId,''),(@ProductId,'2026-01-11','ADJUSTMENT','ALL-ADJ-1',8,0,8,@AdminId,'Count correction');", new { ProductId = productId, AdminId = _testAdminId });
            await connection.ExecuteAsync("INSERT INTO CustomerLedger (CustomerId,TransactionDate,TransactionType,ReferenceNumber,Description,Debit,Credit,RunningBalance) VALUES (@CustomerId,'2026-01-10','SALE','ALL-INV-1','Sale',900,400,500);", new { CustomerId = customerId });
            await connection.ExecuteAsync("INSERT INTO SupplierLedger (SupplierId,TransactionDate,TransactionType,ReferenceNumber,Description,Debit,Credit,RunningBalance) VALUES (@SupplierId,'2026-01-10','PURCHASE','ALL-PUR-1','Purchase',200,450,250);", new { SupplierId = supplierId });
            await connection.ExecuteAsync("INSERT INTO Receipts (ReceiptNumber,CustomerId,ReceiptDate,Amount,PaymentMode,AdminId) VALUES ('ALL-REC-1',@CustomerId,'2026-01-10',100,'Cash',@AdminId);", new { CustomerId = customerId, AdminId = _testAdminId });
            await connection.ExecuteAsync("INSERT INTO Payments (PaymentNumber,SupplierId,PaymentDate,Amount,PaymentMode,AdminId) VALUES ('ALL-PAY-1',@SupplierId,'2026-01-10',150,'Cash',@AdminId);", new { SupplierId = supplierId, AdminId = _testAdminId });
            await connection.ExecuteAsync("INSERT INTO Expenses (ExpenseDate,ExpenseCategory,Amount,Description,PaymentSource,AdminId) VALUES ('2026-01-10','Rent',50,'Shop rent','Cash',@AdminId);", new { AdminId = _testAdminId });
            await connection.ExecuteAsync("INSERT INTO CashTransactions (TransactionDate,TransactionType,ReferenceNumber,Description,Debit,Credit,RunningBalance,AdminId) VALUES ('2026-01-10','RECEIPT','ALL-REC-1','Receipt',100,0,100,@AdminId); INSERT INTO BankTransactions (TransactionDate,TransactionType,ReferenceNumber,Description,Debit,Credit,RunningBalance,AdminId) VALUES ('2026-01-10','DEPOSIT','ALL-DEP-1','Deposit',300,0,300,@AdminId);", new { AdminId = _testAdminId });
            await connection.ExecuteAsync("INSERT INTO CustomerProductPricingHistory (CustomerId,ProductId,SalePrice,SellingPercentage,DiscountPercentage,Quantity,InvoiceNumber,TransactionDate,AdminId) VALUES (@CustomerId,@ProductId,100,66.67,10,10,'ALL-INV-1','2026-01-10',@AdminId); INSERT INTO SupplierProductPricingHistory (SupplierId,ProductId,PurchasePrice,PurchasePercentage,DiscountPercentage,Quantity,InvoiceNumber,TransactionDate,AdminId) VALUES (@SupplierId,@ProductId,50,4,10,10,'ALL-PUR-1','2026-01-10',@AdminId);", new { CustomerId = customerId, SupplierId = supplierId, ProductId = productId, AdminId = _testAdminId });

            var start = new DateTime(2026, 1, 1); var end = new DateTime(2026, 1, 31);
            async Task<OperationalReport> Report(string key) => await _sut.GetOperationalReportAsync(new OperationalReportRequest { ReportKey = key, StartDate = start, EndDate = end });
            static decimal Amount(OperationalReport report, int row, string field) => Convert.ToDecimal(report.Rows[row].Values[field]);

            Amount(await Report("SalesByDate"), 0, "NetTotal").Should().Be(900m);
            Amount(await Report("SalesReturns"), 0, "LineTotal").Should().Be(200m);
            Amount(await Report("SalesDiscounts"), 0, "DiscountAmount").Should().Be(100m);
            Amount(await Report("SalesPaymentStatus"), 0, "NetSales").Should().Be(900m);
            Amount(await Report("PurchasesByDate"), 0, "NetTotal").Should().Be(450m);
            Amount(await Report("PurchaseReturns"), 0, "LineTotal").Should().Be(100m);
            Amount(await Report("PurchaseDiscounts"), 0, "DiscountAmount").Should().Be(50m);
            Amount(await Report("PurchasesPaymentStatus"), 0, "Outstanding").Should().Be(250m);
            (await Report("LowStock")).Rows.Should().ContainSingle();
            Amount(await Report("CurrentStock"), 0, "StockValue").Should().Be(480m);
            (await Report("StockAdjustments")).Rows.Should().ContainSingle();
            Amount(await Report("StockMovementByProduct"), 0, "QuantityOut").Should().Be(10m);
            Amount(await Report("OutstandingReceivables"), 0, "OutstandingBalance").Should().Be(500m);
            Amount(await Report("OutstandingPayables"), 0, "OutstandingBalance").Should().Be(250m);
            Amount(await Report("CustomerPriceHistory"), 0, "SalePrice").Should().Be(100m);
            Amount(await Report("SupplierPriceHistory"), 0, "PurchasePrice").Should().Be(50m);
            Amount(await Report("ExpensesByCategory"), 0, "TotalAmount").Should().Be(50m);
            Amount(await Report("CashBalance"), 0, "RunningBalance").Should().Be(100m);
            Amount(await Report("BankBalance"), 0, "RunningBalance").Should().Be(300m);
            (await Report("ReceiptsPayments")).Rows.Should().HaveCount(2);
            var pnl = await Report("ProfitAndLoss");
            Amount(pnl, 0, "Revenue").Should().Be(700m);
            Amount(pnl, 0, "CostOfGoodsSold").Should().Be(480m);
            Amount(pnl, 0, "NetProfit").Should().Be(170m);
            var profitability = await Report("ProductProfitability");
            Amount(profitability, 0, "NetSales").Should().Be(700m);
            Amount(profitability, 0, "NetCost").Should().Be(480m);
            Amount(profitability, 0, "GrossProfit").Should().Be(220m);
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
