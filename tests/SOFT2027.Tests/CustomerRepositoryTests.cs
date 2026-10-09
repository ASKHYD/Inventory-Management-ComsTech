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
    public sealed class CustomerRepositoryTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly ISqliteConnectionFactory _factory;
        private readonly IDatabaseInitializer _initializer;
        private readonly ICustomerRepository _sut;

        public CustomerRepositoryTests()
        {
            _testDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"test_customers_{Guid.NewGuid():N}.db");
            var schemaPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "docs", "DATABASE_SCHEMA.sql"));
            
            _factory = new SqliteConnectionFactory(_testDbPath);
            _initializer = new DatabaseInitializer(_factory, schemaPath);
            _sut = new CustomerRepository(_factory);

            _initializer.InitializeAsync().GetAwaiter().GetResult();
        }

        [Fact]
        public async Task AddAsync_ShouldSaveAndRetrieveCustomer_WithOpeningBalance()
        {
            // Arrange
            var customer = new Customer
            {
                CustomerCode = "CUST-001",
                CustomerName = "Al-Rehman Superstore",
                ContactPerson = "Muhammad Ahmed",
                Phone = "0300-1234567",
                Address = "Main Bazaar, Block 4",
                AreaTerritory = "North Zone",
                OpeningBalance = 50000.00m,
                OutstandingBalance = 50000.00m,
                CreditLimit = 100000.00m,
                Status = "Active"
            };

            // Act
            var id = await _sut.AddAsync(customer);
            var retrieved = await _sut.GetByCodeAsync("CUST-001");

            // Assert
            id.Should().BeGreaterThan(0);
            retrieved.Should().NotBeNull();
            retrieved!.CustomerCode.Should().Be("CUST-001");
            retrieved.CustomerName.Should().Be("Al-Rehman Superstore");
            retrieved.ContactPerson.Should().Be("Muhammad Ahmed");
            retrieved.OpeningBalance.Should().Be(50000.00m);
            retrieved.OutstandingBalance.Should().Be(50000.00m);
            retrieved.CreditLimit.Should().Be(100000.00m);
            retrieved.Status.Should().Be("Active");
        }

        [Fact]
        public async Task AddAsync_ShouldRejectDuplicateCustomerCodes()
        {
            // Arrange
            var customer1 = new Customer { CustomerCode = "DUP-CUST", CustomerName = "Store 1", OpeningBalance = 100 };
            var customer2 = new Customer { CustomerCode = "DUP-CUST", CustomerName = "Store 2", OpeningBalance = 200 };
            await _sut.AddAsync(customer1);

            // Act
            Func<Task> action = async () => await _sut.AddAsync(customer2);

            // Assert: SQLite UNIQUE constraint violation
            await action.Should().ThrowAsync<SqliteException>()
                .WithMessage("*UNIQUE constraint failed: Customers.CustomerCode*");
        }

        [Theory]
        [InlineData("InvalidStatus")]
        [InlineData("Archived")]
        [InlineData("Suspended")]
        public async Task AddAsync_ShouldEnforceCheckConstraint_OnStatus(string invalidStatus)
        {
            // Arrange: Status CHECK IN ('Active', 'Inactive')
            var customer = new Customer
            {
                CustomerCode = "STAT-CUST",
                CustomerName = "Status Test Customer",
                Status = invalidStatus
            };

            // Act
            Func<Task> action = async () => await _sut.AddAsync(customer);

            // Assert: SQLite CHECK constraint violation
            await action.Should().ThrowAsync<SqliteException>()
                .WithMessage("*CHECK constraint failed*");
        }

        [Fact]
        public async Task AddAsync_ShouldRespectCancellationToken()
        {
            // Arrange
            var customer = new Customer { CustomerCode = "CANCEL-CUST", CustomerName = "Cancel Customer" };
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            Func<Task> action = async () => await _sut.AddAsync(customer, cts.Token);

            // Assert: Clean OperationCanceledException propagation
            await action.Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public async Task UpdateBalanceAsync_ShouldUpdateCustomerOutstandingBalance()
        {
            // Arrange
            var customer = new Customer
            {
                CustomerCode = "BAL-001",
                CustomerName = "Balance Test Customer",
                OpeningBalance = 1000.00m,
                OutstandingBalance = 1000.00m
            };
            var id = await _sut.AddAsync(customer);

            // Act
            var success = await _sut.UpdateBalanceAsync(id, 2500.00m);
            var retrieved = await _sut.GetByIdAsync(id);

            // Assert
            success.Should().BeTrue();
            retrieved.Should().NotBeNull();
            retrieved!.OutstandingBalance.Should().Be(2500.00m);
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
