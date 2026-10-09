using System;

namespace SOFT2027.Core.Entities
{
    public class CustomerProductPricingHistory
    {
        public int HistoryId { get; set; }
        public int CustomerId { get; set; }
        public int ProductId { get; set; }
        public decimal SalePrice { get; set; }
        public decimal SellingPercentage { get; set; }
        public decimal DiscountPercentage { get; set; }
        public decimal Quantity { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime TransactionDate { get; set; }
        public int AdminId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
