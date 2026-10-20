using System;
using System.Collections.Generic;

namespace SOFT2027.Core.Entities;

public sealed class OperationalReportRequest
{
    public string ReportKey { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int? CustomerId { get; set; }
    public int? SupplierId { get; set; }
    public int? ProductId { get; set; }
    public int? CategoryId { get; set; }
    public string? ExpenseCategory { get; set; }
}

public sealed class OperationalReport
{
    public string Title { get; set; } = string.Empty;
    public List<OperationalReportColumn> Columns { get; set; } = new();
    public List<OperationalReportRow> Rows { get; set; } = new();
}

public sealed class OperationalReportColumn
{
    public string Key { get; set; } = string.Empty;
    public string Header { get; set; } = string.Empty;
    public string? Format { get; set; }
}

public sealed class OperationalReportRow
{
    public Dictionary<string, object?> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
