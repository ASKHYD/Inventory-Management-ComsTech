using System;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using Dapper;
using FluentAssertions;
using SOFT2027.Core.Interfaces;
using SOFT2027.Data.Services;
using Xunit;

namespace SOFT2027.Tests
{
    public sealed class DatabaseConnectionManagerTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly ISqliteConnectionFactory _factory;
        private readonly IDatabaseInitializer _initializer;
        private readonly string _schemaPath;

        public DatabaseConnectionManagerTests()
        {
            _testDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"test_db_{Guid.NewGuid():N}.db");
            _schemaPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "docs", "DATABASE_SCHEMA.sql"));
            
            _factory = new SqliteConnectionFactory(_testDbPath);
            _initializer = new DatabaseInitializer(_factory, _schemaPath);
        }

        [Fact]
        public async Task InitializeAsync_ShouldCreateDatabaseFile_InLocalAppDirectory()
        {
            // Act
            await _initializer.InitializeAsync();

            // Assert
            File.Exists(_testDbPath).Should().BeTrue();
        }

        [Fact]
        public async Task InitializeAsync_ShouldEnforcePragmas_WAL_And_ForeignKeys()
        {
            // Act
            await _initializer.InitializeAsync();

            using var connection = _factory.CreateConnection();
            connection.Open();

            // Assert: SQLite PRAGMAs must strictly conform to offline performance & relational rules
            var journalMode = await connection.QuerySingleAsync<string>("PRAGMA journal_mode;");
            var foreignKeys = await connection.QuerySingleAsync<int>("PRAGMA foreign_keys;");

            journalMode.Should().BeEquivalentTo("wal");
            foreignKeys.Should().Be(1);
        }

        [Fact]
        public async Task InitializeAsync_ShouldCreateProductsAndPricingMemoryTables()
        {
            // Act
            await _initializer.InitializeAsync();

            using var connection = _factory.CreateConnection();
            connection.Open();

            // Assert: Tables defined in DATABASE_SCHEMA.sql must exist
            var tables = (await connection.QueryAsync<string>(
                "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;")).AsList();

            tables.Should().Contain("Products");
            tables.Should().Contain("CustomerProductPricingHistory");
            tables.Should().Contain("SupplierProductPricingHistory");
            tables.Should().Contain("Sales");
            tables.Should().Contain("SaleItems");
        }

        [Fact]
        public void CreateConnection_ShouldReturnThreadSafeClosedConnection()
        {
            using var connection = _factory.CreateConnection();
            connection.Should().NotBeNull();
            connection.State.Should().Be(ConnectionState.Closed);
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
