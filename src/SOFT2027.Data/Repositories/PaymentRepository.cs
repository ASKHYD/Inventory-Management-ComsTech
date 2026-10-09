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
    public class PaymentRepository : IPaymentRepository
    {
        private readonly ISqliteConnectionFactory _connectionFactory;

        public PaymentRepository(ISqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public async Task<int> AddAsync(Payment payment, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
        {
            if (payment == null)
            {
                throw new ArgumentNullException(nameof(payment));
            }

            cancellationToken.ThrowIfCancellationRequested();

            const string sql = @"
                INSERT INTO Payments (
                    PaymentNumber, SupplierId, PaymentDate, Amount, PaymentMode,
                    ReferenceDetails, AdminId, Remarks
                ) VALUES (
                    @PaymentNumber, @SupplierId, @PaymentDate, @Amount, @PaymentMode,
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
                        payment.PaymentNumber,
                        payment.SupplierId,
                        PaymentDate = payment.PaymentDate == default ? DateTime.UtcNow : payment.PaymentDate,
                        payment.Amount,
                        PaymentMode = string.IsNullOrWhiteSpace(payment.PaymentMode) ? "Cash" : payment.PaymentMode,
                        payment.ReferenceDetails,
                        payment.AdminId,
                        payment.Remarks
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken);

                var paymentId = await connection.ExecuteScalarAsync<int>(cmd).ConfigureAwait(false);
                payment.PaymentId = paymentId;
                return paymentId;
            }
            finally
            {
                if (shouldDispose)
                {
                    connection.Dispose();
                }
            }
        }

        public async Task<Payment?> GetByIdAsync(int paymentId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            const string sql = "SELECT * FROM Payments WHERE PaymentId = @PaymentId;";

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            var cmd = new CommandDefinition(sql, new { PaymentId = paymentId }, cancellationToken: cancellationToken);
            return await connection.QuerySingleOrDefaultAsync<Payment>(cmd).ConfigureAwait(false);
        }

        public async Task<Payment?> GetByNumberAsync(string paymentNumber, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            const string sql = "SELECT * FROM Payments WHERE PaymentNumber = @PaymentNumber COLLATE NOCASE;";

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            var cmd = new CommandDefinition(sql, new { PaymentNumber = paymentNumber }, cancellationToken: cancellationToken);
            return await connection.QuerySingleOrDefaultAsync<Payment>(cmd).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<Payment>> GetBySupplierIdAsync(int supplierId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            const string sql = "SELECT * FROM Payments WHERE SupplierId = @SupplierId ORDER BY PaymentDate DESC, PaymentId DESC;";

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            var cmd = new CommandDefinition(sql, new { SupplierId = supplierId }, cancellationToken: cancellationToken);
            var payments = await connection.QueryAsync<Payment>(cmd).ConfigureAwait(false);
            return payments.AsList();
        }
    }
}
