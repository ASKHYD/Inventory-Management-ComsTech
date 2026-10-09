using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using SOFT2027.Core.Entities;

namespace SOFT2027.Core.Interfaces
{
    public interface IPurchaseRepository
    {
        Task<int> AddAsync(Purchase purchase, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
        Task<Purchase?> GetByIdAsync(int purchaseId, CancellationToken cancellationToken = default);
        Task<Purchase?> GetByInvoiceNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Purchase>> GetBySupplierIdAsync(int supplierId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<PurchaseItem>> GetItemsByPurchaseIdAsync(int purchaseId, CancellationToken cancellationToken = default);
    }
}
