using System.Threading;
using System.Threading.Tasks;
using SOFT2027.Core.Entities;

namespace SOFT2027.Core.Interfaces
{
    public interface ITransactionCoordinator
    {
        Task<int> PostSaleAsync(Sale sale, CancellationToken cancellationToken = default);
        Task<int> PostPurchaseAsync(Purchase purchase, CancellationToken cancellationToken = default);
        Task<int> PostReturnAsync(ReturnRequest request, CancellationToken cancellationToken = default);
    }
}
