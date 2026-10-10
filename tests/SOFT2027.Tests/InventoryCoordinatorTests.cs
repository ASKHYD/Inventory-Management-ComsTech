using System.IO;
using Dapper;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using SOFT2027.Data.Services;
using SOFT2027.Data.Repositories;

namespace SOFT2027.Tests;

public sealed class InventoryCoordinatorTests : IAsyncLifetime
{
    private readonly string _path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"test_inventory_{Guid.NewGuid():N}.db");
    private string ConnectionString => $"Data Source={_path};Foreign Keys=True;";
    private SqliteConnectionFactory _factory = null!;
    private int _adminId;
    private int _productId;

    public async Task InitializeAsync()
    {
        _factory = new SqliteConnectionFactory(_path);
        var schema = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "docs", "DATABASE_SCHEMA.sql"));
        await new DatabaseInitializer(_factory, schema).InitializeAsync();
        using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        _adminId = await connection.ExecuteScalarAsync<int>("INSERT INTO AdminUsers (Username,PasswordHash,FullName) VALUES ('admin','hash','Admin'); SELECT last_insert_rowid();");
        _productId = await connection.ExecuteScalarAsync<int>("INSERT INTO Products (ProductCode,ProductName,BasePurchasePrice,DefaultSalePrice,CurrentStock,MinimumStock) VALUES ('P-1','Product',5,10,2.5,1); SELECT last_insert_rowid();");
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_path)) File.Delete(_path);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task AdjustStockAsync_ShouldRecordFractionalIncreaseAndReasonAndRejectNegativeResultAtomically()
    {
        var coordinator = new InventoryCoordinator(_factory, new ProductRepository(_factory), new StockMovementRepository(_factory));
        await coordinator.AdjustStockAsync(_productId, 0.75m, _adminId, "Counted an additional opened carton");
        Func<Task> invalid = () => coordinator.AdjustStockAsync(_productId, -4m, _adminId, "Bad count correction");
        await invalid.Should().ThrowAsync<InvalidOperationException>().WithMessage("*negative*");

        using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        (await connection.ExecuteScalarAsync<decimal>("SELECT CurrentStock FROM Products WHERE ProductId=@Id", new { Id = _productId })).Should().Be(3.25m);
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM StockMovements WHERE ProductId=@Id AND MovementType='ADJUSTMENT'", new { Id = _productId })).Should().Be(1);
        (await connection.ExecuteScalarAsync<decimal>("SELECT QuantityIn FROM StockMovements WHERE ProductId=@Id AND MovementType='ADJUSTMENT'", new { Id = _productId })).Should().Be(0.75m);
        (await connection.ExecuteScalarAsync<string>("SELECT Remarks FROM StockMovements WHERE ProductId=@Id AND MovementType='ADJUSTMENT'", new { Id = _productId })).Should().Be("Counted an additional opened carton");
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AuditLogs WHERE ModuleName='Inventory' AND Action='UPDATE'", new { Id = _productId })).Should().Be(1);
    }
}
