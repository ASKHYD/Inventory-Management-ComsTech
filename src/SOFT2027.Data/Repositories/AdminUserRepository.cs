using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using SOFT2027.Core.Entities;
using SOFT2027.Core.Interfaces;

namespace SOFT2027.Data.Repositories;

public sealed class AdminUserRepository : IAdminUserRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public AdminUserRepository(ISqliteConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    public async Task<int> GetUserCountAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        await OpenAsync(connection, cancellationToken);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM AdminUsers;", cancellationToken: cancellationToken));
    }

    public async Task<AdminUser> CreateFirstAdminAsync(string username, string fullName, string password, CancellationToken cancellationToken = default)
    {
        var normalizedUsername = username.Trim();
        using var connection = _connectionFactory.CreateConnection();
        await OpenAsync(connection, cancellationToken);
        using var transaction = connection.BeginTransaction();
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM AdminUsers;", transaction: transaction, cancellationToken: cancellationToken));
        if (count != 0) throw new InvalidOperationException("Initial setup has already been completed. Sign in with an administrator account.");

        var passwordHash = HashPassword(password);
        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
            INSERT INTO AdminUsers (Username, PasswordHash, FullName, Status)
            VALUES (@Username, @PasswordHash, @FullName, 'Active');
            SELECT last_insert_rowid();",
            new { Username = normalizedUsername, PasswordHash = passwordHash, FullName = fullName.Trim() }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO AuditLogs (AdminId, Action, ModuleName, RecordReference, NewValue)
            VALUES (@AdminId, 'CREATE', 'AdminUsers', @RecordReference, 'Initial administrator setup');",
            new { AdminId = id, RecordReference = normalizedUsername }, transaction, cancellationToken: cancellationToken));
        transaction.Commit();
        return new AdminUser { AdminId = id, Username = normalizedUsername, FullName = fullName.Trim(), Status = "Active", CreatedAt = DateTime.UtcNow };
    }

    public async Task<AdminUser?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        await OpenAsync(connection, cancellationToken);
        var user = await connection.QuerySingleOrDefaultAsync<AdminUser>(new CommandDefinition(@"
            SELECT AdminId, Username, PasswordHash, FullName, Status, CreatedAt, LastLogin
            FROM AdminUsers WHERE Username = @Username COLLATE NOCASE;",
            new { Username = username.Trim() }, cancellationToken: cancellationToken));
        if (user == null || user.Status != "Active" || !VerifyPassword(password, user.PasswordHash)) return null;

        await connection.ExecuteAsync(new CommandDefinition(@"
            UPDATE AdminUsers SET LastLogin = CURRENT_TIMESTAMP WHERE AdminId = @AdminId;
            INSERT INTO AuditLogs (AdminId, Action, ModuleName, RecordReference, NewValue)
            VALUES (@AdminId, 'LOGIN', 'Security', @RecordReference, 'Successful sign in');",
            new { user.AdminId, RecordReference = user.Username }, cancellationToken: cancellationToken));
        return user;
    }

    public async Task<IReadOnlyList<AdminUserSummary>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        await OpenAsync(connection, cancellationToken);
        var rows = await connection.QueryAsync<AdminUserSummary>(new CommandDefinition(@"
            SELECT AdminId, Username, FullName, Status, CreatedAt, LastLogin
            FROM AdminUsers ORDER BY FullName COLLATE NOCASE;", cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<int> CreateAdminAsync(string username, string fullName, string password, int actorAdminId, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        await OpenAsync(connection, cancellationToken);
        using var transaction = connection.BeginTransaction();
        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
            INSERT INTO AdminUsers (Username, PasswordHash, FullName, Status)
            VALUES (@Username, @PasswordHash, @FullName, 'Active');
            SELECT last_insert_rowid();",
            new { Username = username.Trim(), PasswordHash = HashPassword(password), FullName = fullName.Trim() }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO AuditLogs (AdminId, Action, ModuleName, RecordReference, NewValue)
            VALUES (@ActorId, 'CREATE', 'AdminUsers', @Reference, @NewValue);",
            new { ActorId = actorAdminId, Reference = username.Trim(), NewValue = $"Created administrator id {id}" }, transaction, cancellationToken: cancellationToken));
        transaction.Commit();
        return id;
    }

    public async Task SetStatusAsync(int adminId, bool isActive, int actorAdminId, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        await OpenAsync(connection, cancellationToken);
        using var transaction = connection.BeginTransaction();
        var user = await connection.QuerySingleOrDefaultAsync<AdminUserSummary>(new CommandDefinition(
            "SELECT AdminId, Username, FullName, Status FROM AdminUsers WHERE AdminId = @AdminId;",
            new { AdminId = adminId }, transaction, cancellationToken: cancellationToken));
        if (user == null) throw new InvalidOperationException("The selected administrator no longer exists.");
        if (!isActive && user.Status == "Active")
        {
            var activeCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM AdminUsers WHERE Status = 'Active';", transaction: transaction, cancellationToken: cancellationToken));
            if (activeCount <= 1) throw new InvalidOperationException("At least one active administrator must remain.");
        }
        var status = isActive ? "Active" : "Inactive";
        await connection.ExecuteAsync(new CommandDefinition("UPDATE AdminUsers SET Status = @Status WHERE AdminId = @AdminId;",
            new { Status = status, AdminId = adminId }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO AuditLogs (AdminId, Action, ModuleName, RecordReference, PreviousValue, NewValue)
            VALUES (@ActorId, 'UPDATE', 'AdminUsers', @Reference, @Previous, @Status);",
            new { ActorId = actorAdminId, Reference = user.Username, Previous = user.Status, Status = status }, transaction, cancellationToken: cancellationToken));
        transaction.Commit();
    }

    public async Task ResetPasswordAsync(int adminId, string newPassword, int actorAdminId, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        await OpenAsync(connection, cancellationToken);
        using var transaction = connection.BeginTransaction();
        var username = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT Username FROM AdminUsers WHERE AdminId = @AdminId;", new { AdminId = adminId }, transaction, cancellationToken: cancellationToken));
        if (username == null) throw new InvalidOperationException("The selected administrator no longer exists.");
        await connection.ExecuteAsync(new CommandDefinition("UPDATE AdminUsers SET PasswordHash = @Hash WHERE AdminId = @AdminId;",
            new { Hash = HashPassword(newPassword), AdminId = adminId }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO AuditLogs (AdminId, Action, ModuleName, RecordReference, NewValue)
            VALUES (@ActorId, 'UPDATE', 'AdminUsers', @Reference, 'Password reset');",
            new { ActorId = actorAdminId, Reference = username }, transaction, cancellationToken: cancellationToken));
        transaction.Commit();
    }

    private static async Task OpenAsync(System.Data.IDbConnection connection, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (connection is DbConnection dbConnection) await dbConnection.OpenAsync(token);
        else connection.Open();
    }

    private static string HashPassword(string password)
    {
        const int iterations = 310_000;
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);
        return $"PBKDF2-SHA256${iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string encodedHash)
    {
        var parts = encodedHash.Split('$');
        if (parts.Length != 4 || parts[0] != "PBKDF2-SHA256" || !int.TryParse(parts[1], out var iterations) || iterations is < 100_000 or > 1_000_000)
            return false;
        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException) { return false; }
    }
}
