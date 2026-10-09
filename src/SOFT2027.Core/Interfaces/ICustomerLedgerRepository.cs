using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using SOFT2027.Core.Entities;

namespace SOFT2027.Core.Interfaces
{
    public interface ICustomerLedgerRepository
    {
        Task<int> AddAsync(CustomerLedgerEntry entry, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<CustomerLedgerEntry>> GetByCustomerIdAsync(int customerId, CancellationToken cancellationToken = default);
        Task<decimal> GetRunningBalanceAsync(int customerId, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
    }
}
