using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using SOFT2027.Core.Entities;

namespace SOFT2027.Core.Interfaces;

public interface IAdminUserRepository
{
    Task<int> GetUserCountAsync(CancellationToken cancellationToken = default);
    Task<AdminUser> CreateFirstAdminAsync(string username, string fullName, string password, CancellationToken cancellationToken = default);
    Task<AdminUser?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminUserSummary>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<int> CreateAdminAsync(string username, string fullName, string password, int actorAdminId, CancellationToken cancellationToken = default);
    Task SetStatusAsync(int adminId, bool isActive, int actorAdminId, CancellationToken cancellationToken = default);
    Task ResetPasswordAsync(int adminId, string newPassword, int actorAdminId, CancellationToken cancellationToken = default);
}
