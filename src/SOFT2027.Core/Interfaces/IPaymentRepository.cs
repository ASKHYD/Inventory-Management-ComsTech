using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using SOFT2027.Core.Entities;

namespace SOFT2027.Core.Interfaces
{
    public interface IPaymentRepository
    {
        Task<int> AddAsync(Payment payment, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
        Task<Payment?> GetByIdAsync(int paymentId, CancellationToken cancellationToken = default);
        Task<Payment?> GetByNumberAsync(string paymentNumber, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Payment>> GetBySupplierIdAsync(int supplierId, CancellationToken cancellationToken = default);
    }
}
