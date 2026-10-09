using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using SOFT2027.Core.Entities;

namespace SOFT2027.Core.Interfaces
{
    public interface ICustomerRepository
    {
        Task<int> AddAsync(Customer customer, CancellationToken cancellationToken = default);
        Task<Customer?> GetByIdAsync(int customerId, CancellationToken cancellationToken = default);
        Task<Customer?> GetByIdAsync(int customerId, IDbTransaction? transaction, CancellationToken cancellationToken = default);
        Task<Customer?> GetByCodeAsync(string customerCode, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<bool> UpdateAsync(Customer customer, CancellationToken cancellationToken = default);
        Task<bool> UpdateBalanceAsync(int customerId, decimal newOutstandingBalance, CancellationToken cancellationToken = default);
        Task<bool> UpdateBalanceAsync(int customerId, decimal newOutstandingBalance, IDbTransaction? transaction, CancellationToken cancellationToken = default);
    }
}
