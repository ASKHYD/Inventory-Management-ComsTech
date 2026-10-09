using System;
using System.Collections.Generic;

namespace SOFT2027.Core.Entities
{
    public class Sale
    {
        public int SaleId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public DateTime SaleDate { get; set; }
        public decimal SubTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal NetTotal { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal RemainingBalance { get; set; }
        public string PaymentType { get; set; } = string.Empty;
        public string PaymentSource { get; set; } = "Cash";
        public string Status { get; set; } = string.Empty;
        public int AdminId { get; set; }
        public string? Remarks { get; set; }
        public DateTime CreatedAt { get; set; }

        public List<SaleItem> Items { get; set; } = new List<SaleItem>();
    }
}
