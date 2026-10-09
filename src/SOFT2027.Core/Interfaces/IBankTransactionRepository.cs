using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using SOFT2027.Core.Entities;

namespace SOFT2027.Core.Interfaces
{
    public interface IBankTransactionRepository
    {
        Task<int> AddAsync(BankTransaction transaction, IDbTransaction? dbTransaction = null, CancellationToken cancellationToken = default);
        Task<decimal> GetCurrentBalanceAsync(IDbTransaction? dbTransaction = null, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<BankTransaction>> GetAllAsync(CancellationToken cancellationToken = default);
    }
}
