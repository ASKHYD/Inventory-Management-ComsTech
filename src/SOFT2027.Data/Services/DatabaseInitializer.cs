using System;
using System.Data;
using System.Data.Common;
using System.IO;
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
        }
    }
}
