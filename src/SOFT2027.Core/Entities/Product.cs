using System;

namespace SOFT2027.Core.Entities
{
    public class Product
    {
        public int ProductId { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        public string ProductName { get; set; } = string.Empty;
        
        public int? CategoryId { get; set; }
        public int? BrandId { get; set; }
        public int? UnitId { get; set; }
        
        // Tier 1 Default Pricing Baseline
        public decimal BasePurchasePrice { get; set; }
        public decimal PurchasePercentage { get; set; }
        public decimal DefaultSalePrice { get; set; }
        public decimal SellingPercentage { get; set; }
        
        // Stock Tracking
        public decimal CurrentStock { get; set; }
        public decimal MinimumStock { get; set; }
        
        public string Status { get; set; } = "Active";
        
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
