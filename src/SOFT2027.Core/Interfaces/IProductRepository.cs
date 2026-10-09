using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using SOFT2027.Core.Entities;

namespace SOFT2027.Core.Interfaces
{
    public interface IProductRepository
    {
        Task<int> AddAsync(Product product, CancellationToken cancellationToken = default);
        Task<Product?> GetByIdAsync(int productId, CancellationToken cancellationToken = default);
        Task<Product?> GetByIdAsync(int productId, IDbTransaction? transaction, CancellationToken cancellationToken = default);
        Task<Product?> GetByCodeAsync(string productCode, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<bool> UpdateAsync(Product product, CancellationToken cancellationToken = default);
        Task<bool> UpdateStockAsync(int productId, decimal newStock, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
    }
}
