using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using SOFT2027.Core.Entities;

namespace SOFT2027.Core.Interfaces
{
    public interface IExpenseRepository
    {
        Task<int> AddAsync(Expense expense, IDbTransaction? transaction = null, CancellationToken cancellationToken = default);
        Task<Expense?> GetByIdAsync(int expenseId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Expense>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Expense>> GetAllAsync(CancellationToken cancellationToken = default);
    }
}
