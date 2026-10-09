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
    public class SupplierLedgerRepository : ISupplierLedgerRepository
    {
        private readonly ISqliteConnectionFactory _connectionFactory;

        public SupplierLedgerRepository(ISqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public async Task<int> AddAsync(SupplierLedgerEntry entry, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            cancellationToken.ThrowIfCancellationRequested();

            const string sql = @"
                INSERT INTO SupplierLedger (
                    SupplierId, TransactionDate, TransactionType, ReferenceNumber, 
                    Description, Debit, Credit, RunningBalance
                ) VALUES (
                    @SupplierId, @TransactionDate, @TransactionType, @ReferenceNumber, 
                    @Description, @Debit, @Credit, @RunningBalance
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
                        entry.SupplierId,
                        TransactionDate = entry.TransactionDate == default ? DateTime.UtcNow : entry.TransactionDate,
                        entry.TransactionType,
                        entry.ReferenceNumber,
                        Description = entry.Description ?? string.Empty,
                        entry.Debit,
                        entry.Credit,
                        entry.RunningBalance
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken);

                var ledgerId = await connection.ExecuteScalarAsync<int>(cmd).ConfigureAwait(false);
                entry.LedgerId = ledgerId;
                return ledgerId;
            }
            finally
            {
                if (shouldDispose)
                {
                    connection.Dispose();
                }
            }
        }

        public async Task<IReadOnlyList<SupplierLedgerEntry>> GetBySupplierIdAsync(int supplierId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            const string sql = "SELECT * FROM SupplierLedger WHERE SupplierId = @SupplierId ORDER BY TransactionDate ASC, LedgerId ASC;";

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            var cmd = new CommandDefinition(sql, new { SupplierId = supplierId }, cancellationToken: cancellationToken);
            var entries = await connection.QueryAsync<SupplierLedgerEntry>(cmd).ConfigureAwait(false);
            return entries.AsList();
        }

        public async Task<decimal> GetRunningBalanceAsync(int supplierId, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            const string sql = @"
                SELECT RunningBalance 
                FROM SupplierLedger 
                WHERE SupplierId = @SupplierId 
                ORDER BY TransactionDate DESC, LedgerId DESC 
                LIMIT 1;";

            var connection = transaction?.Connection ?? _connectionFactory.CreateConnection();
            var shouldDispose = transaction == null;

            try
            {
                if (shouldDispose && connection is DbConnection dbConnection)
                {
                    await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
                }

                var cmd = new CommandDefinition(sql, new { SupplierId = supplierId }, transaction: transaction, cancellationToken: cancellationToken);
                var balance = await connection.ExecuteScalarAsync<decimal?>(cmd).ConfigureAwait(false);
                return balance ?? 0.0m;
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
