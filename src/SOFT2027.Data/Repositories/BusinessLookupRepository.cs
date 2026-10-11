using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using SOFT2027.Core.Entities;
using SOFT2027.Core.Interfaces;

namespace SOFT2027.Data.Repositories;

public sealed class BusinessLookupRepository : IBusinessLookupRepository
{
    private readonly ISqliteConnectionFactory _factory;
    private readonly IAdminSession? _session;
    public BusinessLookupRepository(ISqliteConnectionFactory factory, IAdminSession? session = null) { _factory = factory; _session = session; }
    private static (string Table, string Id, string Name) GetMapping(string kind) => kind switch
    {
        "Categories" => ("Categories", "CategoryId", "CategoryName"),
        "Brands" => ("Brands", "BrandId", "BrandName"),
        "Units" => ("Units", "UnitId", "UnitName"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
    public async Task<IReadOnlyList<LookupItem>> GetAllAsync(string kind, CancellationToken cancellationToken = default)
    {
        var m = GetMapping(kind);
        using var connection = _factory.CreateConnection();
        if (connection is DbConnection db) await db.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<LookupItem>(new CommandDefinition($"SELECT {m.Id} AS Id, {m.Name} AS Name FROM {m.Table} ORDER BY {m.Name} COLLATE NOCASE", cancellationToken: cancellationToken));
        return rows.AsList();
    }
    public async Task SaveAsync(string kind, LookupItem item, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(item.Name)) throw new InvalidOperationException("Enter a name before saving.");
        var m = GetMapping(kind);
        var isNew = item.Id == 0;
        using var connection = _factory.CreateConnection();
        if (connection is DbConnection db) await db.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        var previous = isNew ? null : await connection.ExecuteScalarAsync<string?>(new CommandDefinition($"SELECT {m.Name} FROM {m.Table} WHERE {m.Id}=@Id", new { item.Id }, transaction, cancellationToken: cancellationToken));
        if (isNew)
            item.Id = await connection.ExecuteScalarAsync<int>(new CommandDefinition($"INSERT INTO {m.Table} ({m.Name}) VALUES (@Name); SELECT last_insert_rowid();", new { Name = item.Name.Trim() }, transaction, cancellationToken: cancellationToken));
        else
            await connection.ExecuteAsync(new CommandDefinition($"UPDATE {m.Table} SET {m.Name}=@Name WHERE {m.Id}=@Id", new { Name = item.Name.Trim(), item.Id }, transaction, cancellationToken: cancellationToken));
        if (_session?.AdminId > 0)
            await connection.ExecuteAsync(new CommandDefinition("INSERT INTO AuditLogs (AdminId,Action,ModuleName,RecordReference,PreviousValue,NewValue) VALUES (@AdminId,@Action,@Kind,@Id,@Previous,@Name)", new { AdminId = _session.AdminId, Action = isNew ? "CREATE" : "UPDATE", Kind = kind, Id = item.Id.ToString(), Previous = previous, Name = item.Name.Trim() }, transaction, cancellationToken: cancellationToken));
        transaction.Commit();
    }
    public async Task DeleteAsync(string kind, int id, CancellationToken cancellationToken = default)
    {
        if (_session?.IsAdministrator != true)
            throw new InvalidOperationException("Only an administrator can permanently delete category, brand, or unit records. Employees may still edit these records.");
        var m = GetMapping(kind);
        using var connection = _factory.CreateConnection();
        if (connection is DbConnection db) await db.OpenAsync(cancellationToken);
        var used = await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM Products WHERE @Kind='Categories' AND CategoryId=@Id OR @Kind='Brands' AND BrandId=@Id OR @Kind='Units' AND UnitId=@Id", new { Kind = kind, Id = id }, cancellationToken: cancellationToken));
        if (used > 0) throw new InvalidOperationException("This item is assigned to existing products. Update those products before deleting it.");
        using var transaction = connection.BeginTransaction();
        var name = await connection.ExecuteScalarAsync<string?>(new CommandDefinition($"SELECT {m.Name} FROM {m.Table} WHERE {m.Id}=@Id", new { Id = id }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition($"DELETE FROM {m.Table} WHERE {m.Id}=@Id", new { Id = id }, transaction, cancellationToken: cancellationToken));
        if (_session?.AdminId > 0)
            await connection.ExecuteAsync(new CommandDefinition("INSERT INTO AuditLogs (AdminId,Action,ModuleName,RecordReference,PreviousValue) VALUES (@AdminId,'DELETE',@Kind,@Reference,@Name)", new { AdminId = _session.AdminId, Kind = kind, Reference = id.ToString(), Name = name }, transaction, cancellationToken: cancellationToken));
        transaction.Commit();
    }
}
