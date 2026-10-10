namespace SOFT2027.Core.Entities
{
    public class PurchaseItem
    {
        public int PurchaseItemId { get; set; }
        public int PurchaseId { get; set; }
        public int ProductId { get; set; }
        public string ProductCodeAtPurchase { get; set; } = string.Empty;
        public string ProductNameAtPurchase { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal HistoricalPurchasePrice { get; set; }
        public decimal PurchasePercentage { get; set; }
        public decimal DiscountPercentage { get; set; }
        public decimal LineTotal { get; set; }
    }
}
