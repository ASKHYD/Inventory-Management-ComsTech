using System;
using System.IO;
using System.Threading.Tasks;
using Dapper;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using SOFT2027.Data.Services;

namespace SOFT2027.Tests;

public sealed class DatabaseBackupServiceTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"soft2027-backup-{Guid.NewGuid():N}");
    private string LivePath => Path.Combine(_root, "live.db");
    private string SchemaPath => Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "docs", "DATABASE_SCHEMA.sql"));

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_root);
        await new DatabaseInitializer(new SqliteConnectionFactory(LivePath), SchemaPath).InitializeAsync();
        using var connection = new SqliteConnection($"Data Source={LivePath};Foreign Keys=True;");
        await connection.OpenAsync();
        await connection.ExecuteAsync("INSERT INTO AdminUsers (Username,PasswordHash,FullName) VALUES ('admin','hash','Admin'); INSERT INTO Products (ProductCode,ProductName,BasePurchasePrice,DefaultSalePrice,CurrentStock) VALUES ('P-1','Before backup',10,20,3);");
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task BackupAndRestore_ShouldPreserveConsistentSnapshotAndCreateSafetyCopy()
    {
        var backupPath = Path.Combine(_root, "backup.db");
        var service = new DatabaseBackupService(LivePath);
        await service.CreateBackupAsync(backupPath);

        using (var connection = new SqliteConnection($"Data Source={LivePath};"))
        {
            await connection.OpenAsync();
            await connection.ExecuteAsync("UPDATE Products SET ProductName='Changed after backup' WHERE ProductCode='P-1';");
        }

        var safetyCopy = await service.RestoreBackupAsync(backupPath);
        File.Exists(safetyCopy).Should().BeTrue();
        using var restored = new SqliteConnection($"Data Source={LivePath};");
        await restored.OpenAsync();
        (await restored.ExecuteScalarAsync<string>("SELECT ProductName FROM Products WHERE ProductCode='P-1';")).Should().Be("Before backup");
    }

    [Fact]
    public async Task InvalidBackup_ShouldLeaveLiveDatabaseUntouched()
    {
        var invalidPath = Path.Combine(_root, "incomplete.db");
        using (var invalid = new SqliteConnection($"Data Source={invalidPath};"))
        {
            await invalid.OpenAsync();
            await invalid.ExecuteAsync("CREATE TABLE Products (Id INTEGER PRIMARY KEY);");
        }
        var service = new DatabaseBackupService(LivePath);
        Func<Task> restore = () => service.RestoreBackupAsync(invalidPath);
        await restore.Should().ThrowAsync<InvalidOperationException>().WithMessage("*complete SOFT2027*");

        using var live = new SqliteConnection($"Data Source={LivePath};");
        await live.OpenAsync();
        (await live.ExecuteScalarAsync<string>("SELECT ProductName FROM Products WHERE ProductCode='P-1';")).Should().Be("Before backup");
    }

    [Fact]
    public async Task BackupContainingForeignKeyErrors_ShouldBeRejectedBeforeReplacingLiveData()
    {
        var invalidPath = Path.Combine(_root, "broken-relations.db");
        await new DatabaseInitializer(new SqliteConnectionFactory(invalidPath), SchemaPath).InitializeAsync();
        using (var invalid = new SqliteConnection($"Data Source={invalidPath};Foreign Keys=False;"))
        {
            await invalid.OpenAsync();
            await invalid.ExecuteAsync("INSERT INTO AdminUsers (Username,PasswordHash,FullName) VALUES ('bad-admin','hash','Bad'); INSERT INTO Sales (InvoiceNumber,CustomerId,SaleDate,SubTotal,NetTotal,AdminId) VALUES ('BAD-1',999,'2026-01-01',1,1,1);");
        }
        var service = new DatabaseBackupService(LivePath);
        Func<Task> restore = () => service.RestoreBackupAsync(invalidPath);
        await restore.Should().ThrowAsync<InvalidOperationException>().WithMessage("*foreign-key*");

        using var live = new SqliteConnection($"Data Source={LivePath};");
        await live.OpenAsync();
        (await live.ExecuteScalarAsync<string>("SELECT ProductName FROM Products WHERE ProductCode='P-1';")).Should().Be("Before backup");
    }
}
