using System;
using System.Collections.Generic;

namespace SOFT2027.Core.Entities
{
    public class Purchase
    {
        public int PurchaseId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public DateTime PurchaseDate { get; set; }
        public decimal SubTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal NetTotal { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal RemainingBalance { get; set; }
        public string PaymentSource { get; set; } = "Cash";
        public string Status { get; set; } = string.Empty;
        public int AdminId { get; set; }
        public string? Remarks { get; set; }
        public DateTime CreatedAt { get; set; }

        public List<PurchaseItem> Items { get; set; } = new List<PurchaseItem>();
    }
}
