using System;
using System.IO;
using System.Linq;
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
    public sealed class SupplierPricingHistoryRepositoryTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly ISqliteConnectionFactory _factory;
        private readonly IDatabaseInitializer _initializer;
        private readonly ISupplierPricingHistoryRepository _sut;

        private int _testAdminId;
        private int _testSupplierId;
        private int _testProductId;

        public SupplierPricingHistoryRepositoryTests()
        {
            _testDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"test_sup_hist_{Guid.NewGuid():N}.db");
            var schemaPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "docs", "DATABASE_SCHEMA.sql"));

            _factory = new SqliteConnectionFactory(_testDbPath);
            _initializer = new DatabaseInitializer(_factory, schemaPath);
            _sut = new SupplierPricingHistoryRepository(_factory);

            _initializer.InitializeAsync().GetAwaiter().GetResult();
            SeedTestData();
        }

        private void SeedTestData()
        {
            using var connection = _factory.CreateConnection();
            connection.Open();

            _testAdminId = connection.ExecuteScalar<int>(
                "INSERT INTO AdminUsers (Username, PasswordHash, FullName) VALUES ('admin1', 'hash', 'Admin User'); SELECT last_insert_rowid();");

            _testSupplierId = connection.ExecuteScalar<int>(
                "INSERT INTO Suppliers (SupplierCode, SupplierName) VALUES ('SUP-001', 'Test Supplier'); SELECT last_insert_rowid();");

            _testProductId = connection.ExecuteScalar<int>(
                "INSERT INTO Products (ProductCode, ProductName, BasePurchasePrice, DefaultSalePrice) VALUES ('PRD-001', 'Test Product', 100, 120); SELECT last_insert_rowid();");
        }

        [Fact]
        public async Task AddAsync_ShouldSaveAndRetrieveLatestPricingRecord_ForSupplierProductPair()
        {
            // Arrange
            var record = new SupplierProductPricingHistory
            {
                SupplierId = _testSupplierId,
                ProductId = _testProductId,
                PurchasePrice = 800.00m,
                PurchasePercentage = 5.0m,
                DiscountPercentage = 1.0m,
                Quantity = 100.0m,
                InvoiceNumber = "BILL-2026-0001",
                TransactionDate = DateTime.UtcNow,
                AdminId = _testAdminId
            };

            // Act
            var id = await _sut.AddAsync(record);
            var latest = await _sut.GetLatestAsync(_testSupplierId, _testProductId);

            // Assert
            id.Should().BeGreaterThan(0);
            latest.Should().NotBeNull();
            latest!.SupplierId.Should().Be(_testSupplierId);
            latest.ProductId.Should().Be(_testProductId);
            latest.PurchasePrice.Should().Be(800.00m);
            latest.PurchasePercentage.Should().Be(5.0m);
            latest.DiscountPercentage.Should().Be(1.0m);
            latest.Quantity.Should().Be(100.0m);
            latest.InvoiceNumber.Should().Be("BILL-2026-0001");
        }

        [Fact]
        public async Task AddAsync_ShouldPreserveHistoricalRecords_AndNotOverwritePastPricing()
        {
            // Arrange: First purchase @ 500 PKR, second purchase @ 520 PKR
            var record1 = new SupplierProductPricingHistory
            {
                SupplierId = _testSupplierId,
                ProductId = _testProductId,
                PurchasePrice = 500.00m,
                PurchasePercentage = 5.0m,
                DiscountPercentage = 0.0m,
                Quantity = 50.0m,
                InvoiceNumber = "BILL-001",
                TransactionDate = DateTime.UtcNow.AddDays(-5),
                AdminId = _testAdminId
            };

            var record2 = new SupplierProductPricingHistory
            {
                SupplierId = _testSupplierId,
                ProductId = _testProductId,
                PurchasePrice = 520.00m,
                PurchasePercentage = 6.0m,
                DiscountPercentage = 1.0m,
                Quantity = 100.0m,
                InvoiceNumber = "BILL-002",
                TransactionDate = DateTime.UtcNow,
                AdminId = _testAdminId
            };

            // Act
            await _sut.AddAsync(record1);
            await _sut.AddAsync(record2);

            var latest = await _sut.GetLatestAsync(_testSupplierId, _testProductId);
            var history = await _sut.GetHistoryAsync(_testSupplierId, _testProductId);

            // Assert: Both records must exist in history; latest must be 520 PKR
            history.Should().HaveCount(2);
            history.Select(h => h.PurchasePrice).Should().Contain(new[] { 500.00m, 520.00m });

            latest.Should().NotBeNull();
            latest!.PurchasePrice.Should().Be(520.00m);
            latest.InvoiceNumber.Should().Be("BILL-002");
        }

        [Fact]
        public async Task GetLatestAsync_ShouldReturnNull_WhenNoHistoryExistsForPair()
        {
            // Act: Query a supplier/product combination that has never transacted
            var latest = await _sut.GetLatestAsync(_testSupplierId, 99999);

            // Assert: Must return null so system falls back to Tier 1 default purchase price
            latest.Should().BeNull();
        }

        [Fact]
        public async Task AddAsync_ShouldEnforceCheckConstraint_OnNegativePurchasePrice()
        {
            // Arrange
            var record = new SupplierProductPricingHistory
            {
                SupplierId = _testSupplierId,
                ProductId = _testProductId,
                PurchasePrice = -20.00m,
                Quantity = 10.0m,
                InvoiceNumber = "BILL-NEG",
                TransactionDate = DateTime.UtcNow,
                AdminId = _testAdminId
            };

            // Act
            Func<Task> action = async () => await _sut.AddAsync(record);

            // Assert: CHECK(PurchasePrice >= 0)
            await action.Should().ThrowAsync<SqliteException>()
                .WithMessage("*CHECK constraint failed*");
        }

        [Fact]
        public async Task AddAsync_ShouldRespectCancellationToken()
        {
            // Arrange
            var record = new SupplierProductPricingHistory
            {
                SupplierId = _testSupplierId,
                ProductId = _testProductId,
                PurchasePrice = 100.00m,
                Quantity = 10.0m,
                InvoiceNumber = "BILL-CANCEL",
                TransactionDate = DateTime.UtcNow,
                AdminId = _testAdminId
            };
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            Func<Task> action = async () => await _sut.AddAsync(record, cts.Token);

            // Assert
            await action.Should().ThrowAsync<OperationCanceledException>();
        }

        public void Dispose()
        {
            if (File.Exists(_testDbPath))
            {
                try
                {
                    File.Delete(_testDbPath);
                }
                catch
                {
                    // Ignore transient lock cleanup in test
                }
            }
        }
    }
}
