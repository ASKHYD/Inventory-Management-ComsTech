using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SOFT2027.Core.Entities;

namespace SOFT2027.Core.Interfaces;

public interface IAuditLogRepository
{
    Task<IReadOnlyList<AuditLogEntry>> GetRecentAsync(int limit = 500, CancellationToken cancellationToken = default);
    Task LogActionAsync(int adminId, string action, string moduleName, string reference, string? previousValue = null, string? newValue = null, CancellationToken cancellationToken = default);
}
