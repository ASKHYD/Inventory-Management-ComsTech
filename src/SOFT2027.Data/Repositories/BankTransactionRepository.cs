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
    public class BankTransactionRepository : IBankTransactionRepository
    {
        private readonly ISqliteConnectionFactory _connectionFactory;

        public BankTransactionRepository(ISqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public async Task<int> AddAsync(BankTransaction transaction, IDbTransaction? dbTransaction = null, CancellationToken cancellationToken = default)
        {
            if (transaction == null)
            {
                throw new ArgumentNullException(nameof(transaction));
            }

            cancellationToken.ThrowIfCancellationRequested();

            const string sql = @"
                INSERT INTO BankTransactions (
                    TransactionDate, TransactionType, ReferenceNumber, Description,
                    Debit, Credit, RunningBalance, AdminId, CreatedAt
                ) VALUES (
                    @TransactionDate, @TransactionType, @ReferenceNumber, @Description,
                    @Debit, @Credit, @RunningBalance, @AdminId, @CreatedAt
                );
                SELECT last_insert_rowid();";

            var connection = dbTransaction?.Connection ?? _connectionFactory.CreateConnection();
            var shouldDispose = dbTransaction == null;

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
                        TransactionDate = transaction.TransactionDate == default ? DateTime.UtcNow : transaction.TransactionDate,
                        transaction.TransactionType,
                        ReferenceNumber = transaction.ReferenceNumber ?? string.Empty,
                        Description = transaction.Description ?? string.Empty,
                        transaction.Debit,
                        transaction.Credit,
                        transaction.RunningBalance,
                        transaction.AdminId,
                        CreatedAt = transaction.CreatedAt == default ? DateTime.UtcNow : transaction.CreatedAt
                    },
                    transaction: dbTransaction,
                    cancellationToken: cancellationToken);

                var id = await connection.ExecuteScalarAsync<int>(cmd).ConfigureAwait(false);
                transaction.BankTransactionId = id;
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

        public async Task<decimal> GetCurrentBalanceAsync(IDbTransaction? dbTransaction = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            const string sql = "SELECT RunningBalance FROM BankTransactions ORDER BY TransactionDate DESC, BankTransactionId DESC LIMIT 1;";

            var connection = dbTransaction?.Connection ?? _connectionFactory.CreateConnection();
            var shouldDispose = dbTransaction == null;

            try
            {
                if (shouldDispose && connection is DbConnection dbConnection)
                {
                    await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
                }

                var cmd = new CommandDefinition(sql, transaction: dbTransaction, cancellationToken: cancellationToken);
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

        public async Task<IReadOnlyList<BankTransaction>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            const string sql = "SELECT * FROM BankTransactions ORDER BY TransactionDate DESC, BankTransactionId DESC;";

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            var cmd = new CommandDefinition(sql, cancellationToken: cancellationToken);
            var results = await connection.QueryAsync<BankTransaction>(cmd).ConfigureAwait(false);
            return results.AsList();
        }
    }
}
