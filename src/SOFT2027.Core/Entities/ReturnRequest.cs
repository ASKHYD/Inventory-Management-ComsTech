using System;
using System.Collections.Generic;

namespace SOFT2027.Core.Entities;

public sealed class ReturnRequest
{
    public bool IsSaleReturn { get; set; }
    public string OriginalInvoiceNumber { get; set; } = string.Empty;
    public string ReturnNumber { get; set; } = string.Empty;
    public DateTime ReturnDate { get; set; } = DateTime.Today;
    public int AdminId { get; set; }
    public string? Remarks { get; set; }
    public List<ReturnRequestLine> Items { get; set; } = new();
}

public sealed class ReturnRequestLine
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal RefundRate { get; set; }
}
