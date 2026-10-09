using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SOFT2027.Core.Entities;

namespace SOFT2027.Core.Interfaces;

public interface IBusinessLookupRepository
{
    Task<IReadOnlyList<LookupItem>> GetAllAsync(string kind, CancellationToken cancellationToken = default);
    Task SaveAsync(string kind, LookupItem item, CancellationToken cancellationToken = default);
    Task DeleteAsync(string kind, int id, CancellationToken cancellationToken = default);
}
