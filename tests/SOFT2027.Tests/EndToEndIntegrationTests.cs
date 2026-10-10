using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using SOFT2027.Core.Entities;
using SOFT2027.Core.Interfaces;
using SOFT2027.Data.Services;
using SOFT2027.Presentation.ViewModels;
using SOFT2027.UI;
using Xunit;

namespace SOFT2027.Tests
{
    public sealed class EndToEndIntegrationTests : IAsyncLifetime
    {
        private readonly string _testDbPath;
        private readonly string _connectionString;
        private readonly string _schemaPath;

        public EndToEndIntegrationTests()
        {
            _testDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"test_e2e_{Guid.NewGuid():N}.db");
            _connectionString = $"Data Source={_testDbPath};Foreign Keys=True;";
            _schemaPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "docs", "DATABASE_SCHEMA.sql"));
        }

        public async Task InitializeAsync()
        {
            var factory = new SqliteConnectionFactory(_testDbPath);
            var initializer = new DatabaseInitializer(factory, _schemaPath);
            await initializer.InitializeAsync();
            await SeedBaseDataAsync(factory);
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
                catch (IOException)
                {
                    // Ignore transient SQLite file locks during test teardown
                }
            }
            return Task.CompletedTask;
        }

        private async Task SeedBaseDataAsync(ISqliteConnectionFactory factory)
        {
            using var connection = factory.CreateConnection();
            if (connection is Microsoft.Data.Sqlite.SqliteConnection sqliteConn)
            {
                await sqliteConn.OpenAsync();
            }

            // Seed Admin
            await connection.ExecuteAsync(@"
                INSERT INTO AdminUsers (AdminId, Username, PasswordHash, FullName, Status, CreatedAt)
                VALUES (1, 'admin', 'hash123', 'System Administrator', 'Active', CURRENT_TIMESTAMP);
            ");

            // Seed Customer
            await connection.ExecuteAsync(@"
                INSERT INTO Customers (CustomerId, CustomerCode, CustomerName, ContactPerson, Phone, Address, AreaTerritory, OpeningBalance, OutstandingBalance, CreditLimit, Status, CreatedAt, UpdatedAt)
                VALUES (1, 'CUST-001', 'Al-Madina Traders', 'Muhammad Ali', '03001234567', 'Market Road', 'Central', 0.0, 0.0, 100000.0, 'Active', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);
            ");

            // Seed Product
            await connection.ExecuteAsync(@"
                INSERT INTO Products (ProductId, ProductCode, ProductName, BasePurchasePrice, DefaultSalePrice, CurrentStock, MinimumStock, Status, CreatedAt, UpdatedAt)
                VALUES (101, 'PROD-101', 'Basmati Rice 25kg', 100.0, 150.0, 20.0, 5.0, 'Active', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);
            ");
        }

        [Fact]
        public async Task FullUserJourney_SalesEntry_To_ReportsDashboard_UpdatesProfitAndLossAndStockValuation()
        {
            // Arrange: Bootstrap full container pointing to test database
            var container = Bootstrapper.CreateContainer(_connectionString);
            container.GetRequiredService<IAdminSession>().SignIn(new AdminUser
            {
                AdminId = 1, Username = "admin", FullName = "System Administrator", Status = "Active"
            });
            var mainVm = container.GetRequiredService<MainViewModel>();

            // Act 1: Navigate to Sales Entry
            mainVm.NavigateToSalesCommand.Execute(null);
            var salesVm = mainVm.CurrentViewModel.Should().BeOfType<SalesEntryViewModel>().Subject;

            // Act 2: Add line item and post sale
            salesVm.SelectedCustomer = new Customer { CustomerId = 1, CustomerName = "Al-Madina Traders" };
            salesVm.SelectedProduct = new Product
            {
                ProductId = 101,
                ProductCode = "PROD-101",
                ProductName = "Basmati Rice 25kg",
                BasePurchasePrice = 100m,
                DefaultSalePrice = 150m,
                CurrentStock = 20m
            };
            salesVm.Quantity = 5m;
            salesVm.UnitPrice = 150m;
            salesVm.DiscountPercentage = 0m;

            salesVm.AddItemCommand.Execute(null);
            await salesVm.SaveSaleCommand.ExecuteAsync(null);

            // The compact print sheet resolves saved invoices only. A later catalog edit must not rewrite the invoice rate.
            var savedProduct = await container.GetRequiredService<IProductRepository>().GetByIdAsync(101);
            savedProduct.Should().NotBeNull();
            savedProduct!.DefaultSalePrice = 999m;
            savedProduct.ProductCode = "PROD-RENAMED";
            savedProduct.ProductName = "Renamed Basmati Rice";
            await container.GetRequiredService<IProductRepository>().UpdateAsync(savedProduct);
            var printSheet = await salesVm.LoadRecentInvoiceSheetAsync();
            printSheet.Should().ContainSingle();
            printSheet[0].Lines.Should().ContainSingle().Which.UnitPrice.Should().Be(150m);
            printSheet[0].Lines[0].ProductCode.Should().Be("PROD-101");
            printSheet[0].Lines[0].ProductName.Should().Be("Basmati Rice 25kg");
            printSheet[0].Total.Should().Be(750m);
            using (var verifyPrintConn = new SqliteConnection(_connectionString))
            {
                await verifyPrintConn.OpenAsync();
                (await verifyPrintConn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Sales;")).Should().Be(1);
                (await verifyPrintConn.ExecuteScalarAsync<decimal>("SELECT CurrentStock FROM Products WHERE ProductId=101;")).Should().Be(15m);
            }

            // Act 3: Navigate to Reports Dashboard
            mainVm.NavigateToReportsCommand.Execute(null);
            var reportsVm = mainVm.CurrentViewModel.Should().BeOfType<ReportsDashboardViewModel>().Subject;

            await reportsVm.LoadReportsCommand.ExecuteAsync(null);

            // Assert: Verify business outcomes across layers
            reportsVm.ProfitAndLoss.Should().NotBeNull();
            reportsVm.ProfitAndLoss!.TotalRevenue.Should().Be(750m);
            reportsVm.ProfitAndLoss!.CostOfGoodsSold.Should().Be(500m);
            reportsVm.ProfitAndLoss!.GrossProfit.Should().Be(250m);
            reportsVm.PeriodSales.Should().Be(750m);
            reportsVm.TodaySales.Should().Be(750m);
            reportsVm.PeriodPurchases.Should().Be(0m);
            reportsVm.OutstandingReceivables.Should().Be(750m);
            reportsVm.LowStockItemCount.Should().Be(0);

            reportsVm.StockValuation.Should().NotBeNull();
            var updatedProduct = reportsVm.StockValuation!.Items.FirstOrDefault(p => p.ProductId == 101);
            updatedProduct.Should().NotBeNull();
            updatedProduct!.CurrentStock.Should().Be(15m);
        }

        [Fact]
        public async Task DatabaseLock_ShouldBeHandledGracefully_WithoutCrashingHostOrFreezingUI()
        {
            // Arrange: Bootstrap container and prepare sales view model
            var container = Bootstrapper.CreateContainer(_connectionString);
            container.GetRequiredService<IAdminSession>().SignIn(new AdminUser
            {
                AdminId = 1, Username = "admin", FullName = "System Administrator", Status = "Active"
            });
            var salesVm = container.GetRequiredService<SalesEntryViewModel>();

            salesVm.SelectedCustomer = new Customer { CustomerId = 1, CustomerName = "Al-Madina Traders" };
            salesVm.SelectedProduct = new Product
            {
                ProductId = 101,
                ProductName = "Basmati Rice 25kg",
                BasePurchasePrice = 100m,
                DefaultSalePrice = 150m,
                CurrentStock = 20m
            };
            salesVm.Quantity = 1m;
            salesVm.UnitPrice = 150m;
            salesVm.AddItemCommand.Execute(null);

            // Act: Acquire exclusive database lock from a secondary connection
            using var lockConn = new SqliteConnection(_connectionString);
            await lockConn.OpenAsync();
            using var lockCmd = lockConn.CreateCommand();
            lockCmd.CommandText = "BEGIN EXCLUSIVE;";
            await lockCmd.ExecuteNonQueryAsync();

            try
            {
                // Attempt to post sale while database is locked
                await salesVm.SaveSaleCommand.ExecuteAsync(null);

                // Assert (Silent Failure Hunter Invariants):
                // 1. Host process did not crash
                // 2. IsBusy is reset to false
                salesVm.IsBusy.Should().BeFalse();

                // 3. Error state is properly populated
                salesVm.HasErrors.Should().BeTrue();
                salesVm.ErrorMessage.Should().NotBeNullOrWhiteSpace();
                salesVm.ErrorMessage.Should().MatchRegex("(?i)lock|busy");

                // 4. StatusAnnouncement announces error to screen readers
                salesVm.StatusAnnouncement.Should().Contain("Error");

                // 5. Database integrity: No partial commit occurred
                using var verifyConn = new SqliteConnection(_connectionString);
                await verifyConn.OpenAsync();
                var salesCount = await verifyConn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Sales;");
                salesCount.Should().Be(0);
            }
            finally
            {
                using var rollbackCmd = lockConn.CreateCommand();
                rollbackCmd.CommandText = "ROLLBACK;";
                await rollbackCmd.ExecuteNonQueryAsync();
            }
        }

        [Fact]
        public void Bootstrapper_ShouldResolveAllRegisteredServices_WithoutNullsOrCircularDependencies()
        {
            // Act: Resolve full service graph from bootstrapper
            var container = Bootstrapper.CreateContainer(_connectionString);

            // Assert: All core services and repositories resolve cleanly
            container.GetRequiredService<ISqliteConnectionFactory>().Should().NotBeNull();
            container.GetRequiredService<IDatabaseInitializer>().Should().NotBeNull();
            container.GetRequiredService<ICustomerRepository>().Should().NotBeNull();
            container.GetRequiredService<ISupplierRepository>().Should().NotBeNull();
            container.GetRequiredService<IProductRepository>().Should().NotBeNull();
            container.GetRequiredService<ICustomerPricingHistoryRepository>().Should().NotBeNull();
            container.GetRequiredService<ISupplierPricingHistoryRepository>().Should().NotBeNull();
            container.GetRequiredService<ISalesRepository>().Should().NotBeNull();
            container.GetRequiredService<IPurchaseRepository>().Should().NotBeNull();
            container.GetRequiredService<IStockMovementRepository>().Should().NotBeNull();
            container.GetRequiredService<ICustomerLedgerRepository>().Should().NotBeNull();
            container.GetRequiredService<ISupplierLedgerRepository>().Should().NotBeNull();
            container.GetRequiredService<IReceiptRepository>().Should().NotBeNull();
            container.GetRequiredService<IPaymentRepository>().Should().NotBeNull();
            container.GetRequiredService<IExpenseRepository>().Should().NotBeNull();
            container.GetRequiredService<ICashTransactionRepository>().Should().NotBeNull();
            container.GetRequiredService<IBankTransactionRepository>().Should().NotBeNull();
            container.GetRequiredService<IReportRepository>().Should().NotBeNull();
            container.GetRequiredService<ITransactionCoordinator>().Should().NotBeNull();
            container.GetRequiredService<IFinancialCoordinator>().Should().NotBeNull();
            container.GetRequiredService<SalesEntryViewModel>().Should().NotBeNull();
            container.GetRequiredService<ReportsDashboardViewModel>().Should().NotBeNull();
            container.GetRequiredService<MainViewModel>().Should().NotBeNull();
        }
    }
}
