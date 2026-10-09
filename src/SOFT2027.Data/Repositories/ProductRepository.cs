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
    public sealed class ProductRepository : IProductRepository
    {
        private readonly ISqliteConnectionFactory _connectionFactory;

        public ProductRepository(ISqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public async Task<int> AddAsync(Product product, CancellationToken cancellationToken = default)
        {
            if (product == null)
            {
                throw new ArgumentNullException(nameof(product));
            }

            cancellationToken.ThrowIfCancellationRequested();

            const string sql = @"
                INSERT INTO Products (
                    ProductCode, Barcode, ProductName, CategoryId, BrandId, UnitId,
                    BasePurchasePrice, PurchasePercentage, DefaultSalePrice, SellingPercentage,
                    CurrentStock, MinimumStock, Status
                ) VALUES (
                    @ProductCode, @Barcode, @ProductName, @CategoryId, @BrandId, @UnitId,
                    @BasePurchasePrice, @PurchasePercentage, @DefaultSalePrice, @SellingPercentage,
                    @CurrentStock, @MinimumStock, @Status
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
                    product.ProductCode,
                    product.Barcode,
                    product.ProductName,
                    product.CategoryId,
                    product.BrandId,
                    product.UnitId,
                    product.BasePurchasePrice,
                    product.PurchasePercentage,
                    product.DefaultSalePrice,
                    product.SellingPercentage,
                    product.CurrentStock,
                    product.MinimumStock,
                    Status = string.IsNullOrWhiteSpace(product.Status) ? "Active" : product.Status
                },
                cancellationToken: cancellationToken);

            var id = await connection.ExecuteScalarAsync<int>(command).ConfigureAwait(false);
            product.ProductId = id;
            return id;
        }

        public async Task<Product?> GetByIdAsync(int productId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            const string sql = @"
                SELECT ProductId, ProductCode, Barcode, ProductName, CategoryId, BrandId, UnitId,
                       BasePurchasePrice, PurchasePercentage, DefaultSalePrice, SellingPercentage,
                       CurrentStock, MinimumStock, Status, CreatedAt, UpdatedAt
                FROM Products
                WHERE ProductId = @ProductId;";

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            var command = new CommandDefinition(sql, new { ProductId = productId }, cancellationToken: cancellationToken);
            return await connection.QuerySingleOrDefaultAsync<Product>(command).ConfigureAwait(false);
        }

        public async Task<Product?> GetByCodeAsync(string productCode, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            const string sql = @"
                SELECT ProductId, ProductCode, Barcode, ProductName, CategoryId, BrandId, UnitId,
                       BasePurchasePrice, PurchasePercentage, DefaultSalePrice, SellingPercentage,
                       CurrentStock, MinimumStock, Status, CreatedAt, UpdatedAt
                FROM Products
                WHERE ProductCode = @ProductCode;";

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            var command = new CommandDefinition(sql, new { ProductCode = productCode }, cancellationToken: cancellationToken);
            return await connection.QuerySingleOrDefaultAsync<Product>(command).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            const string sql = @"
                SELECT ProductId, ProductCode, Barcode, ProductName, CategoryId, BrandId, UnitId,
                       BasePurchasePrice, PurchasePercentage, DefaultSalePrice, SellingPercentage,
                       CurrentStock, MinimumStock, Status, CreatedAt, UpdatedAt
                FROM Products
                ORDER BY ProductName ASC;";

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            var command = new CommandDefinition(sql, cancellationToken: cancellationToken);
            var results = await connection.QueryAsync<Product>(command).ConfigureAwait(false);
            return results.AsList();
        }

        public async Task<bool> UpdateAsync(Product product, CancellationToken cancellationToken = default)
        {
            if (product == null)
            {
                throw new ArgumentNullException(nameof(product));
            }

            cancellationToken.ThrowIfCancellationRequested();

            const string sql = @"
                UPDATE Products SET
                    ProductCode = @ProductCode,
                    Barcode = @Barcode,
                    ProductName = @ProductName,
                    CategoryId = @CategoryId,
                    BrandId = @BrandId,
                    UnitId = @UnitId,
                    BasePurchasePrice = @BasePurchasePrice,
                    PurchasePercentage = @PurchasePercentage,
                    DefaultSalePrice = @DefaultSalePrice,
                    SellingPercentage = @SellingPercentage,
                    CurrentStock = @CurrentStock,
                    MinimumStock = @MinimumStock,
                    Status = @Status,
                    UpdatedAt = CURRENT_TIMESTAMP
                WHERE ProductId = @ProductId;";

            using var connection = _connectionFactory.CreateConnection();
            if (connection is DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            var command = new CommandDefinition(
                sql,
                new
                {
                    product.ProductId,
                    product.ProductCode,
                    product.Barcode,
                    product.ProductName,
                    product.CategoryId,
                    product.BrandId,
                    product.UnitId,
                    product.BasePurchasePrice,
                    product.PurchasePercentage,
                    product.DefaultSalePrice,
                    product.SellingPercentage,
                    product.CurrentStock,
                    product.MinimumStock,
                    product.Status
                },
                cancellationToken: cancellationToken);

            var rowsAffected = await connection.ExecuteAsync(command).ConfigureAwait(false);
            return rowsAffected > 0;
        }

        public async Task<Product?> GetByIdAsync(int productId, IDbTransaction? transaction, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            const string sql = "SELECT * FROM Products WHERE ProductId = @ProductId;";

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
                    new { ProductId = productId },
                    transaction: transaction,
                    cancellationToken: cancellationToken);

                return await connection.QuerySingleOrDefaultAsync<Product>(command).ConfigureAwait(false);
            }
            finally
            {
                if (shouldDispose)
                {
                    connection.Dispose();
                }
            }
        }

        public async Task<bool> UpdateStockAsync(int productId, decimal newStock, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            const string sql = "UPDATE Products SET CurrentStock = @CurrentStock, UpdatedAt = CURRENT_TIMESTAMP WHERE ProductId = @ProductId;";

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
                    new { ProductId = productId, CurrentStock = newStock },
                    transaction: transaction,
                    cancellationToken: cancellationToken);

                var rows = await connection.ExecuteAsync(command).ConfigureAwait(false);
                return rows > 0;
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
