using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using SOFT2027.Core.Entities;

namespace SOFT2027.Core.Interfaces
{
    public interface ICustomerPricingHistoryRepository
    {
        Task<int> AddAsync(CustomerProductPricingHistory history, CancellationToken cancellationToken = default);
        Task<int> AddAsync(CustomerProductPricingHistory history, IDbTransaction? transaction, CancellationToken cancellationToken = default);
        Task<CustomerProductPricingHistory?> GetLatestAsync(int customerId, int productId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<CustomerProductPricingHistory>> GetHistoryAsync(int customerId, int productId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<CustomerProductPricingHistory>> GetProductHistoryAsync(int productId, CancellationToken cancellationToken = default);
    }
}
