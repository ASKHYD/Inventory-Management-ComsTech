using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using SOFT2027.Core.Entities;
using SOFT2027.Core.Interfaces;

namespace SOFT2027.Data.Services;

public sealed class InventoryCoordinator : IInventoryCoordinator
{
    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly IProductRepository _products;
    private readonly IStockMovementRepository _movements;

    public InventoryCoordinator(ISqliteConnectionFactory connectionFactory, IProductRepository products, IStockMovementRepository movements)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _products = products ?? throw new ArgumentNullException(nameof(products));
        _movements = movements ?? throw new ArgumentNullException(nameof(movements));
    }

    public async Task AdjustStockAsync(int productId, decimal quantityChange, int adminId, string reason, CancellationToken cancellationToken = default)
    {
        if (quantityChange == 0) throw new ArgumentOutOfRangeException(nameof(quantityChange), "Enter a stock increase or decrease.");
        if (adminId <= 0) throw new ArgumentOutOfRangeException(nameof(adminId), "A signed-in administrator is required.");
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Enter the reason for the stock adjustment.", nameof(reason));

        using var connection = _connectionFactory.CreateConnection();
        if (connection is DbConnection dbConnection) await dbConnection.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        try
        {
            var product = await _products.GetByIdAsync(productId, transaction, cancellationToken)
                ?? throw new InvalidOperationException("The selected product no longer exists.");
            var newStock = product.CurrentStock + quantityChange;
            if (newStock < 0) throw new InvalidOperationException("This adjustment would make stock negative. Reduce the decrease amount.");

            if (!await _products.UpdateStockAsync(productId, newStock, transaction, cancellationToken))
                throw new InvalidOperationException("Stock could not be updated.");
            var movement = new StockMovement
            {
                ProductId = productId, MovementDate = DateTime.Now, MovementType = "ADJUSTMENT",
                ReferenceNumber = $"ADJ-{DateTime.Now:yyyyMMddHHmmssfff}",
                QuantityIn = quantityChange > 0 ? quantityChange : 0,
                QuantityOut = quantityChange < 0 ? Math.Abs(quantityChange) : 0,
                BalanceQuantity = newStock, AdminId = adminId, Remarks = reason.Trim()
            };
            await _movements.AddAsync(movement, transaction, cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO AuditLogs (AdminId, Action, ModuleName, RecordReference, PreviousValue, NewValue)
                VALUES (@AdminId, 'UPDATE', 'Inventory', @Reference, @PreviousValue, @NewValue);",
                new { AdminId = adminId, Reference = movement.ReferenceNumber, PreviousValue = product.CurrentStock, NewValue = newStock },
                transaction, cancellationToken: cancellationToken));
            transaction.Commit();
        }
        catch
        {
            try { transaction.Rollback(); } catch { }
            throw;
        }
    }
}
