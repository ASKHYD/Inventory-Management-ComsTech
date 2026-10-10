namespace SOFT2027.Core.Entities
{
    public class SaleItem
    {
        public int SaleItemId { get; set; }
        public int SaleId { get; set; }
        public int ProductId { get; set; }
        public string ProductCodeAtSale { get; set; } = string.Empty;
        public string ProductNameAtSale { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal HistoricalSalePrice { get; set; }
        public decimal HistoricalCostPrice { get; set; }
        public decimal SellingPercentage { get; set; }
        public decimal DiscountPercentage { get; set; }
        public decimal LineTotal { get; set; }
    }
}
