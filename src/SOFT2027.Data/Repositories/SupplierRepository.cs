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
    public sealed class SupplierRepository : ISupplierRepository
    {
        private readonly ISqliteConnectionFactory _connectionFactory;

        public SupplierRepository(ISqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public async Task<int> AddAsync(Supplier supplier, CancellationToken cancellationToken = default)
        {
            if (supplier == null)
            {
                throw new ArgumentNullException(nameof(supplier));
            }

            cancellationToken.ThrowIfCancellationRequested();

            const string sql = @"
                INSERT INTO Suppliers (
                    SupplierCode, SupplierName, ContactPerson, Phone, Address,
                    OpeningBalance, OutstandingBalance, Status
                ) VALUES (
                    @SupplierCode, @SupplierName, @ContactPerson, @Phone, @Address,
                    @OpeningBalance, @OutstandingBalance, @Status
                );
                SELECT last_insert_rowid();";

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            var command = new CommandDefinition(
                sql,
                new
                {
                    supplier.SupplierCode,
                    supplier.SupplierName,
                    supplier.ContactPerson,
                    supplier.Phone,
                    supplier.Address,
                    supplier.OpeningBalance,
                    supplier.OutstandingBalance,
                    Status = string.IsNullOrWhiteSpace(supplier.Status) ? "Active" : supplier.Status
                },
                cancellationToken: cancellationToken);

            var id = await connection.ExecuteScalarAsync<int>(command).ConfigureAwait(false);
            supplier.SupplierId = id;
            return id;
        }

        public async Task<Supplier?> GetByIdAsync(int supplierId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            const string sql = @"
                SELECT SupplierId, SupplierCode, SupplierName, ContactPerson, Phone, Address,
                       OpeningBalance, OutstandingBalance, Status, CreatedAt, UpdatedAt
                FROM Suppliers
                WHERE SupplierId = @SupplierId;";

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            var command = new CommandDefinition(sql, new { SupplierId = supplierId }, cancellationToken: cancellationToken);
            return await connection.QuerySingleOrDefaultAsync<Supplier>(command).ConfigureAwait(false);
        }

        public async Task<Supplier?> GetByCodeAsync(string supplierCode, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            const string sql = @"
                SELECT SupplierId, SupplierCode, SupplierName, ContactPerson, Phone, Address,
                       OpeningBalance, OutstandingBalance, Status, CreatedAt, UpdatedAt
                FROM Suppliers
                WHERE SupplierCode = @SupplierCode;";

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            var command = new CommandDefinition(sql, new { SupplierCode = supplierCode }, cancellationToken: cancellationToken);
            return await connection.QuerySingleOrDefaultAsync<Supplier>(command).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<Supplier>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            const string sql = @"
                SELECT SupplierId, SupplierCode, SupplierName, ContactPerson, Phone, Address,
                       OpeningBalance, OutstandingBalance, Status, CreatedAt, UpdatedAt
                FROM Suppliers
                ORDER BY SupplierName ASC;";

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            var command = new CommandDefinition(sql, cancellationToken: cancellationToken);
            var results = await connection.QueryAsync<Supplier>(command).ConfigureAwait(false);
            return results.AsList();
        }

        public async Task<bool> UpdateAsync(Supplier supplier, CancellationToken cancellationToken = default)
        {
            if (supplier == null)
            {
                throw new ArgumentNullException(nameof(supplier));
            }

            cancellationToken.ThrowIfCancellationRequested();

            const string sql = @"
                UPDATE Suppliers SET
                    SupplierCode = @SupplierCode,
                    SupplierName = @SupplierName,
                    ContactPerson = @ContactPerson,
                    Phone = @Phone,
                    Address = @Address,
                    OpeningBalance = @OpeningBalance,
                    OutstandingBalance = @OutstandingBalance,
                    Status = @Status,
                    UpdatedAt = CURRENT_TIMESTAMP
                WHERE SupplierId = @SupplierId;";

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            var command = new CommandDefinition(
                sql,
                new
                {
                    supplier.SupplierId,
                    supplier.SupplierCode,
                    supplier.SupplierName,
                    supplier.ContactPerson,
                    supplier.Phone,
                    supplier.Address,
                    supplier.OpeningBalance,
                    supplier.OutstandingBalance,
                    supplier.Status
                },
                cancellationToken: cancellationToken);

            var rowsAffected = await connection.ExecuteAsync(command).ConfigureAwait(false);
            return rowsAffected > 0;
        }

        public async Task<bool> UpdateBalanceAsync(int supplierId, decimal newOutstandingBalance, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            const string sql = @"
                UPDATE Suppliers SET
                    OutstandingBalance = @OutstandingBalance,
                    UpdatedAt = CURRENT_TIMESTAMP
                WHERE SupplierId = @SupplierId;";

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            var command = new CommandDefinition(
                sql,
                new
                {
                    SupplierId = supplierId,
                    OutstandingBalance = newOutstandingBalance
                },
                cancellationToken: cancellationToken);

            var rowsAffected = await connection.ExecuteAsync(command).ConfigureAwait(false);
            return rowsAffected > 0;
        }

        public async Task<Supplier?> GetByIdAsync(int supplierId, IDbTransaction? transaction, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            const string sql = "SELECT * FROM Suppliers WHERE SupplierId = @SupplierId;";

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
                    new { SupplierId = supplierId },
                    transaction: transaction,
                    cancellationToken: cancellationToken);

                return await connection.QuerySingleOrDefaultAsync<Supplier>(command).ConfigureAwait(false);
            }
            finally
            {
                if (shouldDispose)
                {
                    connection.Dispose();
                }
            }
        }

        public async Task<bool> UpdateBalanceAsync(int supplierId, decimal newOutstandingBalance, IDbTransaction? transaction, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            const string sql = @"
                UPDATE Suppliers SET
                    OutstandingBalance = @OutstandingBalance,
                    UpdatedAt = CURRENT_TIMESTAMP
                WHERE SupplierId = @SupplierId;";

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
                        SupplierId = supplierId,
                        OutstandingBalance = newOutstandingBalance
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken);

                var rowsAffected = await connection.ExecuteAsync(command).ConfigureAwait(false);
                return rowsAffected > 0;
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
