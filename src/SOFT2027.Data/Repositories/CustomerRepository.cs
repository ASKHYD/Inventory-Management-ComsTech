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
    public sealed class CustomerRepository : ICustomerRepository
    {
        private readonly ISqliteConnectionFactory _connectionFactory;

        public CustomerRepository(ISqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public async Task<int> AddAsync(Customer customer, CancellationToken cancellationToken = default)
        {
            if (customer == null)
            {
                throw new ArgumentNullException(nameof(customer));
            }

            cancellationToken.ThrowIfCancellationRequested();

            const string sql = @"
                INSERT INTO Customers (
                    CustomerCode, CustomerName, ContactPerson, Phone, Address, AreaTerritory,
                    OpeningBalance, OutstandingBalance, CreditLimit, Status
                ) VALUES (
                    @CustomerCode, @CustomerName, @ContactPerson, @Phone, @Address, @AreaTerritory,
                    @OpeningBalance, @OutstandingBalance, @CreditLimit, @Status
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
                    customer.CustomerCode,
                    customer.CustomerName,
                    customer.ContactPerson,
                    customer.Phone,
                    customer.Address,
                    customer.AreaTerritory,
                    customer.OpeningBalance,
                    customer.OutstandingBalance,
                    customer.CreditLimit,
                    Status = string.IsNullOrWhiteSpace(customer.Status) ? "Active" : customer.Status
                },
                cancellationToken: cancellationToken);

            var id = await connection.ExecuteScalarAsync<int>(command).ConfigureAwait(false);
            customer.CustomerId = id;
            return id;
        }

        public async Task<Customer?> GetByIdAsync(int customerId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            const string sql = @"
                SELECT CustomerId, CustomerCode, CustomerName, ContactPerson, Phone, Address, AreaTerritory,
                       OpeningBalance, OutstandingBalance, CreditLimit, Status, CreatedAt, UpdatedAt
                FROM Customers
                WHERE CustomerId = @CustomerId;";

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            var command = new CommandDefinition(sql, new { CustomerId = customerId }, cancellationToken: cancellationToken);
            return await connection.QuerySingleOrDefaultAsync<Customer>(command).ConfigureAwait(false);
        }

        public async Task<Customer?> GetByCodeAsync(string customerCode, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            const string sql = @"
                SELECT CustomerId, CustomerCode, CustomerName, ContactPerson, Phone, Address, AreaTerritory,
                       OpeningBalance, OutstandingBalance, CreditLimit, Status, CreatedAt, UpdatedAt
                FROM Customers
                WHERE CustomerCode = @CustomerCode;";

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            var command = new CommandDefinition(sql, new { CustomerCode = customerCode }, cancellationToken: cancellationToken);
            return await connection.QuerySingleOrDefaultAsync<Customer>(command).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            const string sql = @"
                SELECT CustomerId, CustomerCode, CustomerName, ContactPerson, Phone, Address, AreaTerritory,
                       OpeningBalance, OutstandingBalance, CreditLimit, Status, CreatedAt, UpdatedAt
                FROM Customers
                ORDER BY CustomerName ASC;";

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            var command = new CommandDefinition(sql, cancellationToken: cancellationToken);
            var results = await connection.QueryAsync<Customer>(command).ConfigureAwait(false);
            return results.AsList();
        }

        public async Task<bool> UpdateAsync(Customer customer, CancellationToken cancellationToken = default)
        {
            if (customer == null)
            {
                throw new ArgumentNullException(nameof(customer));
            }

            cancellationToken.ThrowIfCancellationRequested();

            const string sql = @"
                UPDATE Customers SET
                    CustomerCode = @CustomerCode,
                    CustomerName = @CustomerName,
                    ContactPerson = @ContactPerson,
                    Phone = @Phone,
                    Address = @Address,
                    AreaTerritory = @AreaTerritory,
                    OpeningBalance = @OpeningBalance,
                    OutstandingBalance = @OutstandingBalance,
                    CreditLimit = @CreditLimit,
                    Status = @Status,
                    UpdatedAt = CURRENT_TIMESTAMP
                WHERE CustomerId = @CustomerId;";

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            var command = new CommandDefinition(
                sql,
                new
                {
                    customer.CustomerId,
                    customer.CustomerCode,
                    customer.CustomerName,
                    customer.ContactPerson,
                    customer.Phone,
                    customer.Address,
                    customer.AreaTerritory,
                    customer.OpeningBalance,
                    customer.OutstandingBalance,
                    customer.CreditLimit,
                    customer.Status
                },
                cancellationToken: cancellationToken);

            var rowsAffected = await connection.ExecuteAsync(command).ConfigureAwait(false);
            return rowsAffected > 0;
        }

        public async Task<bool> UpdateBalanceAsync(int customerId, decimal newOutstandingBalance, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            const string sql = @"
                UPDATE Customers SET
                    OutstandingBalance = @OutstandingBalance,
                    UpdatedAt = CURRENT_TIMESTAMP
                WHERE CustomerId = @CustomerId;";

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            var command = new CommandDefinition(
                sql,
                new
                {
                    CustomerId = customerId,
                    OutstandingBalance = newOutstandingBalance
                },
                cancellationToken: cancellationToken);

            var rowsAffected = await connection.ExecuteAsync(command).ConfigureAwait(false);
            return rowsAffected > 0;
        }

        public async Task<Customer?> GetByIdAsync(int customerId, IDbTransaction? transaction, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            const string sql = "SELECT * FROM Customers WHERE CustomerId = @CustomerId;";

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
                    new { CustomerId = customerId },
                    transaction: transaction,
                    cancellationToken: cancellationToken);

                return await connection.QuerySingleOrDefaultAsync<Customer>(command).ConfigureAwait(false);
            }
            finally
            {
                if (shouldDispose)
                {
                    connection.Dispose();
                }
            }
        }

        public async Task<bool> UpdateBalanceAsync(int customerId, decimal newOutstandingBalance, IDbTransaction? transaction, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            const string sql = @"
                UPDATE Customers SET
                    OutstandingBalance = @OutstandingBalance,
                    UpdatedAt = CURRENT_TIMESTAMP
                WHERE CustomerId = @CustomerId;";

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
                        CustomerId = customerId,
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
