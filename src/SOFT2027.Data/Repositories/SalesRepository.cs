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
    public class SalesRepository : ISalesRepository
    {
        private readonly ISqliteConnectionFactory _connectionFactory;

        public SalesRepository(ISqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public async Task<int> AddAsync(Sale sale, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
        {
            if (sale == null)
            {
                throw new ArgumentNullException(nameof(sale));
            }

            cancellationToken.ThrowIfCancellationRequested();

            const string saleSql = @"
                INSERT INTO Sales (
                    InvoiceNumber, CustomerId, SaleDate, SubTotal, DiscountAmount, 
                    NetTotal, PaidAmount, RemainingBalance, PaymentType, Status, 
                    AdminId, Remarks
                ) VALUES (
                    @InvoiceNumber, @CustomerId, @SaleDate, @SubTotal, @DiscountAmount, 
                    @NetTotal, @PaidAmount, @RemainingBalance, @PaymentType, @Status, 
                    @AdminId, @Remarks
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

                var saleCmd = new CommandDefinition(
                    saleSql,
                    sale,
                    transaction: transaction,
                    cancellationToken: cancellationToken);

                var saleId = await connection.ExecuteScalarAsync<int>(saleCmd).ConfigureAwait(false);
                sale.SaleId = saleId;

                if (sale.Items != null && sale.Items.Count > 0)
                {
                    const string itemSql = @"
                        INSERT INTO SaleItems (
                            SaleId, ProductId, Quantity, HistoricalSalePrice, HistoricalCostPrice,
                            SellingPercentage, DiscountPercentage, LineTotal
                        ) VALUES (
                            @SaleId, @ProductId, @Quantity, @HistoricalSalePrice, @HistoricalCostPrice,
                            @SellingPercentage, @DiscountPercentage, @LineTotal
                        );";

                    foreach (var item in sale.Items)
                    {
                        item.SaleId = saleId;
                        var itemCmd = new CommandDefinition(
                            itemSql,
                            item,
                            transaction: transaction,
                            cancellationToken: cancellationToken);

                        await connection.ExecuteAsync(itemCmd).ConfigureAwait(false);
                    }
                }

                return saleId;
            }
            finally
            {
                if (shouldDispose)
                {
                    connection.Dispose();
                }
            }
        }

        public async Task<Sale?> GetByIdAsync(int saleId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            const string sql = "SELECT * FROM Sales WHERE SaleId = @SaleId;";

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            var cmd = new CommandDefinition(sql, new { SaleId = saleId }, cancellationToken: cancellationToken);
            var sale = await connection.QuerySingleOrDefaultAsync<Sale>(cmd).ConfigureAwait(false);
            if (sale != null)
            {
                var items = await GetItemsBySaleIdInternalAsync(connection, saleId, null, cancellationToken).ConfigureAwait(false);
                sale.Items = items.ToList();
            }

            return sale;
        }

        public async Task<Sale?> GetByInvoiceNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            const string sql = "SELECT * FROM Sales WHERE InvoiceNumber = @InvoiceNumber COLLATE NOCASE;";

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            var cmd = new CommandDefinition(sql, new { InvoiceNumber = invoiceNumber }, cancellationToken: cancellationToken);
            var sale = await connection.QuerySingleOrDefaultAsync<Sale>(cmd).ConfigureAwait(false);
            if (sale != null)
            {
                var items = await GetItemsBySaleIdInternalAsync(connection, sale.SaleId, null, cancellationToken).ConfigureAwait(false);
                sale.Items = items.ToList();
            }

            return sale;
        }

        public async Task<IReadOnlyList<Sale>> GetByCustomerIdAsync(int customerId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            const string sql = "SELECT * FROM Sales WHERE CustomerId = @CustomerId ORDER BY SaleDate DESC;";

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            var cmd = new CommandDefinition(sql, new { CustomerId = customerId }, cancellationToken: cancellationToken);
            var sales = await connection.QueryAsync<Sale>(cmd).ConfigureAwait(false);
            return sales.AsList();
        }

        public async Task<IReadOnlyList<SaleItem>> GetItemsBySaleIdAsync(int saleId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            return await GetItemsBySaleIdInternalAsync(connection, saleId, null, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<IReadOnlyList<SaleItem>> GetItemsBySaleIdInternalAsync(
            IDbConnection connection,
            int saleId,
            IDbTransaction? transaction,
            CancellationToken cancellationToken)
        {
            const string sql = "SELECT * FROM SaleItems WHERE SaleId = @SaleId ORDER BY SaleItemId ASC;";
            var cmd = new CommandDefinition(sql, new { SaleId = saleId }, transaction: transaction, cancellationToken: cancellationToken);
            var items = await connection.QueryAsync<SaleItem>(cmd).ConfigureAwait(false);
            return items.AsList();
        }
    }
}
