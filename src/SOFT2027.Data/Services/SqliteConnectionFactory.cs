using System;
using System.Data;
using Microsoft.Data.Sqlite;
using SOFT2027.Core.Interfaces;

namespace SOFT2027.Data.Services
{
    public sealed class SqliteConnectionFactory : ISqliteConnectionFactory
    {
        private readonly string _connectionString;

        public SqliteConnectionFactory(string databasePath)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
            {
                throw new ArgumentException("Database path cannot be null or whitespace.", nameof(databasePath));
            }

            var path = databasePath;
            if (databasePath.Contains("Data Source=", StringComparison.OrdinalIgnoreCase) || databasePath.Contains(';'))
            {
                try
                {
                    var parser = new SqliteConnectionStringBuilder(databasePath);
                    if (!string.IsNullOrWhiteSpace(parser.DataSource))
                    {
                        path = parser.DataSource;
                    }
                }
                catch
                {
                    // Fall back to original string
                }
            }

            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadWriteCreate,
                ForeignKeys = true,
                Pooling = true,
                DefaultTimeout = 2
            };

            _connectionString = builder.ConnectionString;
        }

        public IDbConnection CreateConnection()
        {
            return new SqliteConnection(_connectionString);
        }
    }
}
