using System;

namespace SOFT2027.Core.Entities;

public sealed class AdminUserSummary
{
    public int AdminId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = "Administrator";
    public string Status { get; set; } = "Active";
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLogin { get; set; }
}
