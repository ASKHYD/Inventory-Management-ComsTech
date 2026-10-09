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
    public sealed class SupplierRepositoryTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly ISqliteConnectionFactory _factory;
        private readonly IDatabaseInitializer _initializer;
        private readonly ISupplierRepository _sut;

        public SupplierRepositoryTests()
        {
            _testDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"test_suppliers_{Guid.NewGuid():N}.db");
            var schemaPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "docs", "DATABASE_SCHEMA.sql"));
            
            _factory = new SqliteConnectionFactory(_testDbPath);
            _initializer = new DatabaseInitializer(_factory, schemaPath);
            _sut = new SupplierRepository(_factory);

            _initializer.InitializeAsync().GetAwaiter().GetResult();
        }

        [Fact]
        public async Task AddAsync_ShouldSaveAndRetrieveSupplier_WithOpeningBalance()
        {
            // Arrange
            var supplier = new Supplier
            {
                SupplierCode = "SUP-001",
                SupplierName = "ABC FMCG Distributors Ltd",
                ContactPerson = "Tariq Mahmood",
                Phone = "042-35891234",
                Address = "Industrial Area, Gate 2",
                OpeningBalance = 75000.00m,
                OutstandingBalance = 75000.00m,
                Status = "Active"
            };

            // Act
            var id = await _sut.AddAsync(supplier);
            var retrieved = await _sut.GetByCodeAsync("SUP-001");

            // Assert
            id.Should().BeGreaterThan(0);
            retrieved.Should().NotBeNull();
            retrieved!.SupplierCode.Should().Be("SUP-001");
            retrieved.SupplierName.Should().Be("ABC FMCG Distributors Ltd");
            retrieved.ContactPerson.Should().Be("Tariq Mahmood");
            retrieved.OpeningBalance.Should().Be(75000.00m);
            retrieved.OutstandingBalance.Should().Be(75000.00m);
            retrieved.Status.Should().Be("Active");
        }

        [Fact]
        public async Task AddAsync_ShouldRejectDuplicateSupplierCodes()
        {
            // Arrange
            var supplier1 = new Supplier { SupplierCode = "DUP-SUP", SupplierName = "Supplier 1", OpeningBalance = 500 };
            var supplier2 = new Supplier { SupplierCode = "DUP-SUP", SupplierName = "Supplier 2", OpeningBalance = 600 };
            await _sut.AddAsync(supplier1);

            // Act
            Func<Task> action = async () => await _sut.AddAsync(supplier2);

            // Assert: SQLite UNIQUE constraint violation
            await action.Should().ThrowAsync<SqliteException>()
                .WithMessage("*UNIQUE constraint failed: Suppliers.SupplierCode*");
        }

        [Theory]
        [InlineData("InvalidStatus")]
        [InlineData("Pending")]
        [InlineData("Terminated")]
        public async Task AddAsync_ShouldEnforceCheckConstraint_OnStatus(string invalidStatus)
        {
            // Arrange: Status CHECK IN ('Active', 'Inactive')
            var supplier = new Supplier
            {
                SupplierCode = "STAT-SUP",
                SupplierName = "Status Test Supplier",
                Status = invalidStatus
            };

            // Act
            Func<Task> action = async () => await _sut.AddAsync(supplier);

            // Assert: SQLite CHECK constraint violation
            await action.Should().ThrowAsync<SqliteException>()
                .WithMessage("*CHECK constraint failed*");
        }

        [Fact]
        public async Task AddAsync_ShouldRespectCancellationToken()
        {
            // Arrange
            var supplier = new Supplier { SupplierCode = "CANCEL-SUP", SupplierName = "Cancel Supplier" };
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            Func<Task> action = async () => await _sut.AddAsync(supplier, cts.Token);

            // Assert: Clean OperationCanceledException propagation
            await action.Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public async Task UpdateBalanceAsync_ShouldUpdateSupplierOutstandingBalance()
        {
            // Arrange
            var supplier = new Supplier
            {
                SupplierCode = "BAL-SUP-01",
                SupplierName = "Balance Test Supplier",
                OpeningBalance = 20000.00m,
                OutstandingBalance = 20000.00m
            };
            var id = await _sut.AddAsync(supplier);

            // Act
            var success = await _sut.UpdateBalanceAsync(id, 32000.00m);
            var retrieved = await _sut.GetByIdAsync(id);

            // Assert
            success.Should().BeTrue();
            retrieved.Should().NotBeNull();
            retrieved!.OutstandingBalance.Should().Be(32000.00m);
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
