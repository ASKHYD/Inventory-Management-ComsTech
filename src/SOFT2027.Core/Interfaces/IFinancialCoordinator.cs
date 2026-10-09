using System.Threading;
using System.Threading.Tasks;
using SOFT2027.Core.Entities;

namespace SOFT2027.Core.Interfaces
{
    public interface IFinancialCoordinator
    {
        Task<int> PostReceiptAsync(Receipt receipt, CancellationToken cancellationToken = default);
        Task<int> PostPaymentAsync(Payment payment, CancellationToken cancellationToken = default);
        Task<int> PostExpenseAsync(Expense expense, CancellationToken cancellationToken = default);
    }
}
