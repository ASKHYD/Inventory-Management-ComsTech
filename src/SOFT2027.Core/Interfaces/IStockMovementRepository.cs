using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using SOFT2027.Core.Entities;

namespace SOFT2027.Core.Interfaces
{
    public interface IStockMovementRepository
    {
        Task<int> AddAsync(StockMovement movement, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<StockMovement>> GetByProductIdAsync(int productId, CancellationToken cancellationToken = default);
        Task<decimal> GetCurrentStockAsync(int productId, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
    }
}
