using System;

namespace SOFT2027.Core.Entities
{
    public class StockMovement
    {
        public int MovementId { get; set; }
        public int ProductId { get; set; }
        public DateTime MovementDate { get; set; }
        public string MovementType { get; set; } = string.Empty;
        public string ReferenceNumber { get; set; } = string.Empty;
        public decimal QuantityIn { get; set; }
        public decimal QuantityOut { get; set; }
        public decimal BalanceQuantity { get; set; }
        public int AdminId { get; set; }
        public string? Remarks { get; set; }
    }
}
