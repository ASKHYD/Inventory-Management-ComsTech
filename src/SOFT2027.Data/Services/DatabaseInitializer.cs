using System;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using SOFT2027.Core.Interfaces;

namespace SOFT2027.Data.Services
{
    public sealed class DatabaseInitializer : IDatabaseInitializer
    {
        private readonly ISqliteConnectionFactory _connectionFactory;
        private readonly string _schemaSqlPath;

        public DatabaseInitializer(ISqliteConnectionFactory connectionFactory, string schemaSqlPath)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _schemaSqlPath = schemaSqlPath ?? throw new ArgumentNullException(nameof(schemaSqlPath));
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(_schemaSqlPath))
            {
                throw new FileNotFoundException($"Schema SQL file not found at: {_schemaSqlPath}", _schemaSqlPath);
            }

            var schemaSql = await File.ReadAllTextAsync(_schemaSqlPath, cancellationToken).ConfigureAwait(false);

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConn)
            {
                await dbConn.OpenAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                connection.Open();
            }

            // Enforce performance & integrity PRAGMAs
            using (var pragmaCmd = connection.CreateCommand())
            {
                pragmaCmd.CommandText = @"
                    PRAGMA journal_mode = WAL;
                    PRAGMA foreign_keys = ON;
                    PRAGMA synchronous = NORMAL;
                ";
                if (pragmaCmd is DbCommand dbPragmaCmd)
                {
                    await dbPragmaCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    pragmaCmd.ExecuteNonQuery();
                }
            }

            // Execute the schema script
            using (var schemaCmd = connection.CreateCommand())
            {
                schemaCmd.CommandText = schemaSql;
                if (schemaCmd is DbCommand dbSchemaCmd)
                {
                    await dbSchemaCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    schemaCmd.ExecuteNonQuery();
                }
            }

            // Existing installations may predate item-level product-description snapshots.
            // Add missing columns idempotently and seed existing rows once from their current catalog entry.
            await EnsureSnapshotColumnAsync(connection, "SaleItems", "ProductCodeAtSale", cancellationToken).ConfigureAwait(false);
            await EnsureSnapshotColumnAsync(connection, "SaleItems", "ProductNameAtSale", cancellationToken).ConfigureAwait(false);
            await EnsureSnapshotColumnAsync(connection, "PurchaseItems", "ProductCodeAtPurchase", cancellationToken).ConfigureAwait(false);
            await EnsureSnapshotColumnAsync(connection, "PurchaseItems", "ProductNameAtPurchase", cancellationToken).ConfigureAwait(false);
            await connection.ExecuteAsync(new CommandDefinition(@"
                UPDATE SaleItems SET
                    ProductCodeAtSale=COALESCE((SELECT ProductCode FROM Products WHERE Products.ProductId=SaleItems.ProductId), '')
                WHERE ProductCodeAtSale='';
                UPDATE SaleItems SET
                    ProductNameAtSale=COALESCE((SELECT ProductName FROM Products WHERE Products.ProductId=SaleItems.ProductId), '')
                WHERE ProductNameAtSale='';
                UPDATE PurchaseItems SET
                    ProductCodeAtPurchase=COALESCE((SELECT ProductCode FROM Products WHERE Products.ProductId=PurchaseItems.ProductId), '')
                WHERE ProductCodeAtPurchase='';
                UPDATE PurchaseItems SET
                    ProductNameAtPurchase=COALESCE((SELECT ProductName FROM Products WHERE Products.ProductId=PurchaseItems.ProductId), '')
                WHERE ProductNameAtPurchase='';", cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        private static async Task EnsureSnapshotColumnAsync(System.Data.IDbConnection connection, string table, string column, CancellationToken cancellationToken)
        {
            var columns = await connection.QueryAsync<SchemaColumn>(new CommandDefinition($"PRAGMA table_info({table});", cancellationToken: cancellationToken)).ConfigureAwait(false);
            if (!columns.Any(item => string.Equals(item.Name, column, StringComparison.OrdinalIgnoreCase)))
                await connection.ExecuteAsync(new CommandDefinition($"ALTER TABLE {table} ADD COLUMN {column} TEXT NOT NULL DEFAULT '';", cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        private sealed class SchemaColumn { public string Name { get; set; } = string.Empty; }
    }
}
