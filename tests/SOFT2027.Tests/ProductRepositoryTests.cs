using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using SOFT2027.Core.Entities;
using SOFT2027.Core.Interfaces;
using SOFT2027.Data.Repositories;
using SOFT2027.Data.Services;
using Xunit;

namespace SOFT2027.Tests
{
    public sealed class ProductRepositoryTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly ISqliteConnectionFactory _factory;
        private readonly IDatabaseInitializer _initializer;
        private readonly IProductRepository _sut;

        public ProductRepositoryTests()
        {
            _testDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"test_products_{Guid.NewGuid():N}.db");
            var schemaPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "docs", "DATABASE_SCHEMA.sql"));
            
            _factory = new SqliteConnectionFactory(_testDbPath);
            _initializer = new DatabaseInitializer(_factory, schemaPath);
            _sut = new ProductRepository(_factory);

            _initializer.InitializeAsync().GetAwaiter().GetResult();
        }

        [Fact]
        public async Task AddAsync_ShouldSaveAndRetrieveProduct_WithTier1DefaultPricing()
        {
            // Arrange
            var product = new Product
            {
                ProductCode = "PRD-001",
                ProductName = "Nestle MilkPak 1L",
                BasePurchasePrice = 280.00m,
                PurchasePercentage = 4.0m,
                DefaultSalePrice = 300.00m,
                SellingPercentage = 7.14m,
                MinimumStock = 10.0m,
                Status = "Active"
            };

            // Act
            var id = await _sut.AddAsync(product);
            var retrieved = await _sut.GetByCodeAsync("PRD-001");

            // Assert
            id.Should().BeGreaterThan(0);
            retrieved.Should().NotBeNull();
            retrieved!.ProductCode.Should().Be("PRD-001");
            retrieved.ProductName.Should().Be("Nestle MilkPak 1L");
            retrieved.BasePurchasePrice.Should().Be(280.00m);
            retrieved.DefaultSalePrice.Should().Be(300.00m);
            retrieved.Status.Should().Be("Active");
        }

        [Fact]
        public async Task AddAsync_ShouldRejectDuplicateProductCodes()
        {
            // Arrange
            var product1 = new Product { ProductCode = "DUP-001", ProductName = "Item 1", BasePurchasePrice = 100, DefaultSalePrice = 110 };
            var product2 = new Product { ProductCode = "DUP-001", ProductName = "Item 2", BasePurchasePrice = 105, DefaultSalePrice = 115 };
            await _sut.AddAsync(product1);

            // Act
            Func<Task> action = async () => await _sut.AddAsync(product2);

            // Assert: SQLite UNIQUE constraint violation
            await action.Should().ThrowAsync<SqliteException>()
                .WithMessage("*UNIQUE constraint failed: Products.ProductCode*");
        }

        [Fact]
        public async Task AddAsync_ShouldRejectDuplicateBarcodes()
        {
            // Arrange
            var product1 = new Product { ProductCode = "BC-001", ProductName = "Item 1", Barcode = "8964000123456", BasePurchasePrice = 50, DefaultSalePrice = 60 };
            var product2 = new Product { ProductCode = "BC-002", ProductName = "Item 2", Barcode = "8964000123456", BasePurchasePrice = 55, DefaultSalePrice = 65 };
            await _sut.AddAsync(product1);

            // Act
            Func<Task> action = async () => await _sut.AddAsync(product2);

            // Assert: SQLite UNIQUE constraint violation
            await action.Should().ThrowAsync<SqliteException>()
                .WithMessage("*UNIQUE constraint failed: Products.Barcode*");
        }

        [Fact]
        public async Task AddAsync_ShouldEnforceForeignKeyConstraint_WhenInvalidCategoryIdProvided()
        {
            // Arrange: CategoryId 99999 does not exist in Categories table
            var product = new Product
            {
                ProductCode = "FK-001",
                ProductName = "Invalid Category Product",
                CategoryId = 99999,
                BasePurchasePrice = 50,
                DefaultSalePrice = 60
            };

            // Act
            Func<Task> action = async () => await _sut.AddAsync(product);

            // Assert: PRAGMA foreign_keys = ON must prevent insertion
            await action.Should().ThrowAsync<SqliteException>()
                .WithMessage("*FOREIGN KEY constraint failed*");
        }

        [Fact]
        public async Task AddAsync_ShouldApplyDefaultValues_ForStockAndPercentages()
        {
            // Arrange: Stock and percentages omitted
            var product = new Product
            {
                ProductCode = "DEF-001",
                ProductName = "Default Values Product",
                BasePurchasePrice = 100,
                DefaultSalePrice = 120
            };

            // Act
            await _sut.AddAsync(product);
            var retrieved = await _sut.GetByCodeAsync("DEF-001");

            // Assert: Check default values mandated by DATABASE_SCHEMA.sql
            retrieved.Should().NotBeNull();
            retrieved!.CurrentStock.Should().Be(0.0m);
            retrieved.MinimumStock.Should().Be(0.0m);
            retrieved.PurchasePercentage.Should().Be(0.0m);
            retrieved.SellingPercentage.Should().Be(0.0m);
        }

        [Theory]
        [InlineData(-10.0, 100.0)]
        [InlineData(100.0, -10.0)]
        public async Task AddAsync_ShouldEnforceCheckConstraint_OnNegativePrices(decimal purchasePrice, decimal salePrice)
        {
            // Arrange
            var product = new Product
            {
                ProductCode = "NEG-001",
                ProductName = "Negative Price Product",
                BasePurchasePrice = purchasePrice,
                DefaultSalePrice = salePrice
            };

            // Act
            Func<Task> action = async () => await _sut.AddAsync(product);

            // Assert: SQLite CHECK constraint violation
            await action.Should().ThrowAsync<SqliteException>()
                .WithMessage("*CHECK constraint failed*");
        }

        [Theory]
        [InlineData("Pending")]
        [InlineData("Deleted")]
        [InlineData("Disabled")]
        public async Task AddAsync_ShouldEnforceCheckConstraint_OnStatus(string invalidStatus)
        {
            // Arrange: Schema strictly requires CHECK(Status IN ('Active', 'Inactive'))
            var product = new Product
            {
                ProductCode = "STAT-001",
                ProductName = "Invalid Status Product",
                BasePurchasePrice = 50,
                DefaultSalePrice = 60,
                Status = invalidStatus
            };

            // Act
            Func<Task> action = async () => await _sut.AddAsync(product);

            // Assert: SQLite CHECK constraint violation
            await action.Should().ThrowAsync<SqliteException>()
                .WithMessage("*CHECK constraint failed*");
        }

        [Fact]
        public async Task AddAsync_ShouldRespectCancellationToken()
        {
            // Arrange
            var product = new Product
            {
                ProductCode = "CANCEL-001",
                ProductName = "Cancellation Test Product",
                BasePurchasePrice = 50,
                DefaultSalePrice = 60
            };
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            Func<Task> action = async () => await _sut.AddAsync(product, cts.Token);

            // Assert: Async method propagates OperationCanceledException cleanly
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
