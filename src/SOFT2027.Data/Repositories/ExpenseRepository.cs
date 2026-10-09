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
    public class ExpenseRepository : IExpenseRepository
    {
        private readonly ISqliteConnectionFactory _connectionFactory;

        public ExpenseRepository(ISqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public async Task<int> AddAsync(Expense expense, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
        {
            if (expense == null)
            {
                throw new ArgumentNullException(nameof(expense));
            }

            cancellationToken.ThrowIfCancellationRequested();

            const string sql = @"
                INSERT INTO Expenses (
                    ExpenseDate, ExpenseCategory, Amount, Description, PaymentSource, AdminId
                ) VALUES (
                    @ExpenseDate, @ExpenseCategory, @Amount, @Description, @PaymentSource, @AdminId
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
                        ExpenseDate = expense.ExpenseDate == default ? DateTime.UtcNow : expense.ExpenseDate,
                        expense.ExpenseCategory,
                        expense.Amount,
                        Description = expense.Description ?? string.Empty,
                        PaymentSource = string.IsNullOrWhiteSpace(expense.PaymentSource) ? "Cash" : expense.PaymentSource,
                        expense.AdminId
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken);

                var expenseId = await connection.ExecuteScalarAsync<int>(cmd).ConfigureAwait(false);
                expense.ExpenseId = expenseId;
                return expenseId;
            }
            finally
            {
                if (shouldDispose)
                {
                    connection.Dispose();
                }
            }
        }

        public async Task<Expense?> GetByIdAsync(int expenseId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            const string sql = "SELECT * FROM Expenses WHERE ExpenseId = @ExpenseId;";

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            var cmd = new CommandDefinition(sql, new { ExpenseId = expenseId }, cancellationToken: cancellationToken);
            return await connection.QuerySingleOrDefaultAsync<Expense>(cmd).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<Expense>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            const string sql = "SELECT * FROM Expenses WHERE ExpenseCategory = @Category COLLATE NOCASE ORDER BY ExpenseDate DESC, ExpenseId DESC;";

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            var cmd = new CommandDefinition(sql, new { Category = category }, cancellationToken: cancellationToken);
            var results = await connection.QueryAsync<Expense>(cmd).ConfigureAwait(false);
            return results.AsList();
        }

        public async Task<IReadOnlyList<Expense>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            const string sql = "SELECT * FROM Expenses ORDER BY ExpenseDate DESC, ExpenseId DESC;";

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            var cmd = new CommandDefinition(sql, cancellationToken: cancellationToken);
            var results = await connection.QueryAsync<Expense>(cmd).ConfigureAwait(false);
            return results.AsList();
        }
    }
}
