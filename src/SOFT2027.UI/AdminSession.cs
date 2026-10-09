using SOFT2027.Core.Entities;
using SOFT2027.Core.Interfaces;

namespace SOFT2027.UI;

public sealed class AdminSession : IAdminSession
{
    public int AdminId { get; private set; }
    public string Username { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public bool IsSignedIn => AdminId > 0;

    public void SignIn(AdminUser user)
    {
        AdminId = user.AdminId;
        Username = user.Username;
        FullName = user.FullName;
    }

    public void SignOut()
    {
        AdminId = 0;
        Username = string.Empty;
        FullName = string.Empty;
    }
}
