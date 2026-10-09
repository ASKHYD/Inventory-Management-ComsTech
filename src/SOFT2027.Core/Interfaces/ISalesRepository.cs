using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using SOFT2027.Core.Entities;

namespace SOFT2027.Core.Interfaces
{
    public interface ISalesRepository
    {
        Task<int> AddAsync(Sale sale, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
        Task<Sale?> GetByIdAsync(int saleId, CancellationToken cancellationToken = default);
        Task<Sale?> GetByInvoiceNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Sale>> GetByCustomerIdAsync(int customerId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<SaleItem>> GetItemsBySaleIdAsync(int saleId, CancellationToken cancellationToken = default);
    }
}
