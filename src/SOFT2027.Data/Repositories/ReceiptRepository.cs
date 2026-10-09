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
    public class ReceiptRepository : IReceiptRepository
    {
        private readonly ISqliteConnectionFactory _connectionFactory;

        public ReceiptRepository(ISqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public async Task<int> AddAsync(Receipt receipt, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
        {
            if (receipt == null)
            {
                throw new ArgumentNullException(nameof(receipt));
            }

            cancellationToken.ThrowIfCancellationRequested();

            const string sql = @"
                INSERT INTO Receipts (
                    ReceiptNumber, CustomerId, ReceiptDate, Amount, PaymentMode,
                    ReferenceDetails, AdminId, Remarks
                ) VALUES (
                    @ReceiptNumber, @CustomerId, @ReceiptDate, @Amount, @PaymentMode,
                    @ReferenceDetails, @AdminId, @Remarks
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
                        receipt.ReceiptNumber,
                        receipt.CustomerId,
                        ReceiptDate = receipt.ReceiptDate == default ? DateTime.UtcNow : receipt.ReceiptDate,
                        receipt.Amount,
                        PaymentMode = string.IsNullOrWhiteSpace(receipt.PaymentMode) ? "Cash" : receipt.PaymentMode,
                        receipt.ReferenceDetails,
                        receipt.AdminId,
                        receipt.Remarks
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken);

                var receiptId = await connection.ExecuteScalarAsync<int>(cmd).ConfigureAwait(false);
                receipt.ReceiptId = receiptId;
                return receiptId;
            }
            finally
            {
                if (shouldDispose)
                {
                    connection.Dispose();
                }
            }
        }

        public async Task<Receipt?> GetByIdAsync(int receiptId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            const string sql = "SELECT * FROM Receipts WHERE ReceiptId = @ReceiptId;";

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            var cmd = new CommandDefinition(sql, new { ReceiptId = receiptId }, cancellationToken: cancellationToken);
            return await connection.QuerySingleOrDefaultAsync<Receipt>(cmd).ConfigureAwait(false);
        }

        public async Task<Receipt?> GetByNumberAsync(string receiptNumber, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            const string sql = "SELECT * FROM Receipts WHERE ReceiptNumber = @ReceiptNumber COLLATE NOCASE;";

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            var cmd = new CommandDefinition(sql, new { ReceiptNumber = receiptNumber }, cancellationToken: cancellationToken);
            return await connection.QuerySingleOrDefaultAsync<Receipt>(cmd).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<Receipt>> GetByCustomerIdAsync(int customerId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            const string sql = "SELECT * FROM Receipts WHERE CustomerId = @CustomerId ORDER BY ReceiptDate DESC, ReceiptId DESC;";

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            var cmd = new CommandDefinition(sql, new { CustomerId = customerId }, cancellationToken: cancellationToken);
            var receipts = await connection.QueryAsync<Receipt>(cmd).ConfigureAwait(false);
            return receipts.AsList();
        }
    }
}
