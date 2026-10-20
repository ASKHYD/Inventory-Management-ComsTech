using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using SOFT2027.Core.Entities;
using SOFT2027.Core.Interfaces;

namespace SOFT2027.Data.Repositories
{
    public class PurchaseRepository : IPurchaseRepository
    {
        private readonly ISqliteConnectionFactory _connectionFactory;

        public PurchaseRepository(ISqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public async Task<int> AddAsync(Purchase purchase, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
        {
            if (purchase == null)
            {
                throw new ArgumentNullException(nameof(purchase));
            }

            cancellationToken.ThrowIfCancellationRequested();

            const string purchaseSql = @"
                INSERT INTO Purchases (
                    InvoiceNumber, SupplierId, PurchaseDate, SubTotal, DiscountAmount, 
                    NetTotal, PaidAmount, RemainingBalance, Status, AdminId, Remarks
                ) VALUES (
                    @InvoiceNumber, @SupplierId, @PurchaseDate, @SubTotal, @DiscountAmount, 
                    @NetTotal, @PaidAmount, @RemainingBalance, @Status, @AdminId, @Remarks
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

                var purchaseCmd = new CommandDefinition(
                    purchaseSql,
                    purchase,
                    transaction: transaction,
                    cancellationToken: cancellationToken);

                var purchaseId = await connection.ExecuteScalarAsync<int>(purchaseCmd).ConfigureAwait(false);
                purchase.PurchaseId = purchaseId;

                if (purchase.Items != null && purchase.Items.Count > 0)
                {
                    const string itemSql = @"
                        INSERT INTO PurchaseItems (
                            PurchaseId, ProductId, ProductCodeAtPurchase, ProductNameAtPurchase, Quantity, HistoricalPurchasePrice,
                            PurchasePercentage, DiscountPercentage, LineTotal
                        ) VALUES (
                            @PurchaseId, @ProductId, @ProductCodeAtPurchase, @ProductNameAtPurchase, @Quantity, @HistoricalPurchasePrice,
                            @PurchasePercentage, @DiscountPercentage, @LineTotal
                        );";

                    foreach (var item in purchase.Items)
                    {
                        item.PurchaseId = purchaseId;
                        var itemCmd = new CommandDefinition(
                            itemSql,
                            item,
                            transaction: transaction,
                            cancellationToken: cancellationToken);

                        await connection.ExecuteAsync(itemCmd).ConfigureAwait(false);
                    }
                }

                return purchaseId;
            }
            finally
            {
                if (shouldDispose)
                {
                    connection.Dispose();
                }
            }
        }

        public async Task<Purchase?> GetByIdAsync(int purchaseId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            const string sql = "SELECT * FROM Purchases WHERE PurchaseId = @PurchaseId;";

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            var cmd = new CommandDefinition(sql, new { PurchaseId = purchaseId }, cancellationToken: cancellationToken);
            var purchase = await connection.QuerySingleOrDefaultAsync<Purchase>(cmd).ConfigureAwait(false);
            if (purchase != null)
            {
                var items = await GetItemsByPurchaseIdInternalAsync(connection, purchaseId, null, cancellationToken).ConfigureAwait(false);
                purchase.Items = items.ToList();
            }

            return purchase;
        }

        public async Task<Purchase?> GetByInvoiceNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            const string sql = "SELECT * FROM Purchases WHERE InvoiceNumber = @InvoiceNumber COLLATE NOCASE;";

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            var cmd = new CommandDefinition(sql, new { InvoiceNumber = invoiceNumber }, cancellationToken: cancellationToken);
            var purchase = await connection.QuerySingleOrDefaultAsync<Purchase>(cmd).ConfigureAwait(false);
            if (purchase != null)
            {
                var items = await GetItemsByPurchaseIdInternalAsync(connection, purchase.PurchaseId, null, cancellationToken).ConfigureAwait(false);
                purchase.Items = items.ToList();
            }

            return purchase;
        }

        public async Task<IReadOnlyList<Purchase>> GetBySupplierIdAsync(int supplierId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            const string sql = "SELECT * FROM Purchases WHERE SupplierId = @SupplierId ORDER BY PurchaseDate DESC;";

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            var cmd = new CommandDefinition(sql, new { SupplierId = supplierId }, cancellationToken: cancellationToken);
            var purchases = await connection.QueryAsync<Purchase>(cmd).ConfigureAwait(false);
            return purchases.AsList();
        }

        public async Task<IReadOnlyList<PurchaseItem>> GetItemsByPurchaseIdAsync(int purchaseId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            return await GetItemsByPurchaseIdInternalAsync(connection, purchaseId, null, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<IReadOnlyList<PurchaseItem>> GetItemsByPurchaseIdInternalAsync(
            IDbConnection connection,
            int purchaseId,
            IDbTransaction? transaction,
            CancellationToken cancellationToken)
        {
            const string sql = "SELECT * FROM PurchaseItems WHERE PurchaseId = @PurchaseId ORDER BY PurchaseItemId ASC;";
            var cmd = new CommandDefinition(sql, new { PurchaseId = purchaseId }, transaction: transaction, cancellationToken: cancellationToken);
            var items = await connection.QueryAsync<PurchaseItem>(cmd).ConfigureAwait(false);
            return items.AsList();
        }
    }
}
