using System;

namespace SOFT2027.Core.Entities;

public sealed class AuditLogEntry
{
    public int AuditId { get; set; }
    public int AdminId { get; set; }
    public string AdminName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public string RecordReference { get; set; } = string.Empty;
    public string? PreviousValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime Timestamp { get; set; }
}
