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
    public sealed class TransactionCoordinatorTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly ISqliteConnectionFactory _factory;
        private readonly IDatabaseInitializer _initializer;
        private readonly ITransactionCoordinator _sut;

        private int _testAdminId;
        private int _testCustomerId;
        private int _testSupplierId;
        private int _testProductId;

        public TransactionCoordinatorTests()
        {
            _testDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"test_tx_{Guid.NewGuid():N}.db");
            var schemaPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "docs", "DATABASE_SCHEMA.sql"));

            _factory = new SqliteConnectionFactory(_testDbPath);
            _initializer = new DatabaseInitializer(_factory, schemaPath);

            var salesRepo = new SalesRepository(_factory);
            var purchaseRepo = new PurchaseRepository(_factory);
            var productRepo = new ProductRepository(_factory);
            var stockMovementRepo = new StockMovementRepository(_factory);
            var customerRepo = new CustomerRepository(_factory);
            var supplierRepo = new SupplierRepository(_factory);
            var customerLedgerRepo = new CustomerLedgerRepository(_factory);
            var supplierLedgerRepo = new SupplierLedgerRepository(_factory);
            var customerPricingHistoryRepo = new CustomerPricingHistoryRepository(_factory);
            var supplierPricingHistoryRepo = new SupplierPricingHistoryRepository(_factory);

            _sut = new TransactionCoordinator(
                _factory,
                salesRepo,
                purchaseRepo,
                productRepo,
                stockMovementRepo,
                customerRepo,
                supplierRepo,
                customerLedgerRepo,
                supplierLedgerRepo,
                customerPricingHistoryRepo,
                supplierPricingHistoryRepo);

            _initializer.InitializeAsync().GetAwaiter().GetResult();
            SeedTestData();
        }

        private void SeedTestData()
        {
            using var connection = _factory.CreateConnection();
            connection.Open();

            _testAdminId = connection.ExecuteScalar<int>(
                "INSERT INTO AdminUsers (Username, PasswordHash, FullName) VALUES ('admin1', 'hash', 'Admin User'); SELECT last_insert_rowid();");

            _testCustomerId = connection.ExecuteScalar<int>(
                "INSERT INTO Customers (CustomerCode, CustomerName, OpeningBalance, OutstandingBalance) VALUES ('CUST-001', 'Test Customer', 0.0, 0.0); SELECT last_insert_rowid();");

            _testSupplierId = connection.ExecuteScalar<int>(
                "INSERT INTO Suppliers (SupplierCode, SupplierName, OpeningBalance, OutstandingBalance) VALUES ('SUPP-001', 'Test Supplier', 0.0, 0.0); SELECT last_insert_rowid();");

            _testProductId = connection.ExecuteScalar<int>(
                "INSERT INTO Products (ProductCode, ProductName, BasePurchasePrice, DefaultSalePrice, CurrentStock) VALUES ('PRD-001', 'Test Product', 80.0, 100.0, 50.0); SELECT last_insert_rowid();");
        }

        public void Dispose()
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
                    // Ignore transient file lock on cleanup
                }
            }
        }

        [Fact]
        public async Task PostSaleAsync_ShouldAtomicallyCommit_SaleItemsStockAndLedger_WhenAllSucceed()
        {
            // Arrange
            var sale = new Sale
            {
                InvoiceNumber = "INV-2026-0001",
                CustomerId = _testCustomerId,
                SaleDate = DateTime.UtcNow,
                SubTotal = 500.0m,
                DiscountAmount = 0.0m,
                NetTotal = 500.0m,
                PaidAmount = 200.0m,
                RemainingBalance = 300.0m,
                PaymentType = "Split",
                Status = "Completed",
                AdminId = _testAdminId,
                Remarks = "Test Sale All Succeeded"
            };

            sale.Items.Add(new SaleItem
            {
                ProductId = _testProductId,
                Quantity = 5.0m,
                HistoricalSalePrice = 100.0m,
                HistoricalCostPrice = 80.0m,
                SellingPercentage = 25.0m,
                DiscountPercentage = 0.0m,
                LineTotal = 500.0m
            });

            // Act
            var saleId = await _sut.PostSaleAsync(sale, CancellationToken.None);

            // Assert
            saleId.Should().BeGreaterThan(0);

            using var verifyConn = _factory.CreateConnection();
            verifyConn.Open();

            // 1. Verify Sales header
            var dbSale = await verifyConn.QuerySingleOrDefaultAsync<Sale>(
                "SELECT * FROM Sales WHERE SaleId = @SaleId;", new { SaleId = saleId });
            dbSale.Should().NotBeNull();
            dbSale!.InvoiceNumber.Should().Be("INV-2026-0001");
            dbSale.NetTotal.Should().Be(500.0m);
            dbSale.RemainingBalance.Should().Be(300.0m);

            // 2. Verify SaleItems
            var items = (await verifyConn.QueryAsync<SaleItem>(
                "SELECT * FROM SaleItems WHERE SaleId = @SaleId;", new { SaleId = saleId })).AsList();
            items.Should().HaveCount(1);
            items[0].HistoricalSalePrice.Should().Be(100.0m);
            items[0].HistoricalCostPrice.Should().Be(80.0m);
            items[0].Quantity.Should().Be(5.0m);

            // 3. Verify Stock decrement (50 - 5 = 45)
            var currentStock = await verifyConn.ExecuteScalarAsync<decimal>(
                "SELECT CurrentStock FROM Products WHERE ProductId = @ProductId;", new { ProductId = _testProductId });
            currentStock.Should().Be(45.0m);

            // 4. Verify StockMovement log
            var movement = await verifyConn.QuerySingleOrDefaultAsync<StockMovement>(
                "SELECT * FROM StockMovements WHERE ReferenceNumber = @Ref;", new { Ref = "INV-2026-0001" });
            movement.Should().NotBeNull();
            movement!.MovementType.Should().Be("SALE");
            movement.QuantityOut.Should().Be(5.0m);
            movement.BalanceQuantity.Should().Be(45.0m);

            // 5. Verify Customer balance update (+300 remaining)
            var custBalance = await verifyConn.ExecuteScalarAsync<decimal>(
                "SELECT OutstandingBalance FROM Customers WHERE CustomerId = @CustomerId;", new { CustomerId = _testCustomerId });
            custBalance.Should().Be(300.0m);

            // 6. Verify CustomerLedger entry
            var ledger = await verifyConn.QuerySingleOrDefaultAsync<CustomerLedgerEntry>(
                "SELECT * FROM CustomerLedger WHERE ReferenceNumber = @Ref;", new { Ref = "INV-2026-0001" });
            ledger.Should().NotBeNull();
            ledger!.Debit.Should().Be(500.0m);
            ledger.Credit.Should().Be(200.0m);
            ledger.RunningBalance.Should().Be(300.0m);

            // 7. Verify Pricing History snapshot
            var history = await verifyConn.QuerySingleOrDefaultAsync<CustomerProductPricingHistory>(
                "SELECT * FROM CustomerProductPricingHistory WHERE InvoiceNumber = @Inv;", new { Inv = "INV-2026-0001" });
            history.Should().NotBeNull();
            history!.SalePrice.Should().Be(100.0m);
            history.Quantity.Should().Be(5.0m);
        }

        [Fact]
        public async Task PostSaleAsync_ShouldRollbackEntireTransaction_WhenLedgerOrSubsequentOperationFails()
        {
            // Silent Failure Hunter Mandate:
            // Explicitly assert that if a secondary step fails (e.g. invalid customer balance update,
            // ledger constraint failure, or invalid foreign key), the ENTIRE transaction is rolled back.
            // Nothing must leak into Sales, SaleItems, Products stock, StockMovements, Customer balance, CustomerLedger, or PricingHistory.

            // Arrange: Capture pre-transaction state
            using (var preConn = _factory.CreateConnection())
            {
                preConn.Open();
                var preStock = await preConn.ExecuteScalarAsync<decimal>(
                    "SELECT CurrentStock FROM Products WHERE ProductId = @ProductId;", new { ProductId = _testProductId });
                var preBalance = await preConn.ExecuteScalarAsync<decimal>(
                    "SELECT OutstandingBalance FROM Customers WHERE CustomerId = @CustomerId;", new { CustomerId = _testCustomerId });
                preStock.Should().Be(50.0m);
                preBalance.Should().Be(0.0m);
            }

            // Create a sale with an invalid CustomerId (non-existent foreign key) or invalid data that triggers a rollback
            var invalidSale = new Sale
            {
                InvoiceNumber = "INV-FAIL-001",
                CustomerId = 999999, // Foreign key violation on customer ledger / sales
                SaleDate = DateTime.UtcNow,
                SubTotal = 200.0m,
                DiscountAmount = 0.0m,
                NetTotal = 200.0m,
                PaidAmount = 0.0m,
                RemainingBalance = 200.0m,
                PaymentType = "Credit",
                Status = "Completed",
                AdminId = _testAdminId,
                Remarks = "Intentional Failure Test"
            };

            invalidSale.Items.Add(new SaleItem
            {
                ProductId = _testProductId,
                Quantity = 10.0m,
                HistoricalSalePrice = 100.0m,
                HistoricalCostPrice = 80.0m,
                LineTotal = 1000.0m
            });

            // Act
            Func<Task> act = async () => await _sut.PostSaleAsync(invalidSale, CancellationToken.None);

            // Assert: Must throw a real database/business failure (NOT NotImplementedException)
            var ex = await act.Should().ThrowAsync<Exception>();
            ex.Which.Should().NotBeOfType<NotImplementedException>("a genuine database or domain failure must trigger rollback, not an unimplemented stub");

            // Assert: Verify complete ACID rollback on a fresh SQLite connection
            using var verifyConn = _factory.CreateConnection();
            verifyConn.Open();

            var salesCount = await verifyConn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Sales WHERE InvoiceNumber = 'INV-FAIL-001';");
            salesCount.Should().Be(0, "Invoice creation must be rolled back");

            var saleItemsCount = await verifyConn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM SaleItems WHERE ProductId = @ProductId AND Quantity = 10.0;", new { ProductId = _testProductId });
            saleItemsCount.Should().Be(0, "Sale items must be rolled back");

            var stockAfterFailure = await verifyConn.ExecuteScalarAsync<decimal>(
                "SELECT CurrentStock FROM Products WHERE ProductId = @ProductId;", new { ProductId = _testProductId });
            stockAfterFailure.Should().Be(50.0m, "Stock decrement must be rolled back to pre-transaction value 50.0");

            var stockMovementsCount = await verifyConn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM StockMovements WHERE ReferenceNumber = 'INV-FAIL-001';");
            stockMovementsCount.Should().Be(0, "Stock movements must be rolled back");

            var custBalanceAfterFailure = await verifyConn.ExecuteScalarAsync<decimal>(
                "SELECT OutstandingBalance FROM Customers WHERE CustomerId = @CustomerId;", new { CustomerId = _testCustomerId });
            custBalanceAfterFailure.Should().Be(0.0m, "Customer balance must remain unchanged");

            var ledgerCount = await verifyConn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM CustomerLedger WHERE ReferenceNumber = 'INV-FAIL-001';");
            ledgerCount.Should().Be(0, "Customer ledger must have zero entries for failed invoice");

            var pricingHistoryCount = await verifyConn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM CustomerProductPricingHistory WHERE InvoiceNumber = 'INV-FAIL-001';");
            pricingHistoryCount.Should().Be(0, "Pricing history must not be recorded on failed transaction");
        }

        [Fact]
        public async Task PostPurchaseAsync_ShouldAtomicallyCommit_PurchaseItemsStockAndLedger_WhenAllSucceed()
        {
            // Arrange
            var purchase = new Purchase
            {
                InvoiceNumber = "PUR-2026-0001",
                SupplierId = _testSupplierId,
                PurchaseDate = DateTime.UtcNow,
                SubTotal = 1900.0m,
                DiscountAmount = 0.0m,
                NetTotal = 1900.0m,
                PaidAmount = 900.0m,
                RemainingBalance = 1000.0m,
                Status = "Received",
                AdminId = _testAdminId,
                Remarks = "Test Purchase All Succeeded"
            };

            purchase.Items.Add(new PurchaseItem
            {
                ProductId = _testProductId,
                Quantity = 20.0m,
                HistoricalPurchasePrice = 95.0m,
                PurchasePercentage = 0.0m,
                DiscountPercentage = 0.0m,
                LineTotal = 1900.0m
            });

            // Act
            var purchaseId = await _sut.PostPurchaseAsync(purchase, CancellationToken.None);

            // Assert
            purchaseId.Should().BeGreaterThan(0);

            using var verifyConn = _factory.CreateConnection();
            verifyConn.Open();

            // 1. Verify Purchase header
            var dbPurchase = await verifyConn.QuerySingleOrDefaultAsync<Purchase>(
                "SELECT * FROM Purchases WHERE PurchaseId = @PurchaseId;", new { PurchaseId = purchaseId });
            dbPurchase.Should().NotBeNull();
            dbPurchase!.InvoiceNumber.Should().Be("PUR-2026-0001");
            dbPurchase.NetTotal.Should().Be(1900.0m);
            dbPurchase.RemainingBalance.Should().Be(1000.0m);

            // 2. Verify PurchaseItems
            var items = (await verifyConn.QueryAsync<PurchaseItem>(
                "SELECT * FROM PurchaseItems WHERE PurchaseId = @PurchaseId;", new { PurchaseId = purchaseId })).AsList();
            items.Should().HaveCount(1);
            items[0].HistoricalPurchasePrice.Should().Be(95.0m);
            items[0].Quantity.Should().Be(20.0m);

            // 3. Verify Stock increment (50 + 20 = 70)
            var currentStock = await verifyConn.ExecuteScalarAsync<decimal>(
                "SELECT CurrentStock FROM Products WHERE ProductId = @ProductId;", new { ProductId = _testProductId });
            currentStock.Should().Be(70.0m);

            // 4. Verify StockMovement log
            var movement = await verifyConn.QuerySingleOrDefaultAsync<StockMovement>(
                "SELECT * FROM StockMovements WHERE ReferenceNumber = @Ref;", new { Ref = "PUR-2026-0001" });
            movement.Should().NotBeNull();
            movement!.MovementType.Should().Be("PURCHASE");
            movement.QuantityIn.Should().Be(20.0m);
            movement.BalanceQuantity.Should().Be(70.0m);

            // 5. Verify Supplier balance update (+1000 remaining payable)
            var suppBalance = await verifyConn.ExecuteScalarAsync<decimal>(
                "SELECT OutstandingBalance FROM Suppliers WHERE SupplierId = @SupplierId;", new { SupplierId = _testSupplierId });
            suppBalance.Should().Be(1000.0m);

            // 6. Verify SupplierLedger entry
            var ledger = await verifyConn.QuerySingleOrDefaultAsync<SupplierLedgerEntry>(
                "SELECT * FROM SupplierLedger WHERE ReferenceNumber = @Ref;", new { Ref = "PUR-2026-0001" });
            ledger.Should().NotBeNull();
            ledger!.Debit.Should().Be(900.0m);
            ledger.Credit.Should().Be(1900.0m);
            ledger.RunningBalance.Should().Be(1000.0m);

            // 7. Verify Supplier Pricing History snapshot
            var history = await verifyConn.QuerySingleOrDefaultAsync<SupplierProductPricingHistory>(
                "SELECT * FROM SupplierProductPricingHistory WHERE InvoiceNumber = @Inv;", new { Inv = "PUR-2026-0001" });
            history.Should().NotBeNull();
            history!.PurchasePrice.Should().Be(95.0m);
            history.Quantity.Should().Be(20.0m);
        }

        [Fact]
        public async Task PostPurchaseAsync_ShouldRollbackEntireTransaction_WhenSubsequentOperationFails()
        {
            // Arrange: Capture pre-transaction state
            using (var preConn = _factory.CreateConnection())
            {
                preConn.Open();
                var preStock = await preConn.ExecuteScalarAsync<decimal>(
                    "SELECT CurrentStock FROM Products WHERE ProductId = @ProductId;", new { ProductId = _testProductId });
                var preBalance = await preConn.ExecuteScalarAsync<decimal>(
                    "SELECT OutstandingBalance FROM Suppliers WHERE SupplierId = @SupplierId;", new { SupplierId = _testSupplierId });
                preStock.Should().Be(50.0m);
                preBalance.Should().Be(0.0m);
            }

            // Create a purchase with an invalid SupplierId (non-existent foreign key)
            var invalidPurchase = new Purchase
            {
                InvoiceNumber = "PUR-FAIL-001",
                SupplierId = 888888, // Foreign key violation
                PurchaseDate = DateTime.UtcNow,
                SubTotal = 500.0m,
                DiscountAmount = 0.0m,
                NetTotal = 500.0m,
                PaidAmount = 0.0m,
                RemainingBalance = 500.0m,
                Status = "Received",
                AdminId = _testAdminId,
                Remarks = "Intentional Purchase Failure Test"
            };

            invalidPurchase.Items.Add(new PurchaseItem
            {
                ProductId = _testProductId,
                Quantity = 15.0m,
                HistoricalPurchasePrice = 90.0m,
                LineTotal = 1350.0m
            });

            // Act
            Func<Task> act = async () => await _sut.PostPurchaseAsync(invalidPurchase, CancellationToken.None);

            // Assert: Must throw a real database/business failure (NOT NotImplementedException)
            var ex = await act.Should().ThrowAsync<Exception>();
            ex.Which.Should().NotBeOfType<NotImplementedException>("a genuine database or domain failure must trigger rollback, not an unimplemented stub");

            // Assert: Verify complete ACID rollback
            using var verifyConn = _factory.CreateConnection();
            verifyConn.Open();

            var purchaseCount = await verifyConn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Purchases WHERE InvoiceNumber = 'PUR-FAIL-001';");
            purchaseCount.Should().Be(0, "Purchase header must be rolled back");

            var purchaseItemsCount = await verifyConn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM PurchaseItems WHERE ProductId = @ProductId AND Quantity = 15.0;", new { ProductId = _testProductId });
            purchaseItemsCount.Should().Be(0, "Purchase items must be rolled back");

            var stockAfterFailure = await verifyConn.ExecuteScalarAsync<decimal>(
                "SELECT CurrentStock FROM Products WHERE ProductId = @ProductId;", new { ProductId = _testProductId });
            stockAfterFailure.Should().Be(50.0m, "Stock increment must be rolled back to pre-transaction value 50.0");

            var stockMovementsCount = await verifyConn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM StockMovements WHERE ReferenceNumber = 'PUR-FAIL-001';");
            stockMovementsCount.Should().Be(0, "Stock movements must be rolled back");

            var suppBalanceAfterFailure = await verifyConn.ExecuteScalarAsync<decimal>(
                "SELECT OutstandingBalance FROM Suppliers WHERE SupplierId = @SupplierId;", new { SupplierId = _testSupplierId });
            suppBalanceAfterFailure.Should().Be(0.0m, "Supplier balance must remain unchanged");

            var ledgerCount = await verifyConn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM SupplierLedger WHERE ReferenceNumber = 'PUR-FAIL-001';");
            ledgerCount.Should().Be(0, "Supplier ledger must have zero entries for failed purchase");

            var pricingHistoryCount = await verifyConn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM SupplierProductPricingHistory WHERE InvoiceNumber = 'PUR-FAIL-001';");
            pricingHistoryCount.Should().Be(0, "Supplier pricing history must not be recorded on failed transaction");
        }

        [Fact]
        public async Task PostSaleAsync_ShouldRecordPricingHistorySnapshot_WithHistoricalImmutability()
        {
            // Arrange
            var sale = new Sale
            {
                InvoiceNumber = "INV-IMMUTABLE-001",
                CustomerId = _testCustomerId,
                SaleDate = DateTime.UtcNow,
                SubTotal = 300.0m,
                DiscountAmount = 0.0m,
                NetTotal = 300.0m,
                PaidAmount = 300.0m,
                RemainingBalance = 0.0m,
                PaymentType = "Cash",
                Status = "Completed",
                AdminId = _testAdminId,
                Remarks = "Historical Immutability Test"
            };

            sale.Items.Add(new SaleItem
            {
                ProductId = _testProductId,
                Quantity = 3.0m,
                HistoricalSalePrice = 100.0m,
                HistoricalCostPrice = 80.0m,
                SellingPercentage = 25.0m,
                DiscountPercentage = 0.0m,
                LineTotal = 300.0m
            });

            // Act
            var saleId = await _sut.PostSaleAsync(sale, CancellationToken.None);

            // Mutate master product default price after sale is completed
            using (var modifyConn = _factory.CreateConnection())
            {
                modifyConn.Open();
                await modifyConn.ExecuteAsync(
                    "UPDATE Products SET DefaultSalePrice = 150.0, BasePurchasePrice = 110.0 WHERE ProductId = @ProductId;",
                    new { ProductId = _testProductId });
            }

            // Assert: Existing sale item snapshot must remain permanently unchanged
            using var verifyConn = _factory.CreateConnection();
            verifyConn.Open();

            var saleItem = await verifyConn.QuerySingleAsync<SaleItem>(
                "SELECT * FROM SaleItems WHERE SaleId = @SaleId;", new { SaleId = saleId });
            saleItem.HistoricalSalePrice.Should().Be(100.0m, "HistoricalSalePrice must NOT change when Product master price changes");
            saleItem.HistoricalCostPrice.Should().Be(80.0m, "HistoricalCostPrice must NOT change when Product master cost changes");
        }
    }
}
