using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dapper;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using SOFT2027.Data.Services;
using Xunit;

namespace SOFT2027.Tests;

public sealed class DatabaseSnapshotMigrationTests
{
    [Fact]
    public async Task ExistingDatabase_BackfillsItemNamesAndCodes_Idempotently()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var schemaPath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "docs", "DATABASE_SCHEMA.sql"));
        var dbPath = Path.Combine(baseDir, $"test_snapshot_migration_{Guid.NewGuid():N}.db");
        var legacyPath = Path.Combine(baseDir, $"test_snapshot_migration_{Guid.NewGuid():N}.sql");
        try
        {
            var schema = await File.ReadAllTextAsync(schemaPath);
            var legacySchema = Regex.Replace(schema, @"^\s*Product(?:Code|Name)At(?:Sale|Purchase) TEXT NOT NULL DEFAULT '',\r?\n", string.Empty, RegexOptions.Multiline);
            await File.WriteAllTextAsync(legacyPath, legacySchema);
            var factory = new SqliteConnectionFactory(dbPath);
            using (var connection = (SqliteConnection)factory.CreateConnection())
            {
                await connection.OpenAsync();
                await connection.ExecuteAsync(legacySchema);
                await connection.ExecuteAsync(@"
                    INSERT INTO AdminUsers (AdminId,Username,PasswordHash,FullName) VALUES (1,'admin','hash','Admin');
                    INSERT INTO Customers (CustomerId,CustomerCode,CustomerName) VALUES (1,'C-1','Customer');
                    INSERT INTO Suppliers (SupplierId,SupplierCode,SupplierName) VALUES (1,'S-1','Supplier');
                    INSERT INTO Products (ProductId,ProductCode,ProductName,BasePurchasePrice,DefaultSalePrice) VALUES (1,'OLD-CODE','Old product name',10,15);
                    INSERT INTO Sales (SaleId,InvoiceNumber,CustomerId,SaleDate,SubTotal,NetTotal,PaymentType,Status,AdminId) VALUES (1,'INV-1',1,'2026-01-01',15,15,'Cash','Completed',1);
                    INSERT INTO SaleItems (SaleId,ProductId,Quantity,HistoricalSalePrice,HistoricalCostPrice,LineTotal) VALUES (1,1,1,15,10,15);
                    INSERT INTO Purchases (PurchaseId,InvoiceNumber,SupplierId,PurchaseDate,SubTotal,NetTotal,Status,AdminId) VALUES (1,'PUR-1',1,'2026-01-01',10,10,'Received',1);
                    INSERT INTO PurchaseItems (PurchaseId,ProductId,Quantity,HistoricalPurchasePrice,LineTotal) VALUES (1,1,1,10,10);");
            }

            var initializer = new DatabaseInitializer(factory, schemaPath);
            await initializer.InitializeAsync();
            await initializer.InitializeAsync();
            using var verify = (SqliteConnection)factory.CreateConnection();
            await verify.OpenAsync();
            (await verify.QuerySingleAsync<(string ProductCodeAtSale, string ProductNameAtSale)>("SELECT ProductCodeAtSale,ProductNameAtSale FROM SaleItems WHERE SaleId=1;")).Should().Be(("OLD-CODE", "Old product name"));
            (await verify.QuerySingleAsync<(string ProductCodeAtPurchase, string ProductNameAtPurchase)>("SELECT ProductCodeAtPurchase,ProductNameAtPurchase FROM PurchaseItems WHERE PurchaseId=1;")).Should().Be(("OLD-CODE", "Old product name"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            foreach (var path in new[] { dbPath, dbPath + "-wal", dbPath + "-shm", legacyPath }) if (File.Exists(path)) File.Delete(path);
        }
    }
}
