using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SOFT2027.Core.Entities;

namespace SOFT2027.Core.Interfaces;

public interface IAuditLogRepository
{
    Task<IReadOnlyList<AuditLogEntry>> GetRecentAsync(int limit = 500, CancellationToken cancellationToken = default);
}
