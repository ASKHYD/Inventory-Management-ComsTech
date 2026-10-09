using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using SOFT2027.Core.Entities;

namespace SOFT2027.Core.Interfaces
{
    public interface IReceiptRepository
    {
        Task<int> AddAsync(Receipt receipt, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
        Task<Receipt?> GetByIdAsync(int receiptId, CancellationToken cancellationToken = default);
        Task<Receipt?> GetByNumberAsync(string receiptNumber, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Receipt>> GetByCustomerIdAsync(int customerId, CancellationToken cancellationToken = default);
    }
}
