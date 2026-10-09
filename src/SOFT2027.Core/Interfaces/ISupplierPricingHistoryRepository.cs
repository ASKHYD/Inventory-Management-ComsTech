using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using SOFT2027.Core.Entities;

namespace SOFT2027.Core.Interfaces
{
    public interface ISupplierPricingHistoryRepository
    {
        Task<int> AddAsync(SupplierProductPricingHistory history, CancellationToken cancellationToken = default);
        Task<int> AddAsync(SupplierProductPricingHistory history, IDbTransaction? transaction, CancellationToken cancellationToken = default);
        Task<SupplierProductPricingHistory?> GetLatestAsync(int supplierId, int productId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<SupplierProductPricingHistory>> GetHistoryAsync(int supplierId, int productId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<SupplierProductPricingHistory>> GetProductHistoryAsync(int productId, CancellationToken cancellationToken = default);
    }
}
