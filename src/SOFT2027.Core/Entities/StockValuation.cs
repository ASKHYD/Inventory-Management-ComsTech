using System;
using System.Collections.Generic;

namespace SOFT2027.Core.Entities
{
    public class StockValuation
    {
        public DateTime ValuationDate { get; set; } = DateTime.UtcNow;
        public decimal TotalValuation { get; set; }
        public decimal TotalPotentialRetailValue { get; set; }
        public int TotalItemsCount { get; set; }
        public List<StockValuationItem> Items { get; set; } = new();
    }

    public class StockValuationItem
    {
        public int ProductId { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public decimal CurrentStock { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalValuation { get; set; }
        public decimal DefaultSalePrice { get; set; }
        public decimal PotentialRetailValue { get; set; }
    }
}
