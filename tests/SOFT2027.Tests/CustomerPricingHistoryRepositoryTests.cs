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
    public sealed class CustomerPricingHistoryRepositoryTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly ISqliteConnectionFactory _factory;
        private readonly IDatabaseInitializer _initializer;
        private readonly ICustomerPricingHistoryRepository _sut;

        private int _testAdminId;
        private int _testCustomerId;
        private int _testProductId;

        public CustomerPricingHistoryRepositoryTests()
        {
            _testDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"test_cust_hist_{Guid.NewGuid():N}.db");
            var schemaPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "docs", "DATABASE_SCHEMA.sql"));

            _factory = new SqliteConnectionFactory(_testDbPath);
            _initializer = new DatabaseInitializer(_factory, schemaPath);
            _sut = new CustomerPricingHistoryRepository(_factory);

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
                "INSERT INTO Customers (CustomerCode, CustomerName) VALUES ('CUST-001', 'Test Customer'); SELECT last_insert_rowid();");

            _testProductId = connection.ExecuteScalar<int>(
                "INSERT INTO Products (ProductCode, ProductName, BasePurchasePrice, DefaultSalePrice) VALUES ('PRD-001', 'Test Product', 100, 120); SELECT last_insert_rowid();");
        }

        [Fact]
        public async Task AddAsync_ShouldSaveAndRetrieveLatestPricingRecord_ForCustomerProductPair()
        {
            // Arrange
            var record = new CustomerProductPricingHistory
            {
                CustomerId = _testCustomerId,
                ProductId = _testProductId,
                SalePrice = 540.00m,
                SellingPercentage = 8.0m,
                DiscountPercentage = 2.0m,
                Quantity = 10.0m,
                InvoiceNumber = "INV-2026-0001",
                TransactionDate = DateTime.UtcNow,
                AdminId = _testAdminId
            };

            // Act
            var id = await _sut.AddAsync(record);
            var latest = await _sut.GetLatestAsync(_testCustomerId, _testProductId);

            // Assert
            id.Should().BeGreaterThan(0);
            latest.Should().NotBeNull();
            latest!.CustomerId.Should().Be(_testCustomerId);
            latest.ProductId.Should().Be(_testProductId);
            latest.SalePrice.Should().Be(540.00m);
            latest.SellingPercentage.Should().Be(8.0m);
            latest.DiscountPercentage.Should().Be(2.0m);
            latest.Quantity.Should().Be(10.0m);
            latest.InvoiceNumber.Should().Be("INV-2026-0001");
        }

        [Fact]
        public async Task AddAsync_ShouldPreserveHistoricalRecords_AndNotOverwritePastPricing()
        {
            // Arrange: First transaction @ 540 PKR, Second transaction @ 550 PKR
            var record1 = new CustomerProductPricingHistory
            {
                CustomerId = _testCustomerId,
                ProductId = _testProductId,
                SalePrice = 540.00m,
                SellingPercentage = 8.0m,
                DiscountPercentage = 2.0m,
                Quantity = 10.0m,
                InvoiceNumber = "INV-001",
                TransactionDate = DateTime.UtcNow.AddDays(-2),
                AdminId = _testAdminId
            };

            var record2 = new CustomerProductPricingHistory
            {
                CustomerId = _testCustomerId,
                ProductId = _testProductId,
                SalePrice = 550.00m,
                SellingPercentage = 10.0m,
                DiscountPercentage = 0.0m,
                Quantity = 25.0m,
                InvoiceNumber = "INV-002",
                TransactionDate = DateTime.UtcNow,
                AdminId = _testAdminId
            };

            // Act
            await _sut.AddAsync(record1);
            await _sut.AddAsync(record2);

            var latest = await _sut.GetLatestAsync(_testCustomerId, _testProductId);
            var history = await _sut.GetHistoryAsync(_testCustomerId, _testProductId);

            // Assert: Historical Immutability Guarantee
            // 1. Both records must exist permanently in history
            history.Should().HaveCount(2);
            history.Select(h => h.SalePrice).Should().Contain(new[] { 540.00m, 550.00m });

            // 2. The most recent record is recalled for the next transaction
            latest.Should().NotBeNull();
            latest!.SalePrice.Should().Be(550.00m);
            latest.InvoiceNumber.Should().Be("INV-002");
        }

        [Fact]
        public async Task GetLatestAsync_ShouldReturnNull_WhenNoHistoryExistsForPair()
        {
            // Act: Query a customer/product combination that has never transacted
            var latest = await _sut.GetLatestAsync(_testCustomerId, 99999);

            // Assert: Must return null so system can fall back to Tier 1 default pricing
            latest.Should().BeNull();
        }

        [Fact]
        public async Task AddAsync_ShouldEnforceCheckConstraint_OnNegativeSalePrice()
        {
            // Arrange
            var record = new CustomerProductPricingHistory
            {
                CustomerId = _testCustomerId,
                ProductId = _testProductId,
                SalePrice = -50.00m,
                Quantity = 5.0m,
                InvoiceNumber = "INV-NEG",
                TransactionDate = DateTime.UtcNow,
                AdminId = _testAdminId
            };

            // Act
            Func<Task> action = async () => await _sut.AddAsync(record);

            // Assert: CHECK(SalePrice >= 0)
            await action.Should().ThrowAsync<SqliteException>()
                .WithMessage("*CHECK constraint failed*");
        }

        [Theory]
        [InlineData(99999, 1)] // Non-existent customer
        [InlineData(1, 99999)] // Non-existent product
        public async Task AddAsync_ShouldEnforceForeignKeyConstraint_OnInvalidCustomerOrProduct(int customerId, int productId)
        {
            // Arrange
            var record = new CustomerProductPricingHistory
            {
                CustomerId = customerId,
                ProductId = productId,
                SalePrice = 100.00m,
                Quantity = 1.0m,
                InvoiceNumber = "INV-FK",
                TransactionDate = DateTime.UtcNow,
                AdminId = _testAdminId
            };

            // Act
            Func<Task> action = async () => await _sut.AddAsync(record);

            // Assert: Relational integrity enforced
            await action.Should().ThrowAsync<SqliteException>()
                .WithMessage("*FOREIGN KEY constraint failed*");
        }

        [Fact]
        public async Task AddAsync_ShouldRespectCancellationToken()
        {
            // Arrange
            var record = new CustomerProductPricingHistory
            {
                CustomerId = _testCustomerId,
                ProductId = _testProductId,
                SalePrice = 100.00m,
                Quantity = 1.0m,
                InvoiceNumber = "INV-CANCEL",
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
