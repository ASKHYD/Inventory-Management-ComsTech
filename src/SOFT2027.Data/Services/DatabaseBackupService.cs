using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SOFT2027.Core.Interfaces;

namespace SOFT2027.Data.Services;

public sealed class DatabaseBackupService : IDatabaseBackupService
{
    private readonly string _databasePath;

    public DatabaseBackupService(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath)) throw new ArgumentException("Database path is required.", nameof(databasePath));
        _databasePath = Path.GetFullPath(databasePath);
    }

    public Task CreateBackupAsync(string destinationPath, CancellationToken cancellationToken = default) => Task.Run(async () =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        var destination = Path.GetFullPath(destinationPath);
        if (string.Equals(destination, _databasePath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Choose a different file for the backup.");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await using var source = new SqliteConnection(BuildConnectionString(_databasePath));
        await using var target = new SqliteConnection(BuildConnectionString(destination));
        await source.OpenAsync(cancellationToken);
        await target.OpenAsync(cancellationToken);
        source.BackupDatabase(target);
    }, cancellationToken);

    public Task<string> RestoreBackupAsync(string backupPath, CancellationToken cancellationToken = default) => Task.Run(async () =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        var backup = Path.GetFullPath(backupPath);
        if (!File.Exists(backup)) throw new FileNotFoundException("The selected backup file does not exist.", backup);
        if (string.Equals(backup, _databasePath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Choose a backup file, not the live database.");

        var backupFolder = Path.Combine(Path.GetDirectoryName(_databasePath)!, "Backups");
        Directory.CreateDirectory(backupFolder);
        var safetyCopy = Path.Combine(backupFolder, $"before-restore-{DateTime.Now:yyyyMMdd-HHmmss}.db");

        await using var live = new SqliteConnection(BuildConnectionString(_databasePath));
        await using var safety = new SqliteConnection(BuildConnectionString(safetyCopy));
        await live.OpenAsync(cancellationToken);
        await safety.OpenAsync(cancellationToken);
        live.BackupDatabase(safety);
        await safety.CloseAsync();

        await using var source = new SqliteConnection(BuildConnectionString(backup, readOnly: true));
        await source.OpenAsync(cancellationToken);
        var requiredTables = new[] { "AdminUsers", "Products", "Customers", "Suppliers", "Sales", "Purchases" };
        foreach (var table in requiredTables)
        {
            await using var command = source.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
            command.Parameters.AddWithValue("$name", table);
            if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 0)
                throw new InvalidOperationException("This file is not a complete SOFT2027 database backup.");
        }
        try
        {
            source.BackupDatabase(live);
        }
        catch
        {
            await safety.OpenAsync(cancellationToken);
            safety.BackupDatabase(live);
            throw;
        }
        return safetyCopy;
    }, cancellationToken);

    private static string BuildConnectionString(string path, bool readOnly = false) => new SqliteConnectionStringBuilder
    {
        DataSource = path,
        Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate,
        ForeignKeys = true,
        Pooling = false
    }.ToString();
}
