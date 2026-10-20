using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using SOFT2027.Core.Entities;
using SOFT2027.Core.Interfaces;

namespace SOFT2027.Data.Repositories;

public sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;
    public AuditLogRepository(ISqliteConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<IReadOnlyList<AuditLogEntry>> GetRecentAsync(int limit = 500, CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 2000) limit = 500;
        using var connection = _connectionFactory.CreateConnection();
        if (connection is DbConnection dbConnection) await dbConnection.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<AuditLogEntry>(new CommandDefinition(@"
            SELECT a.AuditId, a.AdminId, u.FullName AS AdminName, a.Action, a.ModuleName,
                   a.RecordReference, a.PreviousValue, a.NewValue, a.Timestamp
            FROM AuditLogs a
            INNER JOIN AdminUsers u ON u.AdminId = a.AdminId
            ORDER BY a.Timestamp DESC, a.AuditId DESC
            LIMIT @Limit;", new { Limit = limit }, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task LogActionAsync(int adminId, string action, string moduleName, string reference, string? previousValue = null, string? newValue = null, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection is DbConnection dbConnection) await dbConnection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO AuditLogs (AdminId, Action, ModuleName, RecordReference, PreviousValue, NewValue)
            VALUES (@AdminId, @Action, @ModuleName, @Reference, @PreviousValue, @NewValue);",
            new { AdminId = adminId, Action = action, ModuleName = moduleName, Reference = reference, PreviousValue = previousValue, NewValue = newValue },
            cancellationToken: cancellationToken));
    }
}
