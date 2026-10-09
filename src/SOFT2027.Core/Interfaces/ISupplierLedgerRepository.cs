using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using SOFT2027.Core.Entities;

namespace SOFT2027.Core.Interfaces
{
    public interface ISupplierLedgerRepository
    {
        Task<int> AddAsync(SupplierLedgerEntry entry, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<SupplierLedgerEntry>> GetBySupplierIdAsync(int supplierId, CancellationToken cancellationToken = default);
        Task<decimal> GetRunningBalanceAsync(int supplierId, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
    }
}
