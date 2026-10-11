using SOFT2027.Core.Entities;
using SOFT2027.Core.Interfaces;

namespace SOFT2027.UI;

public sealed class AdminSession : IAdminSession
{
    public int AdminId { get; private set; }
    public string Username { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public string Role { get; private set; } = string.Empty;
    public bool IsAdministrator => string.Equals(Role, "Administrator", System.StringComparison.OrdinalIgnoreCase);
    public bool IsSignedIn => AdminId > 0;

    public void SignIn(AdminUser user)
    {
        AdminId = user.AdminId;
        Username = user.Username;
        FullName = user.FullName;
        Role = user.Role;
    }

    public void SignOut()
    {
        AdminId = 0;
        Username = string.Empty;
        FullName = string.Empty;
        Role = string.Empty;
    }
}
