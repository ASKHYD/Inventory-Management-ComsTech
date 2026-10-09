using System;

namespace SOFT2027.Core.Entities
{
    public class SupplierLedgerEntry
    {
        public int LedgerId { get; set; }
        public int SupplierId { get; set; }
        public DateTime TransactionDate { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public string ReferenceNumber { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal RunningBalance { get; set; }
    }
}
