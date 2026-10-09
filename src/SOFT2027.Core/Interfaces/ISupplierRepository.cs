using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using SOFT2027.Core.Entities;

namespace SOFT2027.Core.Interfaces
{
    public interface ISupplierRepository
    {
        Task<int> AddAsync(Supplier supplier, CancellationToken cancellationToken = default);
        Task<Supplier?> GetByIdAsync(int supplierId, CancellationToken cancellationToken = default);
        Task<Supplier?> GetByIdAsync(int supplierId, IDbTransaction? transaction, CancellationToken cancellationToken = default);
        Task<Supplier?> GetByCodeAsync(string supplierCode, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Supplier>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<bool> UpdateAsync(Supplier supplier, CancellationToken cancellationToken = default);
        Task<bool> UpdateBalanceAsync(int supplierId, decimal newOutstandingBalance, CancellationToken cancellationToken = default);
        Task<bool> UpdateBalanceAsync(int supplierId, decimal newOutstandingBalance, IDbTransaction? transaction, CancellationToken cancellationToken = default);
    }
}
