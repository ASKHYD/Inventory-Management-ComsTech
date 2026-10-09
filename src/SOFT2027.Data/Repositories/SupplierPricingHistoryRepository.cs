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
    public sealed class SupplierPricingHistoryRepository : ISupplierPricingHistoryRepository
    {
        private readonly ISqliteConnectionFactory _connectionFactory;

        public SupplierPricingHistoryRepository(ISqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public Task<int> AddAsync(SupplierProductPricingHistory history, CancellationToken cancellationToken = default) =>
            AddAsync(history, null, cancellationToken);

        public async Task<int> AddAsync(SupplierProductPricingHistory history, IDbTransaction? transaction, CancellationToken cancellationToken = default)
        {
            if (history == null)
            {
                throw new ArgumentNullException(nameof(history));
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Strict Historical Immutability Guarantee:
            // ALWAYS performs an INSERT. Supplier purchase history is strictly append-only.
            const string sql = @"
                INSERT INTO SupplierProductPricingHistory (
                    SupplierId, ProductId, PurchasePrice, PurchasePercentage, DiscountPercentage,
                    Quantity, InvoiceNumber, TransactionDate, AdminId
                ) VALUES (
                    @SupplierId, @ProductId, @PurchasePrice, @PurchasePercentage, @DiscountPercentage,
                    @Quantity, @InvoiceNumber, @TransactionDate, @AdminId
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

                var command = new CommandDefinition(
                    sql,
                    new
                    {
                        history.SupplierId,
                        history.ProductId,
                        history.PurchasePrice,
                        history.PurchasePercentage,
                        history.DiscountPercentage,
                        history.Quantity,
                        InvoiceNumber = history.InvoiceNumber ?? string.Empty,
                        TransactionDate = history.TransactionDate == default ? DateTime.UtcNow : history.TransactionDate,
                        history.AdminId
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken);

                var id = await connection.ExecuteScalarAsync<int>(command).ConfigureAwait(false);
                history.HistoryId = id;
                return id;
            }
            finally
            {
                if (shouldDispose)
                {
                    connection.Dispose();
                }
            }
        }

        public async Task<SupplierProductPricingHistory?> GetLatestAsync(int supplierId, int productId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // High Performance O(1) query backed by compound index:
            // IDX_SuppPricing_Lookup ON SupplierProductPricingHistory(SupplierId, ProductId, TransactionDate DESC)
            const string sql = @"
                SELECT HistoryId, SupplierId, ProductId, PurchasePrice, PurchasePercentage, DiscountPercentage,
                       Quantity, InvoiceNumber, TransactionDate, AdminId, CreatedAt
                FROM SupplierProductPricingHistory
                WHERE SupplierId = @SupplierId AND ProductId = @ProductId
                ORDER BY TransactionDate DESC, HistoryId DESC
                LIMIT 1;";

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            var command = new CommandDefinition(
                sql,
                new { SupplierId = supplierId, ProductId = productId },
                cancellationToken: cancellationToken);

            return await connection.QuerySingleOrDefaultAsync<SupplierProductPricingHistory>(command).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<SupplierProductPricingHistory>> GetHistoryAsync(int supplierId, int productId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            const string sql = @"
                SELECT HistoryId, SupplierId, ProductId, PurchasePrice, PurchasePercentage, DiscountPercentage,
                       Quantity, InvoiceNumber, TransactionDate, AdminId, CreatedAt
                FROM SupplierProductPricingHistory
                WHERE SupplierId = @SupplierId AND ProductId = @ProductId
                ORDER BY TransactionDate DESC, HistoryId DESC;";

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            var command = new CommandDefinition(
                sql,
                new { SupplierId = supplierId, ProductId = productId },
                cancellationToken: cancellationToken);

            var results = await connection.QueryAsync<SupplierProductPricingHistory>(command).ConfigureAwait(false);
            return results.AsList();
        }

        public async Task<IReadOnlyList<SupplierProductPricingHistory>> GetProductHistoryAsync(int productId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            const string sql = @"
                SELECT HistoryId, SupplierId, ProductId, PurchasePrice, PurchasePercentage, DiscountPercentage,
                       Quantity, InvoiceNumber, TransactionDate, AdminId, CreatedAt
                FROM SupplierProductPricingHistory
                WHERE ProductId = @ProductId
                ORDER BY TransactionDate DESC, HistoryId DESC;";

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            var command = new CommandDefinition(
                sql,
                new { ProductId = productId },
                cancellationToken: cancellationToken);

            var results = await connection.QueryAsync<SupplierProductPricingHistory>(command).ConfigureAwait(false);
            return results.AsList();
        }
    }
}
