using SOFT2027.Core.Entities;

namespace SOFT2027.Core.Interfaces;

public interface IAdminSession
{
    int AdminId { get; }
    string Username { get; }
    string FullName { get; }
    string Role { get; }
    bool IsAdministrator { get; }
    bool IsSignedIn { get; }
    void SignIn(AdminUser user);
    void SignOut();
}
