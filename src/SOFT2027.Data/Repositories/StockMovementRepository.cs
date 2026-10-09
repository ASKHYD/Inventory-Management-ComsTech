using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using SOFT2027.Core.Entities;
using SOFT2027.Core.Interfaces;

namespace SOFT2027.Data.Repositories
{
    public class StockMovementRepository : IStockMovementRepository
    {
        private readonly ISqliteConnectionFactory _connectionFactory;

        public StockMovementRepository(ISqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public async Task<int> AddAsync(StockMovement movement, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
        {
            if (movement == null)
            {
                throw new ArgumentNullException(nameof(movement));
            }

            cancellationToken.ThrowIfCancellationRequested();

            const string sql = @"
                INSERT INTO StockMovements (
                    ProductId, MovementDate, MovementType, ReferenceNumber, 
                    QuantityIn, QuantityOut, BalanceQuantity, AdminId, Remarks
                ) VALUES (
                    @ProductId, @MovementDate, @MovementType, @ReferenceNumber, 
                    @QuantityIn, @QuantityOut, @BalanceQuantity, @AdminId, @Remarks
                );
                SELECT last_insert_rowid();";

            var connection = transaction?.Connection ?? _connectionFactory.CreateConnection();
            var shouldDispose = transaction == null;

            try
            {
                if (shouldDispose && connection is DbConnection dbConnection)
                {
                    await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
                }

                var cmd = new CommandDefinition(
                    sql,
                    new
                    {
                        movement.ProductId,
                        MovementDate = movement.MovementDate == default ? DateTime.UtcNow : movement.MovementDate,
                        movement.MovementType,
                        movement.ReferenceNumber,
                        movement.QuantityIn,
                        movement.QuantityOut,
                        movement.BalanceQuantity,
                        movement.AdminId,
                        movement.Remarks
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken);

                var movementId = await connection.ExecuteScalarAsync<int>(cmd).ConfigureAwait(false);
                movement.MovementId = movementId;
                return movementId;
            }
            finally
            {
                if (shouldDispose)
                {
                    connection.Dispose();
                }
            }
        }

        public async Task<IReadOnlyList<StockMovement>> GetByProductIdAsync(int productId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            const string sql = "SELECT * FROM StockMovements WHERE ProductId = @ProductId ORDER BY MovementDate DESC, MovementId DESC;";

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            var cmd = new CommandDefinition(sql, new { ProductId = productId }, cancellationToken: cancellationToken);
            var movements = await connection.QueryAsync<StockMovement>(cmd).ConfigureAwait(false);
            return movements.AsList();
        }

        public async Task<decimal> GetCurrentStockAsync(int productId, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            const string sql = "SELECT CurrentStock FROM Products WHERE ProductId = @ProductId;";

            var connection = transaction?.Connection ?? _connectionFactory.CreateConnection();
            var shouldDispose = transaction == null;

            try
            {
                if (shouldDispose && connection is DbConnection dbConnection)
                {
                    await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
                }

                var cmd = new CommandDefinition(sql, new { ProductId = productId }, transaction: transaction, cancellationToken: cancellationToken);
                return await connection.ExecuteScalarAsync<decimal>(cmd).ConfigureAwait(false);
            }
            finally
            {
                if (shouldDispose)
                {
                    connection.Dispose();
                }
            }
        }
    }
}
